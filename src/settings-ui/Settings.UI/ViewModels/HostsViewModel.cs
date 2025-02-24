// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Threading;

using global::PowerToys.GPOWrapper;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.ViewModels.Commands;
using PowerToys.Interop;
using Settings.UI.Library.Enumerations;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class HostsViewModel : Observable
    {
        private bool _isElevated;
        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private bool _enabledStateIsGPOConfigured;
        private bool _isEnabled;

        private ISettingsUtils SettingsUtils { get; set; }

        private GeneralSettings GeneralSettingsConfig { get; set; }

        private HostsSettings Settings { get; set; }

        private Func<string, int> SendConfigMSG { get; }

        public ButtonClickCommand LaunchEventHandler => new ButtonClickCommand(Launch);

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

                if (value != _isEnabled)
                {
                    _isEnabled = value;

                    // Set the status in the general settings configuration
                    GeneralSettingsConfig.Enabled.Hosts = value;
                    OutGoingGeneralSettings snd = new OutGoingGeneralSettings(GeneralSettingsConfig);

                    SendConfigMSG(snd.ToString());
                    OnPropertyChanged(nameof(IsEnabled));
                }
            }
        }

        public bool IsEnabledGpoConfigured
        {
            get => _enabledStateIsGPOConfigured;
        }

        public bool LaunchAdministratorEnabled => IsEnabled && !_isElevated;

        public bool ShowStartupWarning
        {
            get => Settings.Properties.ShowStartupWarning;
            set
            {
                if (value != Settings.Properties.ShowStartupWarning)
                {
                    Settings.Properties.ShowStartupWarning = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool LoopbackDuplicates
        {
            get => Settings.Properties.LoopbackDuplicates;
            set
            {
                if (value != Settings.Properties.LoopbackDuplicates)
                {
                    Settings.Properties.LoopbackDuplicates = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool LaunchAdministrator
        {
            get => Settings.Properties.LaunchAdministrator;
            set
            {
                if (value != Settings.Properties.LaunchAdministrator)
                {
                    Settings.Properties.LaunchAdministrator = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public int AdditionalLinesPosition
        {
            get => (int)Settings.Properties.AdditionalLinesPosition;
            set
            {
                if (value != (int)Settings.Properties.AdditionalLinesPosition)
                {
                    Settings.Properties.AdditionalLinesPosition = (HostsAdditionalLinesPosition)value;
                    NotifyPropertyChanged();
                }
            }
        }

        public int Encoding
        {
            get => (int)Settings.Properties.Encoding;
            set
            {
                if (value != (int)Settings.Properties.Encoding)
                {
                    Settings.Properties.Encoding = (HostsEncoding)value;
                    NotifyPropertyChanged();
                }
            }
        }

        public HostsViewModel(ISettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, ISettingsRepository<HostsSettings> moduleSettingsRepository, Func<string, int> ipcMSGCallBackFunc, bool isElevated)
        {
            SettingsUtils = settingsUtils;
            GeneralSettingsConfig = settingsRepository.SettingsConfig;
            Settings = moduleSettingsRepository.SettingsConfig;
            SendConfigMSG = ipcMSGCallBackFunc;
            _isElevated = isElevated;
            InitializeEnabledValue();
        }

        private void InitializeEnabledValue()
        {
            _enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredHostsFileEditorEnabledValue();
            if (_enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _enabledStateIsGPOConfigured = true;
                _isEnabled = _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _isEnabled = GeneralSettingsConfig.Enabled.Hosts;
            }
        }

        public void Launch()
        {
            string eventName = !_isElevated && LaunchAdministrator
                ? Constants.ShowHostsAdminSharedEvent()
                : Constants.ShowHostsSharedEvent();

            using (var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventName))
            {
                eventHandle.Set();
            }
        }

        public void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
        {
            OnPropertyChanged(propertyName);
            SettingsUtils.SaveSettings(Settings.ToJsonString(), HostsSettings.ModuleName);
        }

        public void RefreshEnabledState()
        {
            InitializeEnabledValue();
            OnPropertyChanged(nameof(IsEnabled));
        }
    }
}
