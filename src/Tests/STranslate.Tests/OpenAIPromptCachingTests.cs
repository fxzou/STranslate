using STranslate.Plugin;
using STranslate.Plugin.Translate.OpenAI;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace STranslate.Tests;

public class OpenAIPromptCachingTests
{
    private static readonly JsonSerializerOptions RequestJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Theory]
    [InlineData("gpt-5.6-sol", true)]
    [InlineData("gpt-5.6", true)]
    [InlineData("gpt-5.6-terra", true)]
    [InlineData("gpt-6.2-astra", true)]
    [InlineData("gpt-6", true)]
    [InlineData("gpt-5.5", false)]
    [InlineData("gpt-5", false)]
    [InlineData("gpt-4o", false)]
    [InlineData("gpt-4.1-mini", false)]
    [InlineData("o3", false)]
    [InlineData("deepseek-chat", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void SupportsExplicitBreakpoint_OnlyFromGpt56(string? model, bool expected)
    {
        Assert.Equal(expected, PromptCaching.SupportsExplicitBreakpoint(model));
    }

    [Theory]
    [InlineData("gpt-4o", true)]
    [InlineData("gpt-5.6-sol", true)]
    [InlineData("GPT-4.1-mini", true)]
    [InlineData("o1", true)]
    [InlineData("o3-mini", true)]
    [InlineData("o4-mini", true)]
    [InlineData("deepseek-chat", false)]
    [InlineData("glm-4", false)]
    [InlineData("qwen-max", false)]
    [InlineData("claude-3-5-sonnet", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsOpenAIModel_OnlyRecognizesOpenAIFamilies(string? model, bool expected)
    {
        Assert.Equal(expected, PromptCaching.IsOpenAIModel(model));
    }

    [Fact]
    public void StablePrefixCount_CoversLeadingSystemAndDeveloperMessages()
    {
        List<PromptItem> messages =
        [
            new("system", "glossary"),
            new("developer", "extra"),
            new("user", "translate this")
        ];

        Assert.Equal(2, PromptCaching.GetStablePrefixCount(messages));
    }

    [Fact]
    public void StablePrefixCount_IsZeroWhenFirstMessageIsNotStable()
    {
        List<PromptItem> messages = [new("user", "translate this")];

        Assert.Equal(0, PromptCaching.GetStablePrefixCount(messages));
    }

    [Fact]
    public void CacheKeyIsStableForSamePrefixAndChangesWithContent()
    {
        List<PromptItem> messages = [new("system", "glossary"), new("user", "a")];
        List<PromptItem> other = [new("system", "glossary"), new("user", "b")];
        List<PromptItem> changedPrefix = [new("system", "glossary v2"), new("user", "a")];

        var key = PromptCaching.CreateCacheKey(messages, 1);

        // 原文（user 消息）不在稳定前缀内，不应影响键。
        Assert.Equal(key, PromptCaching.CreateCacheKey(other, 1));
        Assert.NotEqual(key, PromptCaching.CreateCacheKey(changedPrefix, 1));
        Assert.Null(PromptCaching.CreateCacheKey(messages, 0));
    }

    [Fact]
    public void RequestOmitsCacheFieldsByDefault()
    {
        // 未启用缓存时请求体必须与改造前完全一致，避免兼容服务拒收未知字段。
        var request = OpenAIProtocol.CreateRequest(
            OpenAIApiMode.ChatCompletions,
            "test-model",
            CreateMessages(),
            0.7);

        var json = JsonSerializer.SerializeToNode(request, RequestJsonOptions)!;

        Assert.Null(json["prompt_cache_key"]);
        Assert.Null(json["prompt_cache_options"]);
        Assert.Equal("system", json["messages"]?[0]?["role"]?.ToString());
        Assert.Equal("glossary", json["messages"]?[0]?["content"]?.ToString());
    }

    [Fact]
    public void RequestAddsPromptCacheKeyWithoutExplicitBreakpoint()
    {
        var request = OpenAIProtocol.CreateRequest(
            OpenAIApiMode.ChatCompletions,
            "gpt-4o",
            CreateMessages(),
            0.7,
            promptCacheKey: "stranslate-abc");

        var json = JsonSerializer.SerializeToNode(request, RequestJsonOptions)!;

        Assert.Equal("stranslate-abc", json["prompt_cache_key"]?.ToString());
        Assert.Null(json["prompt_cache_options"]);
        // 没有显式断点时消息保持纯字符串内容，兼容性最好。
        Assert.Equal("glossary", json["messages"]?[0]?["content"]?.ToString());
    }

    [Theory]
    [InlineData(OpenAIApiMode.ChatCompletions, "text")]
    [InlineData(OpenAIApiMode.Responses, "input_text")]
    public void ExplicitBreakpointMarksEndOfStablePrefix(OpenAIApiMode apiMode, string textBlockType)
    {
        var request = OpenAIProtocol.CreateRequest(
            apiMode,
            "gpt-5.6-sol",
            CreateMessages(),
            0.7,
            promptCacheKey: "stranslate-abc",
            useExplicitCacheBreakpoint: true);

        var json = JsonSerializer.SerializeToNode(request, RequestJsonOptions)!;
        var payload = apiMode == OpenAIApiMode.Responses ? json["input"] : json["messages"];

        Assert.Equal("explicit", json["prompt_cache_options"]?["mode"]?.ToString());

        // 第一条（system）带断点，内容块化。
        var block = payload?[0]?["content"]?[0];
        Assert.Equal(textBlockType, block?["type"]?.ToString());
        Assert.Equal("glossary", block?["text"]?.ToString());
        Assert.Equal("explicit", block?["prompt_cache_breakpoint"]?["mode"]?.ToString());

        // 第二条（user）不带断点，保持纯字符串，从而不被写入缓存。
        Assert.Equal("translate", payload?[1]?["content"]?.ToString());
    }

    [Fact]
    public void ExplicitBreakpointIsSkippedWhenNoStablePrefix()
    {
        List<PromptItem> messages = [new("user", "translate")];

        var request = OpenAIProtocol.CreateRequest(
            OpenAIApiMode.ChatCompletions,
            "gpt-5.6-sol",
            messages,
            0.7,
            promptCacheKey: "stranslate-abc",
            useExplicitCacheBreakpoint: true);

        var json = JsonSerializer.SerializeToNode(request, RequestJsonOptions)!;

        Assert.Null(json["prompt_cache_options"]);
        Assert.Equal("translate", json["messages"]?[0]?["content"]?.ToString());
    }

    [Fact]
    public void AdditionalParametersCannotOverridePromptCacheFields()
    {
        Assert.Throws<FormatException>(() => OpenAIProtocol.CreateRequest(
            OpenAIApiMode.ChatCompletions,
            "gpt-5.6-sol",
            CreateMessages(),
            0.7,
            """{"prompt_cache_key":"hijack"}""",
            promptCacheKey: "stranslate-abc"));

        Assert.Throws<FormatException>(() => OpenAIProtocol.CreateRequest(
            OpenAIApiMode.ChatCompletions,
            "gpt-5.6-sol",
            CreateMessages(),
            0.7,
            """{"prompt_cache_options":{"mode":"implicit"}}""",
            useExplicitCacheBreakpoint: true));
    }

    private static List<PromptItem> CreateMessages() =>
    [
        new("system", "glossary"),
        new("user", "translate")
    ];
}
