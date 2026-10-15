using CommunityToolkit.Mvvm.ComponentModel;
using STranslate.Core;
using STranslate.Plugin;

namespace STranslate.ViewModels.Pages;

/// <summary>
/// 设置页「常用语言」列表中的一行。
/// 直接引用 <see cref="DataProvider.LangEnumData"/>，以便界面语言切换后显示名自动刷新。
/// </summary>
public partial class LanguageOption : ObservableObject
{
    public LanguageOption(DataProvider.LangEnumData data, bool isEnabled)
    {
        Data = data;
        IsEnabled = isEnabled;
    }

    public DataProvider.LangEnumData Data { get; }

    public LangEnum Value => Data.Value;

    /// <summary>
    /// 是否在主界面语言选择框中展示
    /// </summary>
    [ObservableProperty] public partial bool IsEnabled { get; set; }
}
