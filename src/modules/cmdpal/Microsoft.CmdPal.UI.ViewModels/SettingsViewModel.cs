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

    public ObservableCollection<ProviderSettingsViewModel> CommandProviders { get; } = [];

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
    }

    private IEnumerable<CommandProviderWrapper> GetCommandProviders()
    {
        var manager = _serviceProvider.GetService<TopLevelCommandManager>()!;
        var allProviders = manager.CommandProviders;
        return allProviders;
    }

    private void Save() => SettingsModel.SaveSettings(_settings);
}
