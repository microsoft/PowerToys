// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.ViewModels.Commands;
using Microsoft.PowerToys.Settings.UI.SerializationContext;
using PowerToys.GPOWrapper;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    internal class RobocopyUIViewModel : Observable
    {
        private GeneralSettings GeneralSettingsConfig { get; set; }

        public ButtonClickCommand LaunchEventHandler => new ButtonClickCommand(Launch);

        public RobocopyUIViewModel(ISettingsRepository<GeneralSettings> settingsRepository, ISettingsRepository<RobocopyUISettings> robocopyUISettingsRepository, Func<string, int> ipcMSGCallBackFunc)
        {
            // To obtain the general settings configurations of PowerToys Settings.
            ArgumentNullException.ThrowIfNull(settingsRepository);

            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            _settings = robocopyUISettingsRepository.SettingsConfig;

            InitializeEnabledValue();

            // set the callback functions value to handle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;
        }

        private void InitializeEnabledValue()
        {
            _enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredRobocopyUIEnabledValue();
            if (_enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _enabledStateIsGPOConfigured = true;
                _isRobocopyUIEnabled = _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _isRobocopyUIEnabled = GeneralSettingsConfig.Enabled.RobocopyUI;
            }
        }

        public bool IsRobocopyUIEnabled
        {
            get => _isRobocopyUIEnabled;
            set
            {
                if (_enabledStateIsGPOConfigured)
                {
                    // If it's GPO configured, shouldn't be able to change this state.
                    return;
                }

                if (_isRobocopyUIEnabled != value)
                {
                    _isRobocopyUIEnabled = value;
                    OnPropertyChanged(nameof(IsRobocopyUIEnabled));

                    GeneralSettingsConfig.Enabled.RobocopyUI = value;
                    OutGoingGeneralSettings outgoing = new OutGoingGeneralSettings(GeneralSettingsConfig);
                    SendConfigMSG(outgoing.ToString());
                }
            }
        }

        public bool IsEnabledGpoConfigured
        {
            get => _enabledStateIsGPOConfigured;
        }

        public void Launch()
        {
            SendConfigMSG("{\"action\":{\"RobocopyUI\":{\"action_name\":\"Launch\", \"value\":\"\"}}}");
        }

        private Func<string, int> SendConfigMSG { get; }

        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private bool _enabledStateIsGPOConfigured;
        private bool _isRobocopyUIEnabled;
        private RobocopyUISettings _settings;

        public void RefreshEnabledState()
        {
            InitializeEnabledValue();
            OnPropertyChanged(nameof(IsRobocopyUIEnabled));
        }

        private void NotifySettingsChanged()
        {
            // Using InvariantCulture as this is an IPC message
            SendConfigMSG(
                   string.Format(
                       CultureInfo.InvariantCulture,
                       "{{ \"powertoys\": {{ \"{0}\": {1} }} }}",
                       RobocopyUISettings.ModuleName,
                       JsonSerializer.Serialize(_settings, SourceGenerationContextContext.Default.RobocopyUISettings)));
        }
    }
}
