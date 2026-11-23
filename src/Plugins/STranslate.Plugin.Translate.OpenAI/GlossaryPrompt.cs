using System.Text;

namespace STranslate.Plugin.Translate.OpenAI;

/// <summary>
/// 把全局术语库渲染成提示词文本，并并入消息列表。
/// </summary>
internal static class GlossaryPrompt
{
    /// <summary>
    /// 构建术语提示文本。没有可用术语时返回 <see langword="null"/>。
    /// </summary>
    internal static string? CreateText(IReadOnlyList<GlossaryEntry>? entries)
    {
        if (entries is not { Count: > 0 })
            return null;

        // 先筛出真正可用的条目，避免术语全为空白时仍发出一条只有表头的提示。
        var effective = entries
            .Select(entry => (Source: entry.Source.Trim(), Target: entry.Target.Trim()))
            .Where(entry => entry.Source.Length > 0 && entry.Target.Length > 0)
            .ToList();

        if (effective.Count == 0)
            return null;

        var builder = new StringBuilder();
        builder.AppendLine("The following glossary defines the only correct translation for specific terms.");
        builder.AppendLine("When a source term appears in the text, translate it exactly as the glossary specifies,");
        builder.AppendLine("and never paraphrase, reorder or inflect the required target term. The glossary applies only");
        builder.AppendLine("to terms that actually appear in the text; ignore the rest.");
        builder.AppendLine();
        builder.AppendLine("Glossary (source => target):");

        foreach (var (source, target) in effective)
        {
            builder.Append("- ").Append(source).Append(" => ").AppendLine(target);
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// 把术语要求并入消息列表。没有可用术语时不做任何改动。
    /// </summary>
    /// <remarks>
    /// 术语要求并入开头那条 system / developer 消息，而不是新增一条消息，原因是：
    /// Gemini 的 OpenAI 兼容层只接受一条 system 指令（多条时只保留最后一条），
    /// DeepSeek 同样要求 system 消息位于对话开头，因此额外插入一条 system 消息
    /// 会让术语库静默失效，甚至让请求被判为非法。
    /// 并入首条消息后术语仍然位于最前面、仍属于稳定前缀，提示词缓存的设计不受影响。
    /// </remarks>
    internal static void Apply(List<PromptItem> messages, IReadOnlyList<GlossaryEntry>? entries)
    {
        var text = CreateText(entries);
        if (text is null)
            return;

        if (messages.Count > 0 && IsInstructionRole(messages[0].Role))
        {
            var first = messages[0];
            first.Content = string.IsNullOrWhiteSpace(first.Content)
                ? text
                : text + "\n\n" + first.Content;
            return;
        }

        messages.Insert(0, new PromptItem("system", text));
    }

    /// <summary>
    /// 判断角色是否属于「指令」类消息，这类消息可以承载术语要求且必须位于对话最前。
    /// </summary>
    private static bool IsInstructionRole(string role) =>
        string.Equals(role, "system", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, "developer", StringComparison.OrdinalIgnoreCase);
}
