using CommunityToolkit.Mvvm.ComponentModel;

namespace STranslate.Plugin;

/// <summary>
/// 一条术语。「原文」在翻译时应当被替换为「译文」。
/// </summary>
public partial class GlossaryEntry : ObservableObject
{
    private string _source = string.Empty;
    private string _target = string.Empty;

    /// <summary>
    /// 原文（术语本体）
    /// </summary>
    public string Source { get => _source; set => SetProperty(ref _source, value); }

    /// <summary>
    /// 译文（期望的译法）
    /// </summary>
    public string Target { get => _target; set => SetProperty(ref _target, value); }

    /// <summary>
    /// 术语条目
    /// </summary>
    public GlossaryEntry() { }

    /// <summary>
    /// 术语条目
    /// </summary>
    /// <param name="source">原文</param>
    /// <param name="target">译文</param>
    public GlossaryEntry(string source, string target)
    {
        Source = source;
        Target = target;
    }
}

/// <summary>
/// 全局术语库。术语库由主程序维护，与具体语言方向无关。
/// </summary>
public interface IGlossaryProvider
{
    /// <summary>
    /// 当前生效的术语列表，按用户维护的顺序返回。
    /// </summary>
    IReadOnlyList<GlossaryEntry> Entries { get; }

    /// <summary>
    /// 术语库是否包含可用条目。
    /// </summary>
    bool HasEntries => Entries.Any(entry =>
        !string.IsNullOrWhiteSpace(entry.Source) && !string.IsNullOrWhiteSpace(entry.Target));

    /// <summary>
    /// 获取可直接用于提示词的术语列表：仅保留原文与译文都已填写的条目，
    /// 并对两端做去除首尾空白处理。
    /// </summary>
    IReadOnlyList<GlossaryEntry> GetEffectiveEntries();
}
