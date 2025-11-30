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

    private readonly ScreencastModeSettings screencastModeSettings;
    private readonly ISettingsUtils settingsUtils;
    private readonly ISettingsRepository<GeneralSettings> settingsRepository;

    private Func<string, int> SendConfigMSG { get; }

    public ScreencastModeViewModel(
        ISettingsUtils settingsUtils,
        ISettingsRepository<GeneralSettings> settingsRepository,
        Func<string, int> ipcMSGCallBackFunc)
    {
        ArgumentNullException.ThrowIfNull(settingsRepository);

        this.settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));
        this.settingsRepository = settingsRepository;
        this.GeneralSettingsConfig = settingsRepository.SettingsConfig;

        // Load or create ScreencastMode settings
        this.screencastModeSettings = this.settingsUtils.GetSettingsOrDefault<ScreencastModeSettings>(ScreencastModeSettings.ModuleName);

        this.SendConfigMSG = ipcMSGCallBackFunc;
    }

    public bool IsEnabled
    {
        get => this.GeneralSettingsConfig.Enabled.ScreencastMode;
        set
        {
            if (this.GeneralSettingsConfig.Enabled.ScreencastMode != value)
            {
                this.GeneralSettingsConfig.Enabled.ScreencastMode = value;
                OnPropertyChanged(nameof(this.IsEnabled));

                // Notify the runner about the state change
                OutGoingGeneralSettings outgoing = new OutGoingGeneralSettings(this.GeneralSettingsConfig);
                SendConfigMSG(outgoing.ToString());

                // Also save settings
                this.settingsRepository.SettingsConfig = this.GeneralSettingsConfig;
            }
        }
    }

    public HotkeySettings ScreencastModeShortcut
    {
        get => this.screencastModeSettings.Properties.ScreencastModeShortcut;
        set
        {
            if (this.screencastModeSettings.Properties.ScreencastModeShortcut != value)
            {
                this.screencastModeSettings.Properties.ScreencastModeShortcut = value;
                OnPropertyChanged(nameof(this.ScreencastModeShortcut));

                this.SaveAndNotifySettings();
            }
        }
    }

    public string DisplayPosition
    {
        get => this.screencastModeSettings.Properties.DisplayPosition.Value;
        set
        {
            if (this.screencastModeSettings.Properties.DisplayPosition.Value != value)
            {
                this.screencastModeSettings.Properties.DisplayPosition.Value = value;
                OnPropertyChanged(nameof(this.DisplayPosition));

                this.SaveAndNotifySettings();
            }
        }
    }

    public string TextColor
    {
        get => this.screencastModeSettings.Properties.TextColor.Value;
        set
        {
            if (this.screencastModeSettings.Properties.TextColor.Value != value)
            {
                this.screencastModeSettings.Properties.TextColor.Value = value;
                OnPropertyChanged(nameof(this.TextColor));
                this.SaveAndNotifySettings();
            }
        }
    }

    public string BackgroundColor
    {
        get => this.screencastModeSettings.Properties.BackgroundColor.Value;
        set
        {
            if (this.screencastModeSettings.Properties.BackgroundColor.Value != value)
            {
                this.screencastModeSettings.Properties.BackgroundColor.Value = value;
                OnPropertyChanged(nameof(this.BackgroundColor));

                this.SaveAndNotifySettings();
            }
        }
    }

    public void RefreshEnabledState()
    {
        OnPropertyChanged(nameof(this.IsEnabled));
    }

    private void SaveAndNotifySettings()
    {
        this.settingsUtils.SaveSettings(this.screencastModeSettings.ToJsonString(), ScreencastModeSettings.ModuleName);
        this.NotifySettingsChanged();
    }

    private void NotifySettingsChanged()
    {
        // Using InvariantCulture as this is an IPC message
        SendConfigMSG(
            string.Format(
                CultureInfo.InvariantCulture,
                "{{ \"powertoys\": {{ \"{0}\": {1} }} }}",
                ScreencastModeSettings.ModuleName,
                JsonSerializer.Serialize(this.screencastModeSettings)));
    }
}
