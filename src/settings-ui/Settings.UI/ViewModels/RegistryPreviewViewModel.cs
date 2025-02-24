// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text.Json;

using global::PowerToys.GPOWrapper;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.ViewModels.Commands;
using Microsoft.PowerToys.Settings.UI.SerializationContext;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class RegistryPreviewViewModel : Observable
    {
        private GeneralSettings GeneralSettingsConfig { get; set; }

        public ButtonClickCommand LaunchEventHandler => new ButtonClickCommand(Launch);

        public RegistryPreviewViewModel(ISettingsRepository<GeneralSettings> settingsRepository, ISettingsRepository<RegistryPreviewSettings> registryPreviewSettingsRepository, Func<string, int> ipcMSGCallBackFunc)
        {
            // To obtain the general settings configurations of PowerToys Settings.
            ArgumentNullException.ThrowIfNull(settingsRepository);

            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            _settings = registryPreviewSettingsRepository.SettingsConfig;

            InitializeEnabledValue();

            // set the callback functions value to handle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;
        }

        private void InitializeEnabledValue()
        {
            _enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredRegistryPreviewEnabledValue();
            if (_enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _enabledStateIsGPOConfigured = true;
                _isRegistryPreviewEnabled = _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _isRegistryPreviewEnabled = GeneralSettingsConfig.Enabled.RegistryPreview;
            }
        }

        public bool IsRegistryPreviewEnabled
        {
            get => _isRegistryPreviewEnabled;
            set
            {
                if (_enabledStateIsGPOConfigured)
                {
                    // If it's GPO configured, shouldn't be able to change this state.
                    return;
                }

                if (_isRegistryPreviewEnabled != value)
                {
                    _isRegistryPreviewEnabled = value;
                    OnPropertyChanged(nameof(IsRegistryPreviewEnabled));

                    GeneralSettingsConfig.Enabled.RegistryPreview = value;
                    OutGoingGeneralSettings outgoing = new OutGoingGeneralSettings(GeneralSettingsConfig);
                    SendConfigMSG(outgoing.ToString());
                }
            }
        }

        public bool IsRegistryPreviewDefaultRegApp
        {
            get => _settings.Properties.DefaultRegApp;
            set
            {
                if (_settings.Properties.DefaultRegApp != value)
                {
                    _settings.Properties.DefaultRegApp = value;
                    OnPropertyChanged(nameof(IsRegistryPreviewDefaultRegApp));

                    NotifySettingsChanged();
                }
            }
        }

        public bool IsEnabledGpoConfigured
        {
            get => _enabledStateIsGPOConfigured;
        }

        public void Launch()
        {
            var actionName = "Launch";

            SendConfigMSG("{\"action\":{\"RegistryPreview\":{\"action_name\":\"" + actionName + "\", \"value\":\"\"}}}");
        }

        private Func<string, int> SendConfigMSG { get; }

        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private bool _enabledStateIsGPOConfigured;
        private bool _isRegistryPreviewEnabled;
        private RegistryPreviewSettings _settings;

        public void RefreshEnabledState()
        {
            InitializeEnabledValue();
            OnPropertyChanged(nameof(IsRegistryPreviewEnabled));
        }

        private void NotifySettingsChanged()
        {
            // Using InvariantCulture as this is an IPC message
            SendConfigMSG(
                   string.Format(
                       CultureInfo.InvariantCulture,
                       "{{ \"powertoys\": {{ \"{0}\": {1} }} }}",
                       RegistryPreviewSettings.ModuleName,
                       JsonSerializer.Serialize(_settings, SourceGenerationContextContext.Default.RegistryPreviewSettings)));
        }
    }
}
