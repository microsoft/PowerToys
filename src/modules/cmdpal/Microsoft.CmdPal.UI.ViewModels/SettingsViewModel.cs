// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class SettingsViewModel : INotifyPropertyChanged, IDisposable
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

    private readonly SettingsService _settingsService;
    private readonly TopLevelCommandManager _topLevelCommandManager;
    private readonly TaskScheduler _scheduler;

    private SettingsModel _settings;

    public event PropertyChangedEventHandler? PropertyChanged;

    public AppearanceSettingsViewModel Appearance { get; }

    public HotkeySettings? Hotkey
    {
        get => _settings.Hotkey;
        set
        {
            _settings = _settings with { Hotkey = value ?? SettingsModel.DefaultActivationShortcut };
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Hotkey)));
            Save();
        }
    }

    public bool UseLowLevelGlobalHotkey
    {
        get => _settings.UseLowLevelGlobalHotkey;
        set
        {
            _settings = _settings with { UseLowLevelGlobalHotkey = value };
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Hotkey)));
            Save();
        }
    }

    public bool AllowExternalReload
    {
        get => _settings.AllowExternalReload;
        set
        {
            _settings = _settings with { AllowExternalReload = value };
            Save();
        }
    }

    public bool ShowAppDetails
    {
        get => _settings.ShowAppDetails;
        set
        {
            _settings = _settings with { ShowAppDetails = value };
            Save();
        }
    }

    public bool BackspaceGoesBack
    {
        get => _settings.BackspaceGoesBack;
        set
        {
            _settings = _settings with { BackspaceGoesBack = value };
            Save();
        }
    }

    public bool SingleClickActivates
    {
        get => _settings.SingleClickActivates;
        set
        {
            _settings = _settings with { SingleClickActivates = value };
            Save();
        }
    }

    public bool HighlightSearchOnActivate
    {
        get => _settings.HighlightSearchOnActivate;
        set
        {
            _settings = _settings with { HighlightSearchOnActivate = value };
            Save();
        }
    }

    public bool KeepPreviousQuery
    {
        get => _settings.KeepPreviousQuery;
        set
        {
            _settings = _settings with { KeepPreviousQuery = value };
            Save();
        }
    }

    public int MonitorPositionIndex
    {
        get => (int)_settings.SummonOn;
        set
        {
            _settings = _settings with { SummonOn = (MonitorBehavior)value };
            Save();
        }
    }

    public bool ShowSystemTrayIcon
    {
        get => _settings.ShowSystemTrayIcon;
        set
        {
            _settings = _settings with { ShowSystemTrayIcon = value };
            Save();
        }
    }

    public bool IgnoreShortcutWhenFullscreen
    {
        get => _settings.IgnoreShortcutWhenFullscreen;
        set
        {
            _settings = _settings with { IgnoreShortcutWhenFullscreen = value };
            Save();
        }
    }

    public bool DisableAnimations
    {
        get => _settings.DisableAnimations;
        set
        {
            _settings = _settings with { DisableAnimations = value };
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
                _settings = _settings with { AutoGoHomeInterval = AutoGoHomeIntervals[value] };
            }

            Save();
        }
    }

    public int EscapeKeyBehaviorIndex
    {
        get => (int)_settings.EscapeKeyBehaviorSetting;
        set
        {
            _settings = _settings with { EscapeKeyBehaviorSetting = (EscapeKeyBehavior)value };
            Save();
        }
    }

    public ObservableCollection<ProviderSettingsViewModel> CommandProviders { get; } = new();

    public ObservableCollection<FallbackSettingsViewModel> FallbackRankings { get; set; } = new();

    public SettingsExtensionsViewModel Extensions { get; set; }

    public SettingsViewModel(
        SettingsService settingsService,
        TopLevelCommandManager topLevelCommandManager,
        TaskScheduler scheduler,
        IThemeService themeService)
    {
        _settingsService = settingsService;
        _settings = _settingsService.CurrentSettings;
        _settingsService.SettingsChanged += SettingsService_SettingsChanged;
        _scheduler = scheduler;
        _topLevelCommandManager = topLevelCommandManager;

        Appearance = new AppearanceSettingsViewModel(themeService, _settingsService);

        PopulateCommandAndFallbackSettings();

        Extensions = new SettingsExtensionsViewModel(CommandProviders, _scheduler);
    }

    private void PopulateCommandAndFallbackSettings()
    {
        var activeProviders = GetCommandProviders();
        var allProviderSettings = _settings.ProviderSettings;

        var fallbacks = new List<FallbackSettingsViewModel>();
        var currentRankings = _settings.FallbackRanks;
        var needsSave = false;

        foreach (var item in activeProviders)
        {
            var providerSettings = _settings.GetProviderSettings(item);

            var settingsModel = new ProviderSettingsViewModel(item, providerSettings, _settingsService);
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

        if (needsSave)
        {
            ApplyFallbackSort();
        }
    }

    private void SettingsService_SettingsChanged(SettingsModel sender, object? args)
    {
        _settings = sender;
        PopulateCommandAndFallbackSettings();
        Extensions = new SettingsExtensionsViewModel(CommandProviders, _scheduler);
    }

    private IEnumerable<CommandProviderWrapper> GetCommandProviders()
    {
        var allProviders = _topLevelCommandManager.CommandProviders;
        return allProviders;
    }

    public void ApplyFallbackSort()
    {
        _settings = _settings with { FallbackRanks = FallbackRankings.Select(s => s.Id).ToArray() };
        Save();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FallbackRankings)));
    }

    private void Save() => _settingsService.SaveSettings(_settings);

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _settingsService.SettingsChanged -= SettingsService_SettingsChanged;
    }
}
