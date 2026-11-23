using STranslate.Plugin;
using STranslate.Plugin.Translate.OpenAI;

namespace STranslate.Tests;

public class OpenAIGlossaryPromptTests
{
    [Fact]
    public void NoEntries_ProduceNoText()
    {
        Assert.Null(GlossaryPrompt.CreateText(null));
        Assert.Null(GlossaryPrompt.CreateText([]));
    }

    [Fact]
    public void AllBlankEntries_ProduceNoText()
    {
        // 术语全为空白时不应该发出一条只有表头的提示。
        Assert.Null(GlossaryPrompt.CreateText(
        [
            new GlossaryEntry("   ", "接口"),
            new GlossaryEntry("API", "\t")
        ]));
    }

    [Fact]
    public void TextListsEveryEntry()
    {
        var text = GlossaryPrompt.CreateText(
        [
            new GlossaryEntry("API", "接口"),
            new GlossaryEntry("token", "令牌")
        ]);

        Assert.NotNull(text);
        Assert.Contains("API => 接口", text);
        Assert.Contains("token => 令牌", text);
    }

    [Fact]
    public void BlankEntriesAreSkippedAndValuesTrimmed()
    {
        var text = GlossaryPrompt.CreateText(
        [
            new GlossaryEntry("  ", "接口"),
            new GlossaryEntry("API", "   "),
            new GlossaryEntry(" token ", " 令牌 ")
        ]);

        Assert.NotNull(text);
        Assert.DoesNotContain("接口", text);
        Assert.Contains("token => 令牌", text);
    }

    [Fact]
    public void TextIsStableForSameEntries()
    {
        // 术语提示属于稳定前缀，同样的术语必须渲染出完全相同的文本，否则缓存永远不命中。
        List<GlossaryEntry> entries = [new("API", "接口"), new("token", "令牌")];

        var first = GlossaryPrompt.CreateText(entries);
        var second = GlossaryPrompt.CreateText(entries);

        Assert.Equal(first, second);
    }

    [Fact]
    public void TextChangesWhenEntriesChange()
    {
        var first = GlossaryPrompt.CreateText([new GlossaryEntry("API", "接口")]);
        var second = GlossaryPrompt.CreateText([new GlossaryEntry("API", "应用程序接口")]);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Apply_MergesIntoExistingSystemMessage()
    {
        // Gemini 只认一条 system、DeepSeek 要求 system 在开头，
        // 因此术语必须并入首条指令消息，而不是新增一条。
        List<PromptItem> messages =
        [
            new("system", "You are a translation engine."),
            new("user", "translate")
        ];

        GlossaryPrompt.Apply(messages, [new GlossaryEntry("API", "接口")]);

        Assert.Equal(2, messages.Count);
        Assert.Equal("system", messages[0].Role);
        Assert.Contains("API => 接口", messages[0].Content);
        Assert.Contains("You are a translation engine.", messages[0].Content);
        Assert.Equal("user", messages[1].Role);
    }

    [Fact]
    public void Apply_PutsGlossaryBeforeOriginalInstruction()
    {
        List<PromptItem> messages = [new("system", "persona")];

        GlossaryPrompt.Apply(messages, [new GlossaryEntry("API", "接口")]);

        var content = messages[0].Content;
        Assert.True(
            content.IndexOf("API => 接口", StringComparison.Ordinal) <
            content.IndexOf("persona", StringComparison.Ordinal));
    }

    [Fact]
    public void Apply_MergesIntoDeveloperMessage()
    {
        List<PromptItem> messages = [new("Developer", "persona")];

        GlossaryPrompt.Apply(messages, [new GlossaryEntry("API", "接口")]);

        Assert.Single(messages);
        Assert.Contains("API => 接口", messages[0].Content);
        Assert.Contains("persona", messages[0].Content);
    }

    [Fact]
    public void Apply_InsertsSystemMessageWhenFirstMessageIsNotInstruction()
    {
        // 自定义提示词可能不带 system，此时仍要保证术语位于最前。
        List<PromptItem> messages = [new("user", "translate")];

        GlossaryPrompt.Apply(messages, [new GlossaryEntry("API", "接口")]);

        Assert.Equal(2, messages.Count);
        Assert.Equal("system", messages[0].Role);
        Assert.Contains("API => 接口", messages[0].Content);
        Assert.Equal("user", messages[1].Role);
    }

    [Fact]
    public void Apply_UsesGlossaryAsContentWhenFirstMessageIsEmpty()
    {
        List<PromptItem> messages = [new("system", "   ")];

        GlossaryPrompt.Apply(messages, [new GlossaryEntry("API", "接口")]);

        Assert.Single(messages);
        Assert.StartsWith("The following glossary", messages[0].Content);
    }

    [Fact]
    public void Apply_DoesNothingWithoutEntries()
    {
        List<PromptItem> messages = [new("system", "persona"), new("user", "translate")];

        GlossaryPrompt.Apply(messages, null);
        GlossaryPrompt.Apply(messages, []);

        Assert.Equal(2, messages.Count);
        Assert.Equal("persona", messages[0].Content);
    }

    [Fact]
    public void Apply_KeepsSingleSystemMessageForCompatibleEndpoints()
    {
        // 关键回归点：无论是否启用术语库，system 消息数量都必须保持为 1。
        List<PromptItem> messages = [new("system", "persona"), new("user", "translate")];

        GlossaryPrompt.Apply(messages, [new GlossaryEntry("API", "接口")]);

        Assert.Single(messages.Where(message =>
            string.Equals(message.Role, "system", StringComparison.OrdinalIgnoreCase)));
    }
}
