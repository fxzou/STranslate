using CommunityToolkit.Mvvm.Input;
using STranslate.Core;
using STranslate.Helpers;
using STranslate.Plugin;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;

namespace STranslate.ViewModels.Pages;

public partial class GeneralViewModel : SearchViewModelBase
{
    public GeneralViewModel(
        Settings settings,
        DataProvider dataProvider,
        Internationalization i18n) : base(i18n, "General_")
    {
        Settings = settings;
        DataProvider = dataProvider;
        Languages = i18n.LoadAvailableLanguages();

        InitializeMainHeaderActions();
        InitializeLanguageOptions();

        VisibleHeaderActions.CollectionChanged += OnMainHeaderActionsChanged;
        AvailableHeaderActions.CollectionChanged += OnMainHeaderActionsChanged;
        LanguageOptions.CollectionChanged += OnLanguageOptionsChanged;
        Settings.PropertyChanged += OnSettingsPropertyChanged;
    }

    private bool _isSyncingMainHeaderActions;
    private bool _isMainHeaderSyncPending;
    private bool _isSyncingLanguageOptions;

    public ObservableCollection<string> VisibleHeaderActions { get; } = [];

    public ObservableCollection<string> AvailableHeaderActions { get; } = [];

    /// <summary>
    /// 主界面语言选择框的候选语言，顺序即 <see cref="Settings.CommonLanguages"/> 的顺序
    /// </summary>
    public ObservableCollection<LanguageOption> LanguageOptions { get; } = [];

    [RelayCommand]
    private void ResetFontFamily() => Settings.FontFamily = Win32Helper.GetSystemDefaultFont();

    [RelayCommand]
    private void ResetAutoTransDelay() => Settings.AutoTranslateDelayMs = 500;

    [RelayCommand]
    private void ResetFontSize() => Settings.FontSize = 14;

    [RelayCommand]
    private void ResetMainWindowMaxHeightRatio() => Settings.MainWindowMaxHeightRatio = 0.85;

    [RelayCommand]
    private void ShowAllHeaderActions()
    {
        _isSyncingMainHeaderActions = true;
        try
        {
            ReplaceItems(VisibleHeaderActions, MainHeaderActions.DefaultOrder);
            ReplaceItems(AvailableHeaderActions, []);
        }
        finally
        {
            _isSyncingMainHeaderActions = false;
        }

        SyncMainHeaderActions();
    }

    [RelayCommand]
    private void HideAllHeaderActions()
    {
        _isSyncingMainHeaderActions = true;
        try
        {
            ReplaceItems(VisibleHeaderActions, []);
            ReplaceItems(AvailableHeaderActions, MainHeaderActions.DefaultOrder);
        }
        finally
        {
            _isSyncingMainHeaderActions = false;
        }

        SyncMainHeaderActions();
    }

    /// <summary>
    /// 按配置初始化语言列表：已启用语言按配置顺序在前，其余语言按枚举顺序在后
    /// </summary>
    private void InitializeLanguageOptions()
    {
        var configuredLanguages = Settings.CommonLanguages;
        var options = new List<LanguageOption>();

        foreach (var language in configuredLanguages)
        {
            var data = DataProvider.LangEnums.FirstOrDefault(x => x.Value == language);
            if (data is not null)
            {
                options.Add(new LanguageOption(data, true));
            }
        }

        foreach (var data in DataProvider.LangEnums)
        {
            if (data.Value == LangEnum.Auto || options.Any(x => x.Value == data.Value))
            {
                continue;
            }

            options.Add(new LanguageOption(data, false));
        }

        _isSyncingLanguageOptions = true;
        try
        {
            foreach (var option in LanguageOptions)
            {
                option.PropertyChanged -= OnLanguageOptionPropertyChanged;
            }

            LanguageOptions.Clear();
            foreach (var option in options)
            {
                option.PropertyChanged += OnLanguageOptionPropertyChanged;
                LanguageOptions.Add(option);
            }
        }
        finally
        {
            _isSyncingLanguageOptions = false;
        }
    }

