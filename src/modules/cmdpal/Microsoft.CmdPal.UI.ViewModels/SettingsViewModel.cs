// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class SettingsViewModel
    : INotifyPropertyChanged,
    IRecipient<ReloadFinishedMessage>,
    IDisposable
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
    private bool _disposed;

    private SettingsModel Settings => _settingsService.CurrentSettings;

    public event PropertyChangedEventHandler? PropertyChanged;

    public AppearanceSettingsViewModel Appearance { get; }

    public HotkeySettings? Hotkey
    {
        get => Settings.Hotkey;
        set
        {
            Settings.Hotkey = value ?? SettingsModel.DefaultActivationShortcut;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Hotkey)));
            Save();
        }
    }

    public bool UseLowLevelGlobalHotkey
    {
        get => Settings.UseLowLevelGlobalHotkey;
        set
        {
            Settings.UseLowLevelGlobalHotkey = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Hotkey)));
            Save();
        }
    }

    public bool AllowExternalReload
    {
        get => Settings.AllowExternalReload;
        set
        {
            Settings.AllowExternalReload = value;
            Save();
        }
    }

    public bool ShowAppDetails
    {
        get => Settings.ShowAppDetails;
        set
        {
            Settings.ShowAppDetails = value;
            Save();
        }
    }

    public bool BackspaceGoesBack
    {
        get => Settings.BackspaceGoesBack;
        set
        {
            Settings.BackspaceGoesBack = value;
            Save();
        }
    }

    public bool SingleClickActivates
    {
        get => Settings.SingleClickActivates;
        set
        {
            Settings.SingleClickActivates = value;
            Save();
        }
    }

    public bool HighlightSearchOnActivate
    {
        get => Settings.HighlightSearchOnActivate;
        set
        {
            Settings.HighlightSearchOnActivate = value;
            Save();
        }
    }

    public int MonitorPositionIndex
    {
        get => (int)Settings.SummonOn;
        set
        {
            Settings.SummonOn = (MonitorBehavior)value;
            Save();
        }
    }

    public bool ShowSystemTrayIcon
    {
        get => Settings.ShowSystemTrayIcon;
        set
        {
            Settings.ShowSystemTrayIcon = value;
            Save();
        }
    }

    public bool IgnoreShortcutWhenFullscreen
    {
        get => Settings.IgnoreShortcutWhenFullscreen;
        set
        {
            Settings.IgnoreShortcutWhenFullscreen = value;
            Save();
        }
    }

    public bool DisableAnimations
    {
        get => Settings.DisableAnimations;
        set
        {
            Settings.DisableAnimations = value;
            Save();
        }
    }

    public int AutoGoBackIntervalIndex
    {
        get
        {
            var index = AutoGoHomeIntervals.IndexOf(Settings.AutoGoHomeInterval);
            return index >= 0 ? index : 0;
        }

        set
        {
            if (value >= 0 && value < AutoGoHomeIntervals.Count)
            {
                Settings.AutoGoHomeInterval = AutoGoHomeIntervals[value];
            }

            Save();
        }
    }

    public int EscapeKeyBehaviorIndex
    {
        get => (int)Settings.EscapeKeyBehaviorSetting;
        set
        {
            Settings.EscapeKeyBehaviorSetting = (EscapeKeyBehavior)value;
            Save();
        }
    }

    public ObservableCollection<ProviderSettingsViewModel> CommandProviders { get; } = new();

    public ObservableCollection<FallbackSettingsViewModel> FallbackRankings { get; set; } = new();

    public SettingsExtensionsViewModel Extensions { get; private set; }

    public SettingsViewModel(
        SettingsService settingsService,
        TopLevelCommandManager topLevelCommandManager,
        TaskScheduler scheduler,
        IThemeService themeService)
    {
        _settingsService = settingsService;
        _topLevelCommandManager = topLevelCommandManager;
        _scheduler = scheduler;

        Appearance = new AppearanceSettingsViewModel(themeService, _settingsService);

        Extensions = new SettingsExtensionsViewModel(CommandProviders, _scheduler);
        LoadProvidersAndCommands();

        WeakReferenceMessenger.Default.Register<ReloadFinishedMessage>(this);
    }

    public void Receive(ReloadFinishedMessage message)
    {
        LoadProvidersAndCommands();
    }

    private void LoadProvidersAndCommands()
    {
        var activeProviders = GetCommandProviders();
        var allProviderSettings = Settings.ProviderSettings;

        var fallbacks = new List<FallbackSettingsViewModel>();
        var currentRankings = Settings.FallbackRanks;
        var needsSave = false;

        foreach (var item in activeProviders)
        {
            var providerSettings = Settings.GetProviderSettings(item);

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
        Extensions = new SettingsExtensionsViewModel(CommandProviders, _scheduler);

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
        Settings.FallbackRanks = FallbackRankings.Select(s => s.Id).ToArray();
        Save();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FallbackRankings)));
    }

    private void Save() => _settingsService.SaveSettings(Settings);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Appearance.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
