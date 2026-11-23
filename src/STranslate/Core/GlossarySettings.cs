using STranslate.Plugin;
using System.Collections.ObjectModel;

namespace STranslate.Core;

/// <summary>
/// 全局术语库配置。术语与语言方向无关，由所有翻译服务共享。
/// </summary>
public class GlossarySettings
{
    private AppStorage<GlossarySettings> Storage { get; set; } = null!;

    /// <summary>
    /// 术语条目。仅 <see cref="GlossaryEntry.Source"/> 与
    /// <see cref="GlossaryEntry.Target"/> 均非空的条目会参与翻译。
    /// </summary>
    /// <remarks>
    /// 用 <see cref="ObservableCollection{T}"/> 而非 <see cref="List{T}"/>，
    /// 这样术语页的增删能直接反映到界面，服务也能订阅集合变化做持久化。
    /// </remarks>
    public ObservableCollection<GlossaryEntry> Entries { get; set; } = [];

    public void SetStorage(AppStorage<GlossarySettings> storage) => Storage = storage;

    public void Save() => Storage?.Save();
}
