using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using iNKORE.UI.WPF.Modern.Controls;
using STranslate.Core;
using STranslate.Plugin;
using System.Windows;

namespace STranslate.ViewModels.Pages;

/// <summary>
/// 全局术语库管理。术语对所有翻译服务共享，与语言方向无关。
/// </summary>
public partial class GlossaryViewModel : ObservableObject
{
    private readonly GlossaryService _glossaryService;
    private readonly Internationalization _i18n;

    [ObservableProperty]
    public partial GlossaryEntry? SelectedEntry { get; set; }

    public GlossaryService GlossaryService => _glossaryService;

    public IReadOnlyList<GlossaryEntry> Entries => _glossaryService.Entries;

    public GlossaryViewModel(GlossaryService glossaryService, Internationalization i18n)
    {
        _glossaryService = glossaryService;
        _i18n = i18n;
    }

    [RelayCommand]
    private void Add() => SelectedEntry = _glossaryService.Add();

    [RelayCommand]
    private void Remove(GlossaryEntry entry) => _glossaryService.Remove(entry);

    [RelayCommand]
    private async Task ClearAsync(FrameworkElement element)
    {
        if (_glossaryService.Entries.Count == 0)
            return;

        var dialog = new ContentDialog
        {
            Title = _i18n.GetTranslation("Prompt"),
            Content = _i18n.GetTranslation("GlossaryClearAllConfirm"),
            CloseButtonText = _i18n.GetTranslation("Cancel"),
            PrimaryButtonText = _i18n.GetTranslation("Confirm"),
            DefaultButton = ContentDialogButton.Close,
        };

        // ContentDialog 必须显式指定 owner，避免依赖活动窗口推断。
        var result = Window.GetWindow(element) is { } owner
            ? await dialog.ShowAsync(owner)
            : await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
            _glossaryService.Clear();
    }
}
