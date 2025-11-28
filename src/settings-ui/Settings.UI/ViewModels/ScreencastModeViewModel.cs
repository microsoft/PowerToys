// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Globalization;
using System.Text.Json;
using global::PowerToys.GPOWrapper;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.ViewModels;

public partial class ScreencastModeViewModel : Observable
{
    private GeneralSettings GeneralSettingsConfig { get; set; }

    private readonly ScreencastModeSettings _screencastModeSettings;
    private readonly ISettingsUtils _settingsUtils;
    private readonly ISettingsRepository<GeneralSettings> _settingsRepository;

    private Func<string, int> SendConfigMSG { get; }

    public ScreencastModeViewModel(
        ISettingsUtils settingsUtils,
        ISettingsRepository<GeneralSettings> settingsRepository,
        Func<string, int> ipcMSGCallBackFunc)
    {
        ArgumentNullException.ThrowIfNull(settingsRepository);

        _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));
        _settingsRepository = settingsRepository;
        GeneralSettingsConfig = settingsRepository.SettingsConfig;

        // Load or create ScreencastMode settings
        _screencastModeSettings = _settingsUtils.GetSettingsOrDefault<ScreencastModeSettings>(ScreencastModeSettings.ModuleName);

        SendConfigMSG = ipcMSGCallBackFunc;
    }

    public bool IsEnabled
    {
        get => GeneralSettingsConfig.Enabled.ScreencastMode;
        set
        {
            if (GeneralSettingsConfig.Enabled.ScreencastMode != value)
            {
                GeneralSettingsConfig.Enabled.ScreencastMode = value;
                OnPropertyChanged(nameof(IsEnabled));

                // Notify the runner about the state change
                OutGoingGeneralSettings outgoing = new OutGoingGeneralSettings(GeneralSettingsConfig);
                SendConfigMSG(outgoing.ToString());

                // Also save settings
                _settingsRepository.SettingsConfig = GeneralSettingsConfig;
            }
        }
    }

    public HotkeySettings ScreencastModeShortcut
    {
        get => _screencastModeSettings.Properties.ScreencastModeShortcut;
        set
        {
            if (_screencastModeSettings.Properties.ScreencastModeShortcut != value)
            {
                _screencastModeSettings.Properties.ScreencastModeShortcut = value;
                OnPropertyChanged(nameof(ScreencastModeShortcut));

                SaveAndNotifySettings();
            }
        }
    }

    public string DisplayPosition
    {
        get => _screencastModeSettings.Properties.DisplayPosition.Value;
        set
        {
            if (_screencastModeSettings.Properties.DisplayPosition.Value != value)
            {
                _screencastModeSettings.Properties.DisplayPosition.Value = value;
                OnPropertyChanged(nameof(DisplayPosition));

                SaveAndNotifySettings();
            }
        }
    }

    public string TextColor
    {
        get => _screencastModeSettings.Properties.TextColor.Value;
        set
        {
            if (_screencastModeSettings.Properties.TextColor.Value != value)
            {
                _screencastModeSettings.Properties.TextColor.Value = value;
                OnPropertyChanged(nameof(TextColor));
                SaveAndNotifySettings();
            }
        }
    }

    public string BackgroundColor
    {
        get => _screencastModeSettings.Properties.BackgroundColor.Value;
        set
        {
            if (_screencastModeSettings.Properties.BackgroundColor.Value != value)
            {
                _screencastModeSettings.Properties.BackgroundColor.Value = value;
                OnPropertyChanged(nameof(BackgroundColor));

                SaveAndNotifySettings();
            }
        }
    }

    private void SaveAndNotifySettings()
    {
        _settingsUtils.SaveSettings(_screencastModeSettings.ToJsonString(), ScreencastModeSettings.ModuleName);
        NotifySettingsChanged();
    }

    private void NotifySettingsChanged()
    {
        // Using InvariantCulture as this is an IPC message
        SendConfigMSG(
            string.Format(
                CultureInfo.InvariantCulture,
                "{{ \"powertoys\": {{ \"{0}\": {1} }} }}",
                ScreencastModeSettings.ModuleName,
                JsonSerializer.Serialize(_screencastModeSettings)));
    }

    public void RefreshEnabledState()
    {
        OnPropertyChanged(nameof(IsEnabled));
    }
}
