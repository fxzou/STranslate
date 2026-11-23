using STranslate.Plugin;
using System.Collections.Specialized;
using System.ComponentModel;

namespace STranslate.Core;

/// <summary>
/// 全局术语库服务。术语对所有翻译服务共享，与语言方向无关。
/// </summary>
public class GlossaryService : IGlossaryProvider
{
    private readonly GlossarySettings _settings;

    /// <summary>
    /// 已订阅 <see cref="INotifyPropertyChanged"/> 的条目。
    /// 单独记一份是为了在集合被整体清空（Reset）时仍能逐个退订。
    /// </summary>
    private readonly HashSet<GlossaryEntry> _attached = [];

    /// <summary>
    /// 批量导入期间挂起持久化，避免逐条写盘。
    /// </summary>
    private bool _suspendSave;

    public GlossaryService(GlossarySettings settings)
    {
        _settings = settings;

        // 现有条目可能来自配置文件，同样需要订阅以便改动后立即持久化。
        foreach (var entry in _settings.Entries)
            Attach(entry);

        _settings.Entries.CollectionChanged += OnEntriesChanged;
    }

    /// <inheritdoc />
    public IReadOnlyList<GlossaryEntry> Entries => _settings.Entries;

    /// <inheritdoc />
    public bool HasEntries => GetEffectiveEntries().Count > 0;

    /// <summary>
    /// 新建一条术语并加入术语库。
    /// </summary>
    public GlossaryEntry Add(string source = "", string target = "")
    {
        var entry = new GlossaryEntry(source, target);
        _settings.Entries.Add(entry);
        return entry;
    }

    /// <summary>
    /// 移除指定术语。
    /// </summary>
    public bool Remove(GlossaryEntry entry) => _settings.Entries.Remove(entry);

    /// <summary>
    /// 清空术语库。
    /// </summary>
    public void Clear() => _settings.Entries.Clear();

    /// <summary>
    /// 批量导入术语。已存在的同原文同译文的条目会被跳过。
    /// </summary>
    /// <returns>实际新增的条目数量。</returns>
    public int Import(IEnumerable<GlossaryEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var existing = _settings.Entries
            .Select(entry => (entry.Source?.Trim(), entry.Target?.Trim()))
            .ToHashSet();

        var added = 0;
        // 导入期间挂起逐条落盘，结束时统一保存一次。
        _suspendSave = true;
        try
        {
            foreach (var entry in entries)
            {
                var source = entry.Source?.Trim();
                var target = entry.Target?.Trim();
                if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
                    continue;

                if (!existing.Add((source, target)))
                    continue;

                _settings.Entries.Add(new GlossaryEntry(source, target));
                added++;
            }
        }
        finally
        {
            _suspendSave = false;
        }

        if (added > 0)
            _settings.Save();

        return added;
    }

    /// <inheritdoc />
    public IReadOnlyList<GlossaryEntry> GetEffectiveEntries() =>
        _settings.Entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Source) &&
                            !string.IsNullOrWhiteSpace(entry.Target))
            .Select(entry => new GlossaryEntry(entry.Source.Trim(), entry.Target.Trim()))
            .ToList();

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Clear() 触发的是 Reset，此时拿不到 OldItems，只能按自己记录的集合退订。
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var entry in _attached.ToList())
                Detach(entry);
        }

        if (e.OldItems != null)
            foreach (GlossaryEntry entry in e.OldItems)
                Detach(entry);

        if (e.NewItems != null)
            foreach (GlossaryEntry entry in e.NewItems)
                Attach(entry);

        SaveIfNeeded();
    }

    private void SaveIfNeeded()
    {
        if (_suspendSave)
            return;

        _settings.Save();
    }

    private void Attach(GlossaryEntry entry)
    {
        entry.PropertyChanged -= OnEntryPropertyChanged;
        entry.PropertyChanged += OnEntryPropertyChanged;
        _attached.Add(entry);
    }

    private void Detach(GlossaryEntry entry)
    {
        entry.PropertyChanged -= OnEntryPropertyChanged;
        _attached.Remove(entry);
    }

    private void OnEntryPropertyChanged(object? sender, PropertyChangedEventArgs e) => SaveIfNeeded();
}
