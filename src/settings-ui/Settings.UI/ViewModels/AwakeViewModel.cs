// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using global::PowerToys.GPOWrapper;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class AwakeViewModel : Observable
    {
        private GeneralSettings GeneralSettingsConfig { get; set; }

        private ISettingsRepository<AwakeSettings> AwakeSettingsRepository { get; set; }

        private AwakeSettings Settings { get; set; }

        private Func<string, int> SendConfigMSG { get; }

        public AwakeViewModel(ISettingsRepository<GeneralSettings> settingsRepository, ISettingsRepository<AwakeSettings> moduleSettingsRepository, Func<string, int> ipcMSGCallBackFunc)
        {
            // To obtain the general settings configurations of PowerToys Settings.
            if (settingsRepository == null)
            {
                throw new ArgumentNullException(nameof(settingsRepository));
            }

            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            // To obtain the settings configurations of Fancy zones.
            if (moduleSettingsRepository == null)
            {
                throw new ArgumentNullException(nameof(moduleSettingsRepository));
            }

            AwakeSettingsRepository = moduleSettingsRepository;
            LoadSettings();

            // set the callback functions value to hangle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;

            InitializeEnabledValue();
        }

        public void LoadSettings()
        {
            AwakeSettingsRepository.ReloadSettings();
            Settings = AwakeSettingsRepository.SettingsConfig;

            _keepDisplayOn = Settings.Properties.KeepDisplayOn;
            _mode = Settings.Properties.Mode;
            _intervalHours = Settings.Properties.IntervalHours;
            _intervalMinutes = Settings.Properties.IntervalMinutes;
            _expirationDateTime = Settings.Properties.ExpirationDateTime;
        }

        private void InitializeEnabledValue()
        {
            _enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredAwakeEnabledValue();
            if (_enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _enabledStateIsGPOConfigured = true;
                _isEnabled = _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _isEnabled = GeneralSettingsConfig.Enabled.Awake;
            }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    if (_enabledStateIsGPOConfigured)
                    {
                        // If it's GPO configured, shouldn't be able to change this state.
                        return;
                    }

                    _isEnabled = value;

                    GeneralSettingsConfig.Enabled.Awake = value;
                    OnPropertyChanged(nameof(IsEnabled));
                    OnPropertyChanged(nameof(IsTimeConfigurationEnabled));
                    OnPropertyChanged(nameof(IsScreenConfigurationPossibleEnabled));
                    OnPropertyChanged(nameof(IsExpirationConfigurationEnabled));

                    OutGoingGeneralSettings outgoing = new OutGoingGeneralSettings(GeneralSettingsConfig);
                    SendConfigMSG(outgoing.ToString());
                    NotifyPropertyChanged();
                }
            }
        }

        public bool IsEnabledGpoConfigured
        {
            get => _enabledStateIsGPOConfigured;
        }

        public bool IsExpirationConfigurationEnabled
        {
            get => _mode == AwakeMode.EXPIRABLE && _isEnabled;
        }

        public bool IsTimeConfigurationEnabled
        {
            get => _mode == AwakeMode.TIMED && _isEnabled;
        }

        public bool IsScreenConfigurationPossibleEnabled
        {
            get => _mode != AwakeMode.PASSIVE && _isEnabled;
        }

        public AwakeMode Mode
        {
            get => _mode;
            set
            {
                if (_mode != value)
                {
                    _mode = value;
                    OnPropertyChanged(nameof(IsTimeConfigurationEnabled));
                    OnPropertyChanged(nameof(IsScreenConfigurationPossibleEnabled));
                    OnPropertyChanged(nameof(IsExpirationConfigurationEnabled));

                    Settings.Properties.Mode = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool KeepDisplayOn
        {
            get => _keepDisplayOn;
            set
            {
                if (_keepDisplayOn != value)
                {
                    _keepDisplayOn = value;
                    Settings.Properties.KeepDisplayOn = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public uint IntervalHours
        {
            get => _intervalHours;
            set
            {
                if (_intervalHours != value)
                {
                    _intervalHours = value;
                    Settings.Properties.IntervalHours = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public uint IntervalMinutes
        {
            get => _intervalMinutes;
            set
            {
                if (_intervalMinutes != value)
                {
                    _intervalMinutes = value;
                    Settings.Properties.IntervalMinutes = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public DateTimeOffset ExpirationDateTime
        {
            get => _expirationDateTime;
            set
            {
                if (_expirationDateTime != value)
                {
                    _expirationDateTime = value;
                    Settings.Properties.ExpirationDateTime = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public TimeSpan ExpirationTime
        {
            get => ExpirationDateTime.TimeOfDay;
            set
            {
                if (ExpirationDateTime.TimeOfDay != value)
                {
                    ExpirationDateTime = new DateTime(ExpirationDateTime.Year, ExpirationDateTime.Month, ExpirationDateTime.Day, value.Hours, value.Minutes, value.Seconds);
                }
            }
        }

        public void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
        {
            Logger.LogError($"Changed the property {propertyName}");
            OnPropertyChanged(propertyName);
            if (SendConfigMSG != null)
            {
                SndAwakeSettings outSettings = new(Settings);
                SndModuleSettings<SndAwakeSettings> ipcMessage = new(outSettings);

                string targetMessage = ipcMessage.ToJsonString();

                Logger.LogError($"Sent config JSON: {targetMessage}");
                SendConfigMSG(targetMessage);
            }
        }

        public void RefreshEnabledState()
        {
            InitializeEnabledValue();
            OnPropertyChanged(nameof(IsEnabled));
            OnPropertyChanged(nameof(IsTimeConfigurationEnabled));
            OnPropertyChanged(nameof(IsScreenConfigurationPossibleEnabled));
            OnPropertyChanged(nameof(IsExpirationConfigurationEnabled));
        }

        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private bool _enabledStateIsGPOConfigured;
        private bool _isEnabled;
        private uint _intervalHours;
        private uint _intervalMinutes;
        private bool _keepDisplayOn;
        private DateTimeOffset _expirationDateTime;
        private AwakeMode _mode;
    }
}
