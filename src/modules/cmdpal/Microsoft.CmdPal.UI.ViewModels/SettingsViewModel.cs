// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.CmdPal.UI.ViewModels.Settings;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class SettingsViewModel : INotifyPropertyChanged
{
    private readonly SettingsModel _settings;
    private readonly TopLevelCommandManager _topLevelCommandManager;

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

    public bool HotkeyGoesHome
    {
        get => _settings.HotkeyGoesHome;
        set
        {
            _settings.HotkeyGoesHome = value;
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

    public ObservableCollection<ProviderSettingsViewModel> CommandProviders { get; } = [];

    public ObservableCollection<string> FallbackRanks { get; } = [];

    public SettingsExtensionsViewModel Extensions { get; }

    public SettingsViewModel(SettingsModel settings, TopLevelCommandManager topLevelCommandManager, TaskScheduler scheduler)
    {
        _settings = settings;
        _topLevelCommandManager = topLevelCommandManager;

        var activeProviders = GetCommandProviders();
        var allProviderSettings = _settings.ProviderSettings;

        foreach (var item in activeProviders)
        {
            var providerSettings = settings.GetProviderSettings(item);

            var settingsModel = new ProviderSettingsViewModel(item, providerSettings, _settings);
            CommandProviders.Add(settingsModel);
        }

        FallbackRanks = new ObservableCollection<string>(_settings.FallbackRanks);

        Extensions = new SettingsExtensionsViewModel(CommandProviders, scheduler);
    }

    private IEnumerable<CommandProviderWrapper> GetCommandProviders()
    {
        var allProviders = _topLevelCommandManager.CommandProviders;
        return allProviders;
    }

    private void Save() => SettingsModel.SaveSettings(_settings);
}
