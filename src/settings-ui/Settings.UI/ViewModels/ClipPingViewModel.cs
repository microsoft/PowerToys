// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Enumerations;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using PowerToys.GPOWrapper;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class ClipPingViewModel : Observable
    {
        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private bool _enabledStateIsGpoConfigured;
        private bool _isEnabled;

        public ClipPingViewModel(ISettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, ISettingsRepository<ClipPingSettings> moduleSettingsRepository, Func<string, int> ipcMSGCallBackFunc)
        {
            ArgumentNullException.ThrowIfNull(settingsUtils);

            SettingsUtils = settingsUtils;

            // To obtain the general settings configurations of PowerToys Settings.
            ArgumentNullException.ThrowIfNull(settingsRepository);

            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            InitializeEnabledValue();

            // To obtain the settings configurations of ClipPing.
            ArgumentNullException.ThrowIfNull(moduleSettingsRepository);

            Settings = moduleSettingsRepository.SettingsConfig;

            // set the callback functions value to handle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;
        }

        private ISettingsUtils SettingsUtils { get; }

        private GeneralSettings GeneralSettingsConfig { get; }

        private ClipPingSettings Settings { get; }

        private Func<string, int> SendConfigMSG { get; }

        private void InitializeEnabledValue()
        {
            _enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredClipPingEnabledValue();
            if (_enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _enabledStateIsGpoConfigured = true;
                _isEnabled = _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _isEnabled = GeneralSettingsConfig.Enabled.ClipPing;
            }
        }

        public bool IsEnabled
        {
            get => _isEnabled;

            set
            {
                if (_enabledStateIsGpoConfigured)
                {
                    // If it's GPO configured, shouldn't be able to change this state.
                    return;
                }

                if (value != _isEnabled)
                {
                    _isEnabled = value;

                    // Set the status in the general settings configuration
                    GeneralSettingsConfig.Enabled.ClipPing = value;
                    OutGoingGeneralSettings snd = new OutGoingGeneralSettings(GeneralSettingsConfig);

                    SendConfigMSG(snd.ToString());
                    OnPropertyChanged();
                }
            }
        }

        public bool IsEnabledGpoConfigured
        {
            get => _enabledStateIsGpoConfigured;
        }

        public string OverlayColor
        {
            get
            {
                return Settings.Properties.OverlayColor.Value;
            }

            set
            {
                value = (value != null) ? SettingsUtilities.ToRGBHex(value) : "#00FF00";
                if (!value.Equals(Settings.Properties.OverlayColor.Value, StringComparison.OrdinalIgnoreCase))
                {
                    Settings.Properties.OverlayColor.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public int OverlayType
        {
            get
            {
                return (int)Settings.Properties.OverlayType;
            }

            set
            {
                if (value != (int)Settings.Properties.OverlayType)
                {
                    Settings.Properties.OverlayType = (ClipPingOverlay)value;
                    NotifyPropertyChanged();
                }
            }
        }

        public void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
        {
            OnPropertyChanged(propertyName);
            SettingsUtils.SaveSettings(Settings.ToJsonString(), ClipPingSettings.ModuleName);
        }

        public void RefreshEnabledState()
        {
            InitializeEnabledValue();
            OnPropertyChanged(nameof(IsEnabled));
        }
    }
}
