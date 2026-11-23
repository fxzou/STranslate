using System.Text;

namespace STranslate.Plugin.Translate.OpenAI;

/// <summary>
/// 把全局术语库渲染成一条独立的 system 消息。
/// </summary>
internal static class GlossaryPrompt
{
    /// <summary>
    /// 构建术语提示消息。没有可用术语时返回 <see langword="null"/>。
    /// </summary>
    /// <remarks>
    /// 该消息固定插入在会话最前面，属于稳定前缀，便于模型侧命中提示词缓存。
    /// </remarks>
    internal static PromptItem? CreateMessage(IReadOnlyList<GlossaryEntry>? entries)
    {
        if (entries is not { Count: > 0 })
            return null;

        // 先筛出真正可用的条目，避免术语全为空白时仍发出一条只有表头的消息。
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

        return new PromptItem("system", builder.ToString().TrimEnd());
    }
}
