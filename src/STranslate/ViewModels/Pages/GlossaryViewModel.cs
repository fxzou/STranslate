using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using iNKORE.UI.WPF.Modern.Controls;
using Microsoft.Win32;
using STranslate.Core;
using STranslate.Helpers;
using STranslate.Plugin;
using System.IO;
using System.Windows;

namespace STranslate.ViewModels.Pages;

/// <summary>
/// 全局术语库管理。术语对所有翻译服务共享，与语言方向无关。
/// </summary>
public partial class GlossaryViewModel : ObservableObject
{
    private const string FileFilter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*";

    private readonly GlossaryService _glossaryService;
    private readonly Internationalization _i18n;
    private readonly ISnackbar _snackbar;

    [ObservableProperty]
    public partial GlossaryEntry? SelectedEntry { get; set; }

    public GlossaryService GlossaryService => _glossaryService;

    public IReadOnlyList<GlossaryEntry> Entries => _glossaryService.Entries;

    public GlossaryViewModel(GlossaryService glossaryService, Internationalization i18n, ISnackbar snackbar)
    {
        _glossaryService = glossaryService;
        _i18n = i18n;
        _snackbar = snackbar;
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

    [RelayCommand]
    private async Task ExportAsync()
    {
        var dialog = new SaveFileDialog
        {
            Title = _i18n.GetTranslation("SaveAs"),
            Filter = FileFilter,
            DefaultExt = ".json",
            FileName = $"stranslate_glossary_{DateTime.Now:yyyyMMddHHmmss}.json",
            DefaultDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            AddToRecent = true
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            var json = GlossaryFileHelper.Serialize(_glossaryService.Entries);
            await File.WriteAllTextAsync(dialog.FileName, json, GlossaryFileHelper.Utf8BomEncoding);

            _snackbar.ShowSuccess(_i18n.GetTranslation("ExportSuccess"));
        }
        catch (Exception ex)
        {
            _snackbar.ShowError($"{_i18n.GetTranslation("ExportFailed")}: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = _i18n.GetTranslation("GlossaryImport"),
            Filter = FileFilter,
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            var json = await File.ReadAllTextAsync(dialog.FileName);
            var entries = GlossaryFileHelper.Deserialize(json);

            if (entries == null)
            {
                _snackbar.ShowError(_i18n.GetTranslation("GlossaryImportInvalidFile"));
                return;
            }

            var added = _glossaryService.Import(entries);
            _snackbar.ShowSuccess(string.Format(_i18n.GetTranslation("GlossaryImportResult"), added, entries.Count - added));
        }
        catch (Exception ex)
        {
            _snackbar.ShowError($"{_i18n.GetTranslation("GlossaryImportFailed")}: {ex.Message}");
        }
    }
}
