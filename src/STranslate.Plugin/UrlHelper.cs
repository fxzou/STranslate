namespace STranslate.Plugin;

/// <summary>
/// URL 路径匹配规则类型
/// </summary>
public enum UrlPathMatchRule
{
    /// <summary>
    /// OpenAI / Claude 规则: 匹配 "/"、"/v1" 或以 "/openai" 结尾的兼容基址
    /// （例如 Gemini 的 "/v1beta/openai"）
    /// </summary>
    OpenAI,

    /// <summary>
    /// ChatGLM 规则: 匹配 "/" 或 "/api" 或 "/api/paas/v4"
    /// </summary>
    ChatGLM,

    /// <summary>
    /// OpenRouter 规则: 匹配 "/" 或 "/api/v1"
    /// </summary>
    OpenRouter,

    /// <summary>
    /// 严格规则: 仅匹配 "/"
    /// </summary>
    Strict,
}

/// <summary>
/// URL 处理辅助类
/// </summary>
public static class UrlHelper
{
    /// <summary>
    /// 构建最终请求的完整 URL
    /// </summary>
    /// <param name="url">原始URL</param>
    /// <param name="path">默认路径,默认为 "/v1/chat/completions"</param>
    /// <param name="rule">路径匹配规则,默认为 OpenAI 规则</param>
    /// <returns>处理后的完整URL</returns>
    /// <remarks>
    /// 规则:
    /// <list type="bullet">
    /// <item>如果 URL 以 "#" 结尾,移除 "#" 并强制使用该地址,不添加默认路径</item>
    /// <item>如果 URL 路径匹配规则,自动添加 defaultPath</item>
    /// <item>其他情况保持原样</item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// // 使用默认规则
    /// var url1 = UrlHelper.BuildFinalUrl("https://api.openai.com/");
    /// // 结果: https://api.openai.com/v1/chat/completions
    /// 
    /// // ChatGLM 使用自定义规则
    /// var url2 = UrlHelper.BuildFinalUrl(
    ///     "https://open.bigmodel.cn/",
    ///     "/api/paas/v4/chat/completions",
    ///     UrlPathMatchRule.ChatGLM
    /// );
    /// // 结果: https://open.bigmodel.cn/api/paas/v4/chat/completions
    /// 
    /// // 强制使用指定地址
    /// var url3 = UrlHelper.BuildFinalUrl("https://api.custom.com/my/path#");
    /// // 结果: https://api.custom.com/my/path
    /// </code>
    /// </example>
    public static string BuildFinalUrl(
        string url,
        string path = DefaultChatCompletionsPath,
        UrlPathMatchRule rule = UrlPathMatchRule.OpenAI)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        try
        {
            // 如果以 # 结尾,表示强制使用该地址
            if (url.TrimEnd().EndsWith('#'))
            {
                string forcedUrl = url.TrimEnd('#').TrimEnd();
                return new UriBuilder(forcedUrl).Uri.ToString();
            }

            var builder = new UriBuilder(url);

            // 根据规则判断是否需要替换路径
            if (ShouldReplacePath(builder.Path, rule))
                builder.Path = BuildPath(builder.Path, path);

            return builder.Uri.ToString();
        }
        catch
        {
            return url;
        }
    }

    /// <summary>
    /// 判断路径是否需要替换
    /// </summary>
    private static bool ShouldReplacePath(string path, UrlPathMatchRule rule)
    {
        return rule switch
        {
            UrlPathMatchRule.OpenAI => path == "/" || path == "/v1" || IsOpenAICompatibleBase(path),
            UrlPathMatchRule.ChatGLM => path == "/" || path == "/api" || path == "/api/paas/v4",
            UrlPathMatchRule.OpenRouter => path == "/" || path == "/api/v1",
            _ => path == "/"
        };
    }

    /// <summary>
    /// 判断路径是否为带版本段的 OpenAI 兼容基址，
    /// 例如 Gemini 的 "/v1beta/openai"、"/v1/openai"。
    /// </summary>
    private static bool IsOpenAICompatibleBase(string path)
    {
        var trimmed = path.TrimEnd('/');
        const string suffix = "/openai";

        if (!trimmed.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            return false;

        // 必须像 Gemini 那样在 "/openai" 之前还有版本段，避免把
        // 用户自建的、恰好以 "/openai" 结尾的完整地址误判为基址。
        var version = trimmed[..^suffix.Length];
        return version.Length > 2 &&
               version[0] == '/' &&
               (version[1] == 'v' || version[1] == 'V');
    }

    /// <summary>
    /// 依据基址计算最终路径
    /// </summary>
    /// <remarks>
    /// 普通基址（"/" 或 "/v1"）直接用默认路径替换；
    /// 带版本段的 "/openai" 兼容基址（如 "/v1beta/openai"）把版本段放在 "/openai" 之前，
    /// 因此保留该前缀，只把默认路径里的 "/v1" 一段去掉。
    /// </remarks>
    private static string BuildPath(string basePath, string path)
    {
        if (!IsOpenAICompatibleBase(basePath))
            return path;

        var prefix = basePath.TrimEnd('/');

        // 只剥掉 "/v1" 这一段版本前缀，且后面必须紧跟分隔符，
        // 避免把 "/v1beta/..." 之类的路径误当作版本段。
        if (path.Length > 3 &&
            path[..3].Equals("/v1", StringComparison.OrdinalIgnoreCase) &&
            path[3] == '/')
        {
            return prefix + path[3..];
        }

        return prefix + path;
    }

    internal const string DefaultChatCompletionsPath = "/v1/chat/completions";
}