using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace STranslate.Plugin.Translate.OpenAI;

/// <summary>
/// 提示词缓存辅助。让每次翻译请求都能复用同一段稳定前缀的缓存。
/// </summary>
/// <remarks>
/// 提示词缓存按「前缀」匹配：一旦某个请求把变化的内容（待翻译原文）也算进缓存前缀，
/// 下一个请求的前缀就会不同，永远命中不了。因此这里把连续位于最前面的
/// system / developer 消息视为稳定前缀，并在其末尾打上显式缓存断点。
/// </remarks>
internal static partial class PromptCaching
{
    /// <summary>
    /// 显式缓存断点（<c>prompt_cache_options</c> / <c>prompt_cache_breakpoint</c>）
    /// 自 GPT-5.6 起才被支持，更早的模型以及多数 OpenAI 兼容服务不认识这些字段，
    /// 贸然发送会直接被判为非法请求。
    /// </summary>
    [GeneratedRegex(@"^gpt[-_ ]?(\d+)(?:[.\-](\d+))?", RegexOptions.IgnoreCase)]
    private static partial Regex ModelVersionRegex();

    /// <summary>
    /// 匹配 o1 / o3 / o4-mini 这类以字母 o 开头的 OpenAI 推理模型。
    /// </summary>
    [GeneratedRegex(@"^o\d", RegexOptions.IgnoreCase)]
    private static partial Regex OSeriesRegex();

    /// <summary>
    /// 依据模型名称推断提供该模型的服务商。
    /// </summary>
    /// <remarks>
    /// 同一个插件要同时服务官方 OpenAI 与各类兼容端点，而各家对缓存的实现方式并不相同：
    /// OpenAI 需要在请求里声明 <c>prompt_cache_key</c> 与显式断点，DeepSeek、Gemini 等则是
    /// 服务端按前缀自动缓存、多传字段反而会被判为非法请求。因此这里先按模型名认出服务商，
    /// 再由 <see cref="SupportsRequestCacheFields"/> 决定是否附带缓存字段。
    /// 识别不出的模型按 <see cref="LlmProvider.Unknown"/> 处理，保持请求体原样最安全。
    /// </remarks>
    internal static LlmProvider DetectProvider(string? model)
    {
        var name = NormalizeModelName(model);
        if (name.Length == 0)
            return LlmProvider.Unknown;

        if (name.Contains("deepseek", StringComparison.OrdinalIgnoreCase))
            return LlmProvider.DeepSeek;

        if (name.StartsWith("gemini", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("gemma", StringComparison.OrdinalIgnoreCase))
            return LlmProvider.Gemini;

        if (name.Contains("claude", StringComparison.OrdinalIgnoreCase))
            return LlmProvider.Anthropic;

        if (name.Contains("qwen", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("qwq", StringComparison.OrdinalIgnoreCase))
            return LlmProvider.Qwen;

        if (name.Contains("glm", StringComparison.OrdinalIgnoreCase))
            return LlmProvider.Zhipu;

        if (name.Contains("moonshot", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("kimi", StringComparison.OrdinalIgnoreCase))
            return LlmProvider.Moonshot;

        if (name.Contains("mistral", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("mixtral", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("codestral", StringComparison.OrdinalIgnoreCase))
            return LlmProvider.Mistral;

        if (name.Contains("gpt", StringComparison.OrdinalIgnoreCase) ||
            OSeriesRegex().IsMatch(name) ||
            name.StartsWith("chatgpt", StringComparison.OrdinalIgnoreCase))
            return LlmProvider.OpenAI;

        return LlmProvider.Unknown;
    }

    /// <summary>
    /// 判断该服务商是否需要在请求中附带缓存字段。
    /// </summary>
    /// <remarks>
    /// 只有 OpenAI 官方接口认 <c>prompt_cache_key</c>；DeepSeek、Gemini、Claude 等
    /// 都是服务端按前缀自动缓存，多传字段只会带来被拒收的风险。
    /// 注意这不代表它们没有缓存：只要能保持稳定前缀，它们同样会命中各自的隐式缓存。
    /// </remarks>
    internal static bool SupportsRequestCacheFields(LlmProvider provider) =>
        provider == LlmProvider.OpenAI;

    /// <summary>
    /// 判断模型是否支持显式缓存断点（GPT-5.6 及以后）。
    /// 无法识别的模型名一律视为不支持，从而保持请求体对兼容服务的兼容性。
    /// </summary>
    internal static bool SupportsExplicitBreakpoint(string? model)
    {
        if (DetectProvider(model) != LlmProvider.OpenAI)
            return false;

        var match = ModelVersionRegex().Match(NormalizeModelName(model));
        if (!match.Success)
            return false;

        var major = int.Parse(match.Groups[1].Value);
        var minor = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;

        return major > 5 || (major == 5 && minor >= 6);
    }

    /// <summary>
    /// 归一化模型名：去掉首尾空白，并剥掉「openai/gpt-4o」「deepseek/deepseek-chat」
    /// 这类聚合网关的厂商前缀，使后续判断只看模型本身。
    /// </summary>
    private static string NormalizeModelName(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
            return string.Empty;

        var name = model.Trim();
        var slash = name.LastIndexOf('/');
        return slash >= 0 && slash < name.Length - 1 ? name[(slash + 1)..] : name;
    }

    /// <summary>
    /// 计算稳定前缀包含的消息条数，即开头连续的 system / developer 消息数量。
    /// </summary>
    internal static int GetStablePrefixCount(IReadOnlyList<PromptItem> messages)
    {
        var count = 0;
        while (count < messages.Count && IsStableRole(messages[count].Role))
            count++;

        return count;
    }

    /// <summary>
    /// 依据稳定前缀内容生成稳定的缓存路由键。
    /// 前缀相同则键相同，前缀变化（改提示词或改术语库）则键随之变化。
    /// </summary>
    internal static string? CreateCacheKey(IReadOnlyList<PromptItem> messages, int stablePrefixCount)
    {
        if (stablePrefixCount <= 0)
            return null;

        var builder = new StringBuilder();
        for (var i = 0; i < stablePrefixCount; i++)
        {
            builder.Append(messages[i].Role)
                .Append('\u001f')
                .Append(messages[i].Content)
                .Append('\u001e');
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return "stranslate-" + Convert.ToHexString(hash.AsSpan(0, 8)).ToLowerInvariant();
    }

    private static bool IsStableRole(string role) =>
        string.Equals(role, "system", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, "developer", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// 由模型名称推断出的服务商。用于决定是否在请求中附带缓存字段。
/// </summary>
internal enum LlmProvider
{
    /// <summary>无法识别的模型，按最保守的方式处理：不附带任何缓存字段。</summary>
    Unknown,
    /// <summary>OpenAI 官方接口，支持 <c>prompt_cache_key</c> 与显式断点。</summary>
    OpenAI,
    /// <summary>DeepSeek，服务端按前缀自动缓存，无需请求字段。</summary>
    DeepSeek,
    /// <summary>Google Gemini 的 OpenAI 兼容层，服务端自动缓存。</summary>
    Gemini,
    /// <summary>Anthropic Claude，服务端自动缓存。</summary>
    Anthropic,
    /// <summary>阿里通义千问。</summary>
    Qwen,
    /// <summary>智谱 GLM。</summary>
    Zhipu,
    /// <summary>月之暗面 Kimi。</summary>
    Moonshot,
    /// <summary>Mistral。</summary>
    Mistral,
}
