// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Messages;
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

    private SettingsModel _settings;

    public event PropertyChangedEventHandler? PropertyChanged;

    public AppearanceSettingsViewModel Appearance { get; }

    public DockAppearanceSettingsViewModel DockAppearance { get; }

    public HotkeySettings? Hotkey
    {
        get => _settings.Hotkey;
        set
        {
            Save(_settings with { Hotkey = value ?? SettingsModel.DefaultActivationShortcut });
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Hotkey)));
        }
    }

    public bool UseLowLevelGlobalHotkey
    {
        get => _settings.UseLowLevelGlobalHotkey;
        set
        {
            Save(_settings with { UseLowLevelGlobalHotkey = value });
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Hotkey)));
        }
    }

    public bool AllowExternalReload
    {
        get => _settings.AllowExternalReload;
        set
        {
            Save(_settings with { AllowExternalReload = value });
        }
    }

    public bool ShowAppDetails
    {
        get => _settings.ShowAppDetails;
        set
        {
            Save(_settings with { ShowAppDetails = value });
        }
    }

    public bool BackspaceGoesBack
    {
        get => _settings.BackspaceGoesBack;
        set
        {
            Save(_settings with { BackspaceGoesBack = value });
        }
    }

    public bool SingleClickActivates
    {
        get => _settings.SingleClickActivates;
        set
        {
            Save(_settings with { SingleClickActivates = value });
        }
    }

    public bool HighlightSearchOnActivate
    {
        get => _settings.HighlightSearchOnActivate;
        set
        {
            Save(_settings with { HighlightSearchOnActivate = value });
        }
    }

    public bool KeepPreviousQuery
    {
        get => _settings.KeepPreviousQuery;
        set
        {
            Save(_settings with { KeepPreviousQuery = value });
        }
    }

    public int MonitorPositionIndex
    {
        get => (int)_settings.SummonOn;
        set
        {
            Save(_settings with { SummonOn = (MonitorBehavior)value });
        }
    }

    public bool ShowSystemTrayIcon
    {
        get => _settings.ShowSystemTrayIcon;
        set
        {
            Save(_settings with { ShowSystemTrayIcon = value });
        }
    }

    public bool IgnoreShortcutWhenFullscreen
    {
        get => _settings.IgnoreShortcutWhenFullscreen;
        set
        {
            Save(_settings with { IgnoreShortcutWhenFullscreen = value });
        }
    }

    public bool DisableAnimations
    {
        get => _settings.DisableAnimations;
        set
        {
            Save(_settings with { DisableAnimations = value });
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
                Save(_settings with { AutoGoHomeInterval = AutoGoHomeIntervals[value] });
            }
        }
    }

    public int EscapeKeyBehaviorIndex
    {
        get => (int)_settings.EscapeKeyBehaviorSetting;
        set
        {
            Save(_settings with { EscapeKeyBehaviorSetting = (EscapeKeyBehavior)value });
        }
    }

    public DockSide Dock_Side
    {
        get => _settings.DockSettings.Side;
        set
        {
            var dockSettings = _settings.DockSettings with { Side = value };
            Save(_settings with { DockSettings = dockSettings });
        }
    }

    public DockSize Dock_DockSize
    {
        get => _settings.DockSettings.DockSize;
        set
        {
            var dockSettings = _settings.DockSettings with { DockSize = value };
            Save(_settings with { DockSettings = dockSettings });
        }
    }

    public DockBackdrop Dock_Backdrop
    {
        get => _settings.DockSettings.Backdrop;
        set
        {
            var dockSettings = _settings.DockSettings with { Backdrop = value };
            Save(_settings with { DockSettings = dockSettings });
        }
    }

    public bool Dock_ShowLabels
    {
        get => _settings.DockSettings.ShowLabels;
        set
        {
            var dockSettings = _settings.DockSettings with { ShowLabels = value };
            Save(_settings with { DockSettings = dockSettings });
        }
    }

    public bool EnableDock
    {
        get => _settings.EnableDock;
        set
        {
            Save(_settings with { EnableDock = value });
            WeakReferenceMessenger.Default.Send(new ShowHideDockMessage(value));
            WeakReferenceMessenger.Default.Send(new ReloadCommandsMessage()); // TODO! we need to update the MoreCommands of all top level items, but we don't _really_ want to reload
        }
    }

    public ObservableCollection<ProviderSettingsViewModel> CommandProviders { get; } = new();

    public ObservableCollection<FallbackSettingsViewModel> FallbackRankings { get; set; } = new();

    public SettingsExtensionsViewModel Extensions { get; }

    public SettingsViewModel(
        SettingsService settingsService,
        TopLevelCommandManager topLevelCommandManager,
        TaskScheduler scheduler,
        IThemeService themeService)
    {
        _settingsService = settingsService;
        _settings = _settingsService.CurrentSettings;
        _topLevelCommandManager = topLevelCommandManager;

        _settingsService.SettingsChanged += SettingsService_SettingsChanged;

        Appearance = new AppearanceSettingsViewModel(themeService, _settingsService);
        DockAppearance = new DockAppearanceSettingsViewModel(themeService, _settingsService);

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
        Extensions = new SettingsExtensionsViewModel(CommandProviders, scheduler);

        if (needsSave)
        {
            ApplyFallbackSort();
        }
    }

    private void SettingsService_SettingsChanged(SettingsService sender, SettingsChangedEventArgs args)
    {
        _settings = args.NewSettingsModel;
    }

    private IEnumerable<CommandProviderWrapper> GetCommandProviders()
    {
        var allProviders = _topLevelCommandManager.CommandProviders;
        return allProviders;
    }

    public void ApplyFallbackSort()
    {
        Save(_settings with { FallbackRanks = FallbackRankings.Select(s => s.Id).ToArray() });
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FallbackRankings)));
    }

    private void Save(SettingsModel settings)
    {
        _settings = settings;
        _settingsService.SaveSettings(settings, true);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _settingsService.SettingsChanged -= SettingsService_SettingsChanged;
    }
}
