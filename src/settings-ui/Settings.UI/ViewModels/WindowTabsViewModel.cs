// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using global::PowerToys.GPOWrapper;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Settings.UI.SerializationContext;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class WindowTabsViewModel : PageViewModelBase
    {
        protected override string ModuleName => WindowTabsSettings.ModuleName;

        private SettingsUtils SettingsUtils { get; set; }

        private GeneralSettings GeneralSettingsConfig { get; set; }

        private WindowTabsSettings Settings { get; set; }

        private Func<string, int> SendConfigMSG { get; }

        public WindowTabsViewModel(SettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, ISettingsRepository<WindowTabsSettings> moduleSettingsRepository, Func<string, int> ipcMSGCallBackFunc)
        {
            ArgumentNullException.ThrowIfNull(settingsUtils);

            SettingsUtils = settingsUtils;

            // To obtain the general settings configurations of PowerToys Settings.
            ArgumentNullException.ThrowIfNull(settingsRepository);

            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            InitializeEnabledValue();

            // To obtain the settings configurations of WindowTabs.
            ArgumentNullException.ThrowIfNull(moduleSettingsRepository);

            Settings = moduleSettingsRepository.SettingsConfig;

            _nextWindowShortcut = Settings.Properties.NextTabShortcut;
            _previousWindowShortcut = Settings.Properties.PreviousTabShortcut;

            // set the callback functions value to handle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;
        }

        private void InitializeEnabledValue()
        {
            _enabledGpoRuleConfiguration = ModuleGpoHelper.GetModuleGpoConfiguration(ManagedCommon.ModuleType.WindowTabs);
            if (_enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _enabledStateIsGPOConfigured = true;
                _isEnabled = _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _isEnabled = GeneralSettingsConfig.Enabled.WindowTabs;
            }
        }

        public override Dictionary<string, HotkeySettings[]> GetAllHotkeySettings()
        {
            var hotkeysDict = new Dictionary<string, HotkeySettings[]>
            {
                [ModuleName] = [NextTabShortcut, PreviousTabShortcut],
            };

            return hotkeysDict;
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

                if (value != _isEnabled)
                {
                    _isEnabled = value;

                    // Set the status in the general settings configuration
                    GeneralSettingsConfig.Enabled.WindowTabs = value;
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

        public HotkeySettings NextTabShortcut
        {
            get => _nextWindowShortcut;

            set
            {
                if (value != _nextWindowShortcut)
                {
                    _nextWindowShortcut = value ?? Settings.Properties.DefaultNextTabShortcut;

                    Settings.Properties.NextTabShortcut = _nextWindowShortcut;
                    NotifyPropertyChanged();
                    SendSettingsConfigMessage();
                }
            }
        }

        public HotkeySettings PreviousTabShortcut
        {
            get => _previousWindowShortcut;

            set
            {
                if (value != _previousWindowShortcut)
                {
                    _previousWindowShortcut = value ?? Settings.Properties.DefaultPreviousTabShortcut;

                    Settings.Properties.PreviousTabShortcut = _previousWindowShortcut;
                    NotifyPropertyChanged();
                    SendSettingsConfigMessage();
                }
            }
        }

        public void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
        {
            OnPropertyChanged(propertyName);
            SettingsUtils.SaveSettings(Settings.ToJsonString(), WindowTabsSettings.ModuleName);
        }

        public void RefreshEnabledState()
        {
            InitializeEnabledValue();
            OnPropertyChanged(nameof(IsEnabled));
        }

        private void SendSettingsConfigMessage()
        {
            SendConfigMSG(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{{ \"powertoys\": {{ \"{0}\": {1} }} }}",
                    WindowTabsSettings.ModuleName,
                    JsonSerializer.Serialize(Settings, SourceGenerationContextContext.Default.WindowTabsSettings)));
        }

        public bool RestoreSession
        {
            get => Settings.Properties.RestoreSession.Value;
            set
            {
                if (value != RestoreSession)
                {
                    Settings.Properties.RestoreSession.Value = value;
                    NotifyPropertyChanged();
                    SendSettingsConfigMessage();
                }
            }
        }

        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private bool _enabledStateIsGPOConfigured;
        private bool _isEnabled;
        private HotkeySettings _nextWindowShortcut;
        private HotkeySettings _previousWindowShortcut;
    }
}
