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
    /// 判断模型是否属于会接受缓存字段的 OpenAI 模型。
    /// </summary>
    /// <remarks>
    /// 插件允许把地址指向任意 OpenAI 兼容服务，而那些服务（DeepSeek、GLM、Qwen 等）
    /// 往往不认识 <c>prompt_cache_key</c> 之类的字段，直接发送可能被判为非法请求。
    /// 因此只有识别出的 OpenAI 模型才附带缓存字段，其余模型保持请求体原样。
    /// </remarks>
    internal static bool IsOpenAIModel(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
            return false;

        var name = model.Trim();
        return name.Contains("gpt", StringComparison.OrdinalIgnoreCase) ||
               OSeriesRegex().IsMatch(name);
    }

    /// <summary>
    /// 判断模型是否支持显式缓存断点（GPT-5.6 及以后）。
    /// 无法识别的模型名一律视为不支持，从而保持请求体对兼容服务的兼容性。
    /// </summary>
    internal static bool SupportsExplicitBreakpoint(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
            return false;

        var match = ModelVersionRegex().Match(model.Trim());
        if (!match.Success)
            return false;

        var major = int.Parse(match.Groups[1].Value);
        var minor = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;

        return major > 5 || (major == 5 && minor >= 6);
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
