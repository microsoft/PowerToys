// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using global::PowerToys.GPOWrapper;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Enumerations;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class AltBacktickViewModel : Observable
    {
        private GeneralSettings GeneralSettingsConfig { get; set; }

        private readonly AltBacktickSettings _altBacktickSettings;
        private readonly SettingsUtils _settingsUtils;

        private Func<string, int> SendConfigMSG { get; }

        public AltBacktickViewModel(SettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, Func<string, int> ipcMSGCallBackFunc)
        {
            ArgumentNullException.ThrowIfNull(settingsRepository);
            _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));
            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            InitializeEnabledValue();

            // Load AltBacktick settings
            _altBacktickSettings = _settingsUtils.GetSettingsOrDefault<AltBacktickSettings>(AltBacktickSettings.ModuleName);

            SendConfigMSG = ipcMSGCallBackFunc;
        }

        private void InitializeEnabledValue()
        {
            // TODO: Add GPO support for AltBacktick
            // _enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredAltBacktickEnabledValue();
            _enabledGpoRuleConfiguration = GpoRuleConfigured.NotConfigured;
            if (_enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _enabledStateIsGPOConfigured = true;
                _isEnabled = _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _isEnabled = GeneralSettingsConfig.Enabled.AltBacktick;
            }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_enabledStateIsGPOConfigured)
                {
                    // If it's GPO configured, shouldn't be able to change this state.
                    return;
                }

                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    GeneralSettingsConfig.Enabled.AltBacktick = value;
                    OnPropertyChanged(nameof(IsEnabled));
                    OutGoingGeneralSettings outgoing = new OutGoingGeneralSettings(GeneralSettingsConfig);
                    SendConfigMSG(outgoing.ToString());
                }
            }
        }

        public bool IsEnabledGpoConfigured
        {
            get => _enabledStateIsGPOConfigured;
        }

        public int ModifierKey
        {
            get
            {
                return _altBacktickSettings.Properties.ModifierKey.Value;
            }

            set
            {
                if (value != _altBacktickSettings.Properties.ModifierKey.Value)
                {
                    _altBacktickSettings.Properties.ModifierKey.Value = value;
                    OnPropertyChanged(nameof(ModifierKey));
                    RaisePropertyChanged();
                }
            }
        }

        public bool IgnoreMinimizedWindows
        {
            get
            {
                return _altBacktickSettings.Properties.IgnoreMinimizedWindows.Value;
            }

            set
            {
                if (value != _altBacktickSettings.Properties.IgnoreMinimizedWindows.Value)
                {
                    _altBacktickSettings.Properties.IgnoreMinimizedWindows.Value = value;
                    OnPropertyChanged(nameof(IgnoreMinimizedWindows));
                    RaisePropertyChanged();
                }
            }
        }

        private void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            // Notify UI of property change
            OnPropertyChanged(propertyName);

            _settingsUtils.SaveSettings(_altBacktickSettings.ToJsonString(), AltBacktickSettings.ModuleName);
            SendConfigMSG("{\"powertoys\":{\"" + AltBacktickSettings.ModuleName + "\":" + _altBacktickSettings.ToJsonString() + "}}");
        }

        public void RefreshEnabledState()
        {
            InitializeEnabledValue();
            OnPropertyChanged(nameof(IsEnabled));
        }

        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private bool _enabledStateIsGPOConfigured;
        private bool _isEnabled;
    }
}
