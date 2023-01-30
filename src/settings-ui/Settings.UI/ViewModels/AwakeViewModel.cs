// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using global::PowerToys.GPOWrapper;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class AwakeViewModel : Observable
    {
        private GeneralSettings GeneralSettingsConfig { get; set; }

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

            Settings = moduleSettingsRepository.SettingsConfig;

            InitializeEnabledValue();

            _keepDisplayOn = Settings.Properties.KeepDisplayOn;
            _mode = Settings.Properties.Mode;
            _hours = Settings.Properties.Hours;
            _minutes = Settings.Properties.Minutes;

            // set the callback functions value to hangle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;
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
                    OnPropertyChanged(nameof(Mode));
                    OnPropertyChanged(nameof(IsTimeConfigurationEnabled));
                    OnPropertyChanged(nameof(IsScreenConfigurationPossibleEnabled));

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
                    OnPropertyChanged(nameof(KeepDisplayOn));

                    Settings.Properties.KeepDisplayOn = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public uint Hours
        {
            get => _hours;
            set
            {
                if (_hours != value)
                {
                    _hours = value;
                    OnPropertyChanged(nameof(Hours));

                    Settings.Properties.Hours = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public uint Minutes
        {
            get => _minutes;
            set
            {
                if (_minutes != value)
                {
                    _minutes = value;
                    OnPropertyChanged(nameof(Minutes));

                    Settings.Properties.Minutes = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
        {
            OnPropertyChanged(propertyName);
            if (SendConfigMSG != null)
            {
                SndAwakeSettings outsettings = new SndAwakeSettings(Settings);
                SndModuleSettings<SndAwakeSettings> ipcMessage = new SndModuleSettings<SndAwakeSettings>(outsettings);

                string targetMessage = ipcMessage.ToJsonString();
                SendConfigMSG(targetMessage);
            }
        }

        public void RefreshEnabledState()
        {
            InitializeEnabledValue();
            OnPropertyChanged(nameof(IsEnabled));
            OnPropertyChanged(nameof(IsTimeConfigurationEnabled));
            OnPropertyChanged(nameof(IsScreenConfigurationPossibleEnabled));
        }

        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private bool _enabledStateIsGPOConfigured;
        private bool _isEnabled;
        private uint _hours;
        private uint _minutes;
        private bool _keepDisplayOn;
        private AwakeMode _mode;
    }
}
