using STranslate.Plugin;
using STranslate.Plugin.Translate.OpenAI;

namespace STranslate.Tests;

public class OpenAIGlossaryPromptTests
{
    [Fact]
    public void NoEntries_ProducesNoMessage()
    {
        Assert.Null(GlossaryPrompt.CreateMessage(null));
        Assert.Null(GlossaryPrompt.CreateMessage([]));
    }

    [Fact]
    public void AllBlankEntries_ProduceNoMessage()
    {
        // 术语全为空白时不应该发出一条只有表头的 system 消息。
        Assert.Null(GlossaryPrompt.CreateMessage(
        [
            new GlossaryEntry("   ", "接口"),
            new GlossaryEntry("API", "\t")
        ]));
    }

    [Fact]
    public void MessageIsSystemRoleAndListsEveryEntry()
    {
        var message = GlossaryPrompt.CreateMessage(
        [
            new GlossaryEntry("API", "接口"),
            new GlossaryEntry("token", "令牌")
        ]);

        Assert.NotNull(message);
        Assert.Equal("system", message.Role);
        Assert.Contains("API => 接口", message.Content);
        Assert.Contains("token => 令牌", message.Content);
    }

    [Fact]
    public void BlankEntriesAreSkippedAndValuesTrimmed()
    {
        var message = GlossaryPrompt.CreateMessage(
        [
            new GlossaryEntry("  ", "接口"),
            new GlossaryEntry("API", "   "),
            new GlossaryEntry(" token ", " 令牌 ")
        ]);

        Assert.NotNull(message);
        Assert.DoesNotContain("接口", message.Content);
        Assert.Contains("token => 令牌", message.Content);
    }

    [Fact]
    public void ContentIsStableForSameEntries()
    {
        // 术语提示属于稳定前缀，同样的术语必须渲染出完全相同的文本，否则缓存永远不命中。
        List<GlossaryEntry> entries = [new("API", "接口"), new("token", "令牌")];

        var first = GlossaryPrompt.CreateMessage(entries);
        var second = GlossaryPrompt.CreateMessage(entries);

        Assert.Equal(first!.Content, second!.Content);
    }

    [Fact]
    public void ContentChangesWhenEntriesChange()
    {
        var first = GlossaryPrompt.CreateMessage([new GlossaryEntry("API", "接口")]);
        var second = GlossaryPrompt.CreateMessage([new GlossaryEntry("API", "应用程序接口")]);

        Assert.NotEqual(first!.Content, second!.Content);
    }
}
