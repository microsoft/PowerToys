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

    private readonly ISettingsService _settingsService;
    private readonly TopLevelCommandManager _topLevelCommandManager;

    public event PropertyChangedEventHandler? PropertyChanged;

    public AppearanceSettingsViewModel Appearance { get; }

    public DockAppearanceSettingsViewModel DockAppearance { get; }

    public HotkeySettings? Hotkey
    {
        get => _settingsService.Settings.Hotkey;
        set
        {
            _settingsService.Settings.Hotkey = value ?? SettingsModel.DefaultActivationShortcut;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Hotkey)));
            Save();
        }
    }

    public bool UseLowLevelGlobalHotkey
    {
        get => _settingsService.Settings.UseLowLevelGlobalHotkey;
        set
        {
            _settingsService.Settings.UseLowLevelGlobalHotkey = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Hotkey)));
            Save();
        }
    }

    public bool AllowExternalReload
    {
        get => _settingsService.Settings.AllowExternalReload;
        set
        {
            _settingsService.Settings.AllowExternalReload = value;
            Save();
        }
    }

    public bool ShowAppDetails
    {
        get => _settingsService.Settings.ShowAppDetails;
        set
        {
            _settingsService.Settings.ShowAppDetails = value;
            Save();
        }
    }

    public bool BackspaceGoesBack
    {
        get => _settingsService.Settings.BackspaceGoesBack;
        set
        {
            _settingsService.Settings.BackspaceGoesBack = value;
            Save();
        }
    }

    public bool SingleClickActivates
    {
        get => _settingsService.Settings.SingleClickActivates;
        set
        {
            _settingsService.Settings.SingleClickActivates = value;
            Save();
        }
    }

    public bool HighlightSearchOnActivate
    {
        get => _settingsService.Settings.HighlightSearchOnActivate;
        set
        {
            _settingsService.Settings.HighlightSearchOnActivate = value;
            Save();
        }
    }

    public bool KeepPreviousQuery
    {
        get => _settingsService.Settings.KeepPreviousQuery;
        set
        {
            _settingsService.Settings.KeepPreviousQuery = value;
            Save();
        }
    }

    public int MonitorPositionIndex
    {
        get => (int)_settingsService.Settings.SummonOn;
        set
        {
            _settingsService.Settings.SummonOn = (MonitorBehavior)value;
            Save();
        }
    }

    public bool ShowSystemTrayIcon
    {
        get => _settingsService.Settings.ShowSystemTrayIcon;
        set
        {
            _settingsService.Settings.ShowSystemTrayIcon = value;
            Save();
        }
    }

    public bool IgnoreShortcutWhenFullscreen
    {
        get => _settingsService.Settings.IgnoreShortcutWhenFullscreen;
        set
        {
            _settingsService.Settings.IgnoreShortcutWhenFullscreen = value;
            Save();
        }
    }

    public bool DisableAnimations
    {
        get => _settingsService.Settings.DisableAnimations;
        set
        {
            _settingsService.Settings.DisableAnimations = value;
            Save();
        }
    }

    public int AutoGoBackIntervalIndex
    {
        get
        {
            var index = AutoGoHomeIntervals.IndexOf(_settingsService.Settings.AutoGoHomeInterval);
            return index >= 0 ? index : 0;
        }

        set
        {
            if (value >= 0 && value < AutoGoHomeIntervals.Count)
            {
                _settingsService.Settings.AutoGoHomeInterval = AutoGoHomeIntervals[value];
            }

            Save();
        }
    }

    public int EscapeKeyBehaviorIndex
    {
        get => (int)_settingsService.Settings.EscapeKeyBehaviorSetting;
        set
        {
            _settingsService.Settings.EscapeKeyBehaviorSetting = (EscapeKeyBehavior)value;
            Save();
        }
    }

    public DockSide Dock_Side
    {
        get => _settingsService.Settings.DockSettings.Side;
        set
        {
            _settingsService.Settings.DockSettings.Side = value;
            Save();
        }
    }

    public DockSize Dock_DockSize
    {
        get => _settingsService.Settings.DockSettings.DockSize;
        set
        {
            _settingsService.Settings.DockSettings.DockSize = value;
            Save();
        }
    }

    public DockBackdrop Dock_Backdrop
    {
        get => _settingsService.Settings.DockSettings.Backdrop;
        set
        {
            _settingsService.Settings.DockSettings.Backdrop = value;
            Save();
        }
    }

    public bool Dock_ShowLabels
    {
        get => _settingsService.Settings.DockSettings.ShowLabels;
        set
        {
            _settingsService.Settings.DockSettings.ShowLabels = value;
            Save();
        }
    }

    public bool EnableDock
    {
        get => _settingsService.Settings.EnableDock;
        set
        {
            _settingsService.Settings.EnableDock = value;
            Save();
            WeakReferenceMessenger.Default.Send(new ShowHideDockMessage(value));
            WeakReferenceMessenger.Default.Send(new ReloadCommandsMessage()); // TODO! we need to update the MoreCommands of all top level items, but we don't _really_ want to reload
        }
    }

    public ObservableCollection<ProviderSettingsViewModel> CommandProviders { get; } = new();

    public ObservableCollection<FallbackSettingsViewModel> FallbackRankings { get; set; } = new();

    public SettingsExtensionsViewModel Extensions { get; }

    public SettingsViewModel(TopLevelCommandManager topLevelCommandManager, TaskScheduler scheduler, IThemeService themeService, ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _topLevelCommandManager = topLevelCommandManager;

        Appearance = new AppearanceSettingsViewModel(themeService, settingsService);
        DockAppearance = new DockAppearanceSettingsViewModel(themeService, settingsService);

        var activeProviders = GetCommandProviders();
        var allProviderSettings = _settingsService.Settings.ProviderSettings;

        var fallbacks = new List<FallbackSettingsViewModel>();
        var currentRankings = _settingsService.Settings.FallbackRanks;
        var needsSave = false;

        foreach (var item in activeProviders)
        {
            var providerSettings = _settingsService.Settings.GetProviderSettings(item);

            var settingsModel = new ProviderSettingsViewModel(item, providerSettings, settingsService);
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

    private IEnumerable<CommandProviderWrapper> GetCommandProviders()
    {
        var allProviders = _topLevelCommandManager.CommandProviders;
        return allProviders;
    }

    public void ApplyFallbackSort()
    {
        _settingsService.Settings.FallbackRanks = FallbackRankings.Select(s => s.Id).ToArray();
        Save();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FallbackRankings)));
    }

    private void Save() => _settingsService.Save();
}
