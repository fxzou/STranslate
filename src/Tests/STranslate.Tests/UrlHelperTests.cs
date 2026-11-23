using STranslate.Plugin;

namespace STranslate.Tests;

public class UrlHelperTests
{
    [Theory]
    [InlineData("https://api.deepseek.com", "https://api.deepseek.com/v1/chat/completions")]
    [InlineData("https://api.deepseek.com/", "https://api.deepseek.com/v1/chat/completions")]
    [InlineData("https://api.deepseek.com/v1", "https://api.deepseek.com/v1/chat/completions")]
    [InlineData("https://api.openai.com", "https://api.openai.com/v1/chat/completions")]
    public void OpenAiRule_AppendsDefaultPathToRootAndV1Bases(string url, string expected)
    {
        Assert.Equal(expected, UrlHelper.BuildFinalUrl(url));
    }

    [Theory]
    [InlineData("https://generativelanguage.googleapis.com/v1beta/openai")]
    [InlineData("https://generativelanguage.googleapis.com/v1beta/openai/")]
    public void OpenAiRule_AppendsDefaultPathToGeminiCompatibleBase(string url)
    {
        // Gemini 的 OpenAI 兼容基址是 /v1beta/openai，版本段在 /openai 之前，
        // 必须保留该前缀，否则会拼出 /v1/chat/completions 而 404。
        Assert.Equal(
            "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions",
            UrlHelper.BuildFinalUrl(url));
    }

    [Fact]
    public void OpenAiRule_KeepsVersionPrefixForCustomPath()
    {
        Assert.Equal(
            "https://generativelanguage.googleapis.com/v1beta/openai/responses",
            UrlHelper.BuildFinalUrl(
                "https://generativelanguage.googleapis.com/v1beta/openai",
                "/v1/responses"));
    }

    [Fact]
    public void OpenAiRule_PreservesCustomPaths()
    {
        Assert.Equal(
            "https://example.com/custom",
            UrlHelper.BuildFinalUrl("https://example.com/custom"));
    }

    [Fact]
    public void OpenAiRule_RequiresVersionSegmentBeforeOpenAiSuffix()
    {
        // 只有 "/版本段/openai" 才算兼容基址；
        // 恰好以 "/openai" 结尾的普通路径不应被改写。
        Assert.Equal(
            "https://example.com/openai",
            UrlHelper.BuildFinalUrl("https://example.com/openai"));
    }

    [Fact]
    public void TrailingHash_ForcesExactUrlWithoutDefaultPath()
    {
        Assert.Equal(
            "https://group.example.com/v1beta/openai/custom",
            UrlHelper.BuildFinalUrl("https://group.example.com/v1beta/openai/custom#"));
    }

    [Fact]
    public void EmptyUrl_ReturnsEmptyString()
    {
        Assert.Equal(string.Empty, UrlHelper.BuildFinalUrl("   "));
    }

    [Fact]
    public void StrictRule_OnlyMatchesRootPath()
    {
        Assert.Equal(
            "https://example.com/v1/chat/completions",
            UrlHelper.BuildFinalUrl("https://example.com", rule: UrlPathMatchRule.Strict));

        // 严格规则下 /v1 不算基址，保持原样。
        Assert.Equal(
            "https://example.com/v1",
            UrlHelper.BuildFinalUrl("https://example.com/v1", rule: UrlPathMatchRule.Strict));
    }
}