    private void OnLanguageOptionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LanguageOption.IsEnabled))
        {
            SyncCommonLanguages();
        }
    }

    private void OnLanguageOptionsChanged(object? sender, NotifyCollectionChangedEventArgs e) => SyncCommonLanguages();

    /// <summary>
    /// 把列表的勾选状态与顺序写回 <see cref="Settings.CommonLanguages"/>
    /// </summary>
    private void SyncCommonLanguages()
    {
        if (_isSyncingLanguageOptions)
        {
            return;
        }

        Settings.ApplyCommonLanguages(LanguageOptions.Where(x => x.IsEnabled).Select(x => x.Value).ToList());
    }

    public List<int> ScreenNumbers
    {
        get
        {
            var screens = MonitorInfo.GetDisplayMonitors();
            var screenNumbers = new List<int>();
            for (int i = 1; i <= screens.Count; i++)
            {
                screenNumbers.Add(i);
            }

            return screenNumbers;
        }
    }
    public Settings Settings { get; }
    public DataProvider DataProvider { get; }

    public List<I18nPair> Languages { get; }

    public bool IsTextSeparatorMouseSelectionScopeEnabled
    {
        get => IsTextSeparatorScopeEnabled(TextSeparatorHandleScope.MouseSelection);
        set => SetTextSeparatorScope(TextSeparatorHandleScope.MouseSelection, value);
    }

    public bool IsTextSeparatorCrosswordScopeEnabled
    {
        get => IsTextSeparatorScopeEnabled(TextSeparatorHandleScope.Crossword);
        set => SetTextSeparatorScope(TextSeparatorHandleScope.Crossword, value);
    }

    public bool IsTextSeparatorIncrementalScopeEnabled
    {
        get => IsTextSeparatorScopeEnabled(TextSeparatorHandleScope.Incremental);
        set => SetTextSeparatorScope(TextSeparatorHandleScope.Incremental, value);
    }

    public bool IsTextSeparatorClipboardMonitorScopeEnabled
    {
        get => IsTextSeparatorScopeEnabled(TextSeparatorHandleScope.ClipboardMonitor);
        set => SetTextSeparatorScope(TextSeparatorHandleScope.ClipboardMonitor, value);
    }

    public bool IsTextSeparatorScreenshotTranslateScopeEnabled
    {
        get => IsTextSeparatorScopeEnabled(TextSeparatorHandleScope.ScreenshotTranslate);
        set => SetTextSeparatorScope(TextSeparatorHandleScope.ScreenshotTranslate, value);
    }

    public bool IsTextSeparatorSilentOcrScopeEnabled
    {
        get => IsTextSeparatorScopeEnabled(TextSeparatorHandleScope.SilentOcr);
        set => SetTextSeparatorScope(TextSeparatorHandleScope.SilentOcr, value);
    }

    private bool IsTextSeparatorScopeEnabled(TextSeparatorHandleScope scope)
        => (Settings.TextSeparatorHandleScopes & scope) == scope;

    private void SetTextSeparatorScope(TextSeparatorHandleScope scope, bool isEnabled)
    {
        var current = Settings.TextSeparatorHandleScopes;
        var updated = isEnabled ? current | scope : current & ~scope;
        if (updated == current)
            return;

        Settings.TextSeparatorHandleScopes = updated;
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Settings.TextSeparatorHandleScopes))
        {
            OnTextSeparatorScopePropertiesChanged();
        }
    }

    private void OnTextSeparatorScopePropertiesChanged()
    {
        OnPropertyChanged(nameof(IsTextSeparatorMouseSelectionScopeEnabled));
        OnPropertyChanged(nameof(IsTextSeparatorCrosswordScopeEnabled));
        OnPropertyChanged(nameof(IsTextSeparatorIncrementalScopeEnabled));
        OnPropertyChanged(nameof(IsTextSeparatorClipboardMonitorScopeEnabled));
        OnPropertyChanged(nameof(IsTextSeparatorScreenshotTranslateScopeEnabled));
        OnPropertyChanged(nameof(IsTextSeparatorSilentOcrScopeEnabled));
    }

    private void InitializeMainHeaderActions()
    {
        Settings.EnsureMainHeaderVisibleActionsInitialized();

        var normalizedVisible = MainHeaderActions.Normalize(Settings.MainHeaderVisibleActions);

        _isSyncingMainHeaderActions = true;
        try
        {
            ReplaceItems(VisibleHeaderActions, normalizedVisible);

            var visibleSet = normalizedVisible.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var availableActions = MainHeaderActions.DefaultOrder
                .Where(action => !visibleSet.Contains(action))
                .ToList();

            ReplaceItems(AvailableHeaderActions, availableActions);
        }
        finally
        {
            _isSyncingMainHeaderActions = false;
        }

        Settings.ApplyMainHeaderVisibleActions(normalizedVisible);
    }

    private void OnMainHeaderActionsChanged(object? _, NotifyCollectionChangedEventArgs __)
    {
        if (_isSyncingMainHeaderActions || _isMainHeaderSyncPending)
        {
            return;
        }

        _isMainHeaderSyncPending = true;

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            _isMainHeaderSyncPending = false;
            SyncMainHeaderActions();
            return;
        }

        dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            _isMainHeaderSyncPending = false;
            SyncMainHeaderActions();
        }));
    }

    private void SyncMainHeaderActions()
    {
        if (_isSyncingMainHeaderActions)
        {
            return;
        }

        _isSyncingMainHeaderActions = true;
        try
        {
            var normalizedVisible = MainHeaderActions.Normalize(VisibleHeaderActions);
            ReplaceItemsIfDifferent(VisibleHeaderActions, normalizedVisible);

            var visibleSet = normalizedVisible.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var availableActions = MainHeaderActions.DefaultOrder
                .Where(action => !visibleSet.Contains(action))
                .ToList();
            ReplaceItemsIfDifferent(AvailableHeaderActions, availableActions);

            Settings.ApplyMainHeaderVisibleActions(normalizedVisible);
        }
        finally
        {
            _isSyncingMainHeaderActions = false;
        }
    }

    private static void ReplaceItems(ObservableCollection<string> target, IEnumerable<string> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }

    private static void ReplaceItemsIfDifferent(ObservableCollection<string> target, IReadOnlyList<string> source)
    {
        if (target.Count == source.Count && target.SequenceEqual(source))
        {
            return;
        }

        ReplaceItems(target, source);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Settings.PropertyChanged -= OnSettingsPropertyChanged;
            LanguageOptions.CollectionChanged -= OnLanguageOptionsChanged;

            foreach (var option in LanguageOptions)
            {
                option.PropertyChanged -= OnLanguageOptionPropertyChanged;
            }
        }

        base.Dispose(disposing);
    }
}
