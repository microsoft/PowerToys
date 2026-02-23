// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class SettingsViewModel : INotifyPropertyChanged
{
    private static readonly List<TimeSpan> AutoGoHomeIntervals =
    [
        Timeout.InfiniteTimeSpan,
        TimeSpan.Zero,
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(20),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(60),
        TimeSpan.FromSeconds(90),
        TimeSpan.FromSeconds(120),
        TimeSpan.FromSeconds(180),
    ];

    private readonly SettingsModel _settings;
    private readonly TopLevelCommandManager _topLevelCommandManager;
    private readonly ILanguageService _languageService;

    private int _languageIndex;
    private int _initialLanguageIndex;
    private bool _languageInitialized;

    public event PropertyChangedEventHandler? PropertyChanged;

    public event Action? RestartRequested;

    public List<LanguageItem> Languages { get; private set; } = [];

    public int LanguageIndex
    {
        get => _languageIndex;
        set
        {
            if (_languageIndex == value)
            {
                return;
            }

            _languageIndex = value;
            if (Languages.Count > 0 && value >= 0 && value < Languages.Count)
            {
                _settings.Language = Languages[value].Tag;
                Save();
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LanguageIndex)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LanguageChanged)));

            if (_languageInitialized && LanguageChanged)
            {
                RestartRequested?.Invoke();
            }
        }
    }

    public bool LanguageChanged
    {
        get
        {
            if (Languages.Count == 0)
            {
                return false;
            }

            var initialLanguage = _initialLanguageIndex >= 0 && _initialLanguageIndex < Languages.Count
                ? Languages[_initialLanguageIndex].Tag : string.Empty;
            var currentLanguage = _languageIndex >= 0 && _languageIndex < Languages.Count
                ? Languages[_languageIndex].Tag : string.Empty;

            return !string.Equals(
                _languageService.GetEffectiveLanguageTag(initialLanguage),
                _languageService.GetEffectiveLanguageTag(currentLanguage),
                StringComparison.OrdinalIgnoreCase);
        }
    }

    public AppearanceSettingsViewModel Appearance { get; }

    public HotkeySettings? Hotkey
    {
        get => _settings.Hotkey;
        set
        {
            _settings.Hotkey = value ?? SettingsModel.DefaultActivationShortcut;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Hotkey)));
            Save();
        }
    }

    public bool UseLowLevelGlobalHotkey
    {
        get => _settings.UseLowLevelGlobalHotkey;
        set
        {
            _settings.UseLowLevelGlobalHotkey = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Hotkey)));
            Save();
        }
    }

    public bool AllowExternalReload
    {
        get => _settings.AllowExternalReload;
        set
        {
            _settings.AllowExternalReload = value;
            Save();
        }
    }

    public bool ShowAppDetails
    {
        get => _settings.ShowAppDetails;
        set
        {
            _settings.ShowAppDetails = value;
            Save();
        }
    }

    public bool BackspaceGoesBack
    {
        get => _settings.BackspaceGoesBack;
        set
        {
            _settings.BackspaceGoesBack = value;
            Save();
        }
    }

    public bool SingleClickActivates
    {
        get => _settings.SingleClickActivates;
        set
        {
            _settings.SingleClickActivates = value;
            Save();
        }
    }

    public bool HighlightSearchOnActivate
    {
        get => _settings.HighlightSearchOnActivate;
        set
        {
            _settings.HighlightSearchOnActivate = value;
            Save();
        }
    }

    public int MonitorPositionIndex
    {
        get => (int)_settings.SummonOn;
        set
        {
            _settings.SummonOn = (MonitorBehavior)value;
            Save();
        }
    }

    public bool ShowSystemTrayIcon
    {
        get => _settings.ShowSystemTrayIcon;
        set
        {
            _settings.ShowSystemTrayIcon = value;
            Save();
        }
    }

    public bool IgnoreShortcutWhenFullscreen
    {
        get => _settings.IgnoreShortcutWhenFullscreen;
        set
        {
            _settings.IgnoreShortcutWhenFullscreen = value;
            Save();
        }
    }

    public bool DisableAnimations
    {
        get => _settings.DisableAnimations;
        set
        {
            _settings.DisableAnimations = value;
            Save();
        }
    }

    public int AutoGoBackIntervalIndex
    {
        get
        {
            var index = AutoGoHomeIntervals.IndexOf(_settings.AutoGoHomeInterval);
            return index >= 0 ? index : 0;
        }

        set
        {
            if (value >= 0 && value < AutoGoHomeIntervals.Count)
            {
                _settings.AutoGoHomeInterval = AutoGoHomeIntervals[value];
            }

            Save();
        }
    }

    public int EscapeKeyBehaviorIndex
    {
        get => (int)_settings.EscapeKeyBehaviorSetting;
        set
        {
            _settings.EscapeKeyBehaviorSetting = (EscapeKeyBehavior)value;
            Save();
        }
    }

    public ObservableCollection<ProviderSettingsViewModel> CommandProviders { get; } = new();

    public ObservableCollection<FallbackSettingsViewModel> FallbackRankings { get; set; } = new();

    public SettingsExtensionsViewModel Extensions { get; }

    public SettingsViewModel(SettingsModel settings, TopLevelCommandManager topLevelCommandManager, TaskScheduler scheduler, IThemeService themeService, ILanguageService languageService)
    {
        _settings = settings;
        _topLevelCommandManager = topLevelCommandManager;
        _languageService = languageService;

        InitializeLanguages(languageService);

        Appearance = new AppearanceSettingsViewModel(themeService, _settings);

        var activeProviders = GetCommandProviders();
        var allProviderSettings = _settings.ProviderSettings;

        var fallbacks = new List<FallbackSettingsViewModel>();
        var currentRankings = _settings.FallbackRanks;
        var needsSave = false;

        foreach (var item in activeProviders)
        {
            var providerSettings = settings.GetProviderSettings(item);

            var settingsModel = new ProviderSettingsViewModel(item, providerSettings, _settings);
            CommandProviders.Add(settingsModel);

            fallbacks.AddRange(settingsModel.FallbackCommands);
        }

        var fallbackRankings = new List<Scored<FallbackSettingsViewModel>>(fallbacks.Count);
        foreach (var fallback in fallbacks)
        {
            var index = currentRankings.IndexOf(fallback.Id);
            var score = fallbacks.Count;

            if (index >= 0)
            {
                score = index;
            }

            fallbackRankings.Add(new Scored<FallbackSettingsViewModel>() { Item = fallback, Score = score });

            if (index == -1)
            {
                needsSave = true;
            }
        }

        FallbackRankings = new ObservableCollection<FallbackSettingsViewModel>(fallbackRankings.OrderBy(o => o.Score).Select(fr => fr.Item));
        Extensions = new SettingsExtensionsViewModel(CommandProviders, scheduler);

        if (needsSave)
        {
            ApplyFallbackSort();
        }
    }

    private void InitializeLanguages(ILanguageService languageService)
    {
        var defaultItem = new LanguageItem(string.Empty, Properties.Resources.Language_Default);

        var sorted = new List<LanguageItem>();
        foreach (var tag in languageService.AvailableLanguages)
        {
            try
            {
                var culture = new CultureInfo(tag);
                sorted.Add(new LanguageItem(tag, culture.DisplayName));
            }
            catch (CultureNotFoundException ex)
            {
                Logger.LogError($"Culture '{tag}' not found", ex);
            }
        }

        sorted.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
        sorted.Insert(0, defaultItem);
        Languages = sorted;

        var currentLang = _settings.Language ?? string.Empty;
        _initialLanguageIndex = Languages.FindIndex(l => l.Tag.Equals(currentLang, StringComparison.OrdinalIgnoreCase));
        if (_initialLanguageIndex < 0)
        {
            _initialLanguageIndex = 0;
        }

        _languageIndex = _initialLanguageIndex;
        _languageInitialized = true;
    }

    private IEnumerable<CommandProviderWrapper> GetCommandProviders()
    {
        var allProviders = _topLevelCommandManager.CommandProviders;
        return allProviders;
    }

    public void ApplyFallbackSort()
    {
        _settings.FallbackRanks = FallbackRankings.Select(s => s.Id).ToArray();
        Save();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FallbackRankings)));
    }

    private void Save() => SettingsModel.SaveSettings(_settings);
}
