// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.Extensions.DependencyInjection;

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
    private readonly IServiceProvider _serviceProvider;

    public event PropertyChangedEventHandler? PropertyChanged;

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

    public ObservableCollection<ProviderSettingsViewModel> CommandProviders { get; } = [];

    public SettingsExtensionsViewModel Extensions { get; }

    public SettingsViewModel(SettingsModel settings, IServiceProvider serviceProvider, TaskScheduler scheduler)
    {
        _settings = settings;
        _serviceProvider = serviceProvider;

        var activeProviders = GetCommandProviders();
        var allProviderSettings = _settings.ProviderSettings;

        foreach (var item in activeProviders)
        {
            var providerSettings = settings.GetProviderSettings(item);

            var settingsModel = new ProviderSettingsViewModel(item, providerSettings, _serviceProvider);
            CommandProviders.Add(settingsModel);
        }

        Extensions = new SettingsExtensionsViewModel(CommandProviders, scheduler);
    }

    private IEnumerable<CommandProviderWrapper> GetCommandProviders()
    {
        var manager = _serviceProvider.GetService<TopLevelCommandManager>()!;
        var allProviders = manager.CommandProviders;
        return allProviders;
    }

    private void Save() => SettingsModel.SaveSettings(_settings);
}
