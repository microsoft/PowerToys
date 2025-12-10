// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using Microsoft.CommandPalette.UI.Models;
using Microsoft.CommandPalette.UI.Services;

namespace Microsoft.CommandPalette.UI.ViewModels.Settings;

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
    private SettingsModel _settingsModel;

    public event PropertyChangedEventHandler? PropertyChanged;

    public HotkeySettings? Hotkey
    {
        get => _settingsModel.Hotkey;
        set
        {
            _settingsModel.Hotkey = value ?? SettingsModel.DefaultActivationShortcut;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Hotkey)));
            Save();
        }
    }

    public bool UseLowLevelGlobalHotkey
    {
        get => _settingsModel.UseLowLevelGlobalHotkey;
        set
        {
            _settingsModel.UseLowLevelGlobalHotkey = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Hotkey)));
            Save();
        }
    }

    public bool AllowExternalReload
    {
        get => _settingsModel.AllowExternalReload;
        set
        {
            _settingsModel.AllowExternalReload = value;
            Save();
        }
    }

    public bool ShowAppDetails
    {
        get => _settingsModel.ShowAppDetails;
        set
        {
            _settingsModel.ShowAppDetails = value;
            Save();
        }
    }

    public bool BackspaceGoesBack
    {
        get => _settingsModel.BackspaceGoesBack;
        set
        {
            _settingsModel.BackspaceGoesBack = value;
            Save();
        }
    }

    public bool SingleClickActivates
    {
        get => _settingsModel.SingleClickActivates;
        set
        {
            _settingsModel.SingleClickActivates = value;
            Save();
        }
    }

    public bool HighlightSearchOnActivate
    {
        get => _settingsModel.HighlightSearchOnActivate;
        set
        {
            _settingsModel.HighlightSearchOnActivate = value;
            Save();
        }
    }

    public int MonitorPositionIndex
    {
        get => (int)_settingsModel.SummonOn;
        set
        {
            _settingsModel.SummonOn = (MonitorBehavior)value;
            Save();
        }
    }

    public bool ShowSystemTrayIcon
    {
        get => _settingsModel.ShowSystemTrayIcon;
        set
        {
            _settingsModel.ShowSystemTrayIcon = value;
            Save();
        }
    }

    public bool IgnoreShortcutWhenFullscreen
    {
        get => _settingsModel.IgnoreShortcutWhenFullscreen;
        set
        {
            _settingsModel.IgnoreShortcutWhenFullscreen = value;
            Save();
        }
    }

    public bool DisableAnimations
    {
        get => _settingsModel.DisableAnimations;
        set
        {
            _settingsModel.DisableAnimations = value;
            Save();
        }
    }

    public int AutoGoBackIntervalIndex
    {
        get
        {
            var index = AutoGoHomeIntervals.IndexOf(_settingsModel.AutoGoHomeInterval);
            return index >= 0 ? index : 0;
        }

        set
        {
            if (value >= 0 && value < AutoGoHomeIntervals.Count)
            {
                _settingsModel.AutoGoHomeInterval = AutoGoHomeIntervals[value];
            }

            Save();
        }
    }

    // public ObservableCollection<ProviderSettingsViewModel> CommandProviders { get; } = [];
    // public SettingsExtensionsViewModel Extensions { get; }
    public SettingsViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _settingsModel = _settingsService.CurrentSettings;

        _settingsService.SettingsChanged += OnSettingsChanged;

        // var activeProviders = GetCommandProviders();
        // var allProviderSettings = _settings.ProviderSettings;
        // foreach (var item in activeProviders)
        // {
        //    var providerSettings = settings.GetProviderSettings(item);
        //    var settingsModel = new ProviderSettingsViewModel(item, providerSettings, _serviceProvider);
        //    CommandProviders.Add(settingsModel);
        // }
        // Extensions = new SettingsExtensionsViewModel(CommandProviders, scheduler);
    }

    private void OnSettingsChanged(SettingsModel sender, object? args)
    {
        _settingsModel = sender;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    // private IEnumerable<CommandProviderWrapper> GetCommandProviders()
    // {
    //    var manager = _serviceProvider.GetService<TopLevelCommandManager>()!;
    //    var allProviders = manager.CommandProviders;
    //    return allProviders;
    // }
    private void Save() => _settingsService.SaveSettings(_settingsModel);

    public void Dispose()
    {
        if (_settingsService is not null)
        {
            _settingsService.SettingsChanged -= OnSettingsChanged;
        }

        GC.SuppressFinalize(this);
    }
}
