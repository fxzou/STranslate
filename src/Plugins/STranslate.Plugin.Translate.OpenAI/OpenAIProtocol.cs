using System.Text.Json;
using System.Text.Json.Nodes;

namespace STranslate.Plugin.Translate.OpenAI;

internal static class OpenAIProtocol
{
    private const string ChatCompletionsPath = "/v1/chat/completions";
    private const string ResponsesPath = "/v1/responses";

    internal static string BuildFinalUrl(string url, OpenAIApiMode apiMode)
    {
        var path = apiMode == OpenAIApiMode.Responses ? ResponsesPath : ChatCompletionsPath;
        return UrlHelper.BuildFinalUrl(url, path);
    }

    internal static object CreateRequest(
        OpenAIApiMode apiMode,
        string model,
        IReadOnlyCollection<PromptItem> messages,
        double temperature,
        string? additionalParametersJson = null,
        bool sendTemperature = false,
        string? promptCacheKey = null,
        bool useExplicitCacheBreakpoint = false)
    {
        var list = messages as IReadOnlyList<PromptItem> ?? messages.ToList();

        // 只有确实存在稳定前缀时才值得发显式断点；否则一律退回普通请求，
        // 既不发 prompt_cache_options，也避免让兼容服务收到看不懂的字段。
        var applyBreakpoint = useExplicitCacheBreakpoint &&
                              PromptCaching.GetStablePrefixCount(list) > 0;

        var payload = applyBreakpoint
            ? BuildPayloadWithCacheBreakpoint(apiMode, list)
            : JsonSerializer.SerializeToNode(list);

        JsonObject request = apiMode switch
        {
            OpenAIApiMode.Responses => new JsonObject
            {
                ["model"] = model,
                ["input"] = payload,
                ["stream"] = true,
                ["store"] = false
            },
            _ => new JsonObject
            {
                ["model"] = model,
                ["messages"] = payload,
                ["stream"] = true
            }
        };

        if (applyBreakpoint)
        {
            // 显式模式下由我们决定缓存边界，隐式断点会被关闭，
            // 从而避免把每次都变化的原文写进缓存。
            request["prompt_cache_options"] = new JsonObject
            {
                ["mode"] = "explicit"
            };
        }

        if (sendTemperature)
            request["temperature"] = temperature;

        // 稳定的缓存路由键：前缀相同的请求落到同一台缓存机器，从而提高命中率。
        if (!string.IsNullOrWhiteSpace(promptCacheKey))
            request["prompt_cache_key"] = promptCacheKey;

        AppendAdditionalParameters(request, additionalParametersJson);
        return request;
    }

    /// <summary>
    /// 把稳定前缀的最后一条消息内容改写为内容块数组，并在块上打显式缓存断点。
    /// 「稳定前缀」指开头连续的 system / developer 消息，它们是跨请求不变的部分。
    /// </summary>
    /// <remarks>
    /// 两种 API 的文本块类型名不同：Chat Completions 用 <c>text</c>，
    /// Responses 用 <c>input_text</c>。
    /// </remarks>
    private static JsonArray? BuildPayloadWithCacheBreakpoint(
        OpenAIApiMode apiMode,
        IReadOnlyCollection<PromptItem> messages)
    {
        var list = messages as IReadOnlyList<PromptItem> ?? messages.ToList();
        var stablePrefixCount = PromptCaching.GetStablePrefixCount(list);
        if (stablePrefixCount == 0)
            return JsonSerializer.SerializeToNode(list);

        var textBlockType = apiMode == OpenAIApiMode.Responses ? "input_text" : "text";
        var breakpointIndex = stablePrefixCount - 1;
        var nodes = new JsonArray();

        for (var i = 0; i < list.Count; i++)
        {
            var item = list[i];
            if (i != breakpointIndex)
            {
                nodes.Add(JsonSerializer.SerializeToNode(item));
                continue;
            }

            nodes.Add(new JsonObject
            {
                ["role"] = item.Role,
                ["content"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["type"] = textBlockType,
                        ["text"] = item.Content,
                        ["prompt_cache_breakpoint"] = new JsonObject
                        {
                            ["mode"] = "explicit"
                        }
                    }
                }
            });
        }

        return nodes;
    }

    private static void AppendAdditionalParameters(JsonObject request, string? additionalParametersJson)
    {
        if (string.IsNullOrWhiteSpace(additionalParametersJson))
            return;

        JsonObject additionalParameters;
        try
        {
            additionalParameters = JsonNode.Parse(additionalParametersJson) as JsonObject
                ?? throw new FormatException("附加请求参数的根节点必须是 JSON 对象。");
        }
        catch (JsonException ex)
        {
            throw new FormatException("附加请求参数不是有效的 JSON。", ex);
        }

        foreach (var (name, value) in additionalParameters)
        {
            if (string.Equals(name, "temperature", StringComparison.OrdinalIgnoreCase) ||
                request.Any(item => string.Equals(item.Key, name, StringComparison.OrdinalIgnoreCase)))
                throw new FormatException($"附加请求参数不能覆盖内置字段“{name}”。");

            request[name] = value?.DeepClone();
        }
    }

    internal static OpenAIStreamEvent ParseStreamLine(OpenAIApiMode apiMode, string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return default;

        var payload = line.StartsWith("data:", StringComparison.Ordinal)
            ? line["data:".Length..].Trim()
            : line.Trim();

        if (payload.Length == 0 || payload.Equals("[DONE]", StringComparison.Ordinal))
            return default;

        if (!payload.StartsWith('{'))
            return default;

        JsonNode? parsedData;
        try
        {
            parsedData = JsonNode.Parse(payload);
        }
        catch
        {
            // 部分 OpenAI-compatible 服务会在 SSE 中混入非 JSON 状态行。
            return default;
        }

        if (parsedData is null)
            return default;

        var errorMessage = GetErrorMessage(parsedData);
        if (!string.IsNullOrWhiteSpace(errorMessage))
            return new OpenAIStreamEvent(null, errorMessage);

        var choices = parsedData["choices"] as JsonArray;
        var textDelta = apiMode switch
        {
            OpenAIApiMode.Responses when parsedData["type"]?.ToString() == "response.output_text.delta"
                => parsedData["delta"]?.ToString(),
            OpenAIApiMode.ChatCompletions
                => choices is { Count: > 0 }
                    ? choices[0]?["delta"]?["content"]?.ToString()
                    : null,
            _ => null
        };

        return string.IsNullOrEmpty(textDelta)
            ? default
            : new OpenAIStreamEvent(textDelta, null);
    }

    private static string? GetErrorMessage(JsonNode parsedData)
    {
        if (parsedData["type"]?.ToString() == "error")
            return parsedData["message"]?.ToString();

        if (parsedData["type"]?.ToString() == "response.failed")
            return parsedData["response"]?["error"]?["message"]?.ToString();

        return parsedData["error"]?["message"]?.ToString();
    }
}

internal readonly record struct OpenAIStreamEvent(string? TextDelta, string? ErrorMessage);
