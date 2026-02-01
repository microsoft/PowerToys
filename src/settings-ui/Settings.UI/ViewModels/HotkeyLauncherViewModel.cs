// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.SerializationContext;
using PowerToys.GPOWrapper;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class HotkeyLauncherViewModel : PageViewModelBase
    {
        protected override string ModuleName => HotkeyLauncherSettings.ModuleName;

        private Func<string, int> SendConfigMSG { get; }

        private GeneralSettings GeneralSettingsConfig { get; set; }

        public HotkeyLauncherViewModel(ISettingsRepository<GeneralSettings> settingsRepository, HotkeyLauncherSettings initialSettings = null, Func<string, int> ipcMSGCallBackFunc = null)
        {
            ArgumentNullException.ThrowIfNull(settingsRepository);
            GeneralSettingsConfig = settingsRepository.SettingsConfig;
            InitializeEnabledValue();

            _moduleSettings = initialSettings ?? new HotkeyLauncherSettings();
            SendConfigMSG = ipcMSGCallBackFunc;

            HotkeyActions = _moduleSettings.Properties.HotkeyActions.Value;
        }

        public ObservableCollection<HotkeyLauncherAction> HotkeyActions { get; }

        public HotkeyLauncherSettings ModuleSettings
        {
            get => _moduleSettings;
            set
            {
                if (_moduleSettings != value)
                {
                    _moduleSettings = value;
                    OnPropertyChanged(nameof(ModuleSettings));
                }
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

                if (value != _isEnabled)
                {
                    _isEnabled = value;

                    // Set the status in the general settings configuration
                    GeneralSettingsConfig.Enabled.HotkeyLauncher = value;
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

        public GpoRuleConfigured EnabledGPOConfiguration
        {
            get => _enabledGpoRuleConfiguration;
            set
            {
                if (_enabledGpoRuleConfiguration != value)
                {
                    _enabledGpoRuleConfiguration = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public void AddAction()
        {
            int nextId = HotkeyActions.Count > 0
                ? HotkeyActions.Max(a => a.Id) + 1
                : 0;

            var newAction = new HotkeyLauncherAction { Id = nextId };
            newAction.PropertyChanged += Action_PropertyChanged;
            HotkeyActions.Add(newAction);
        }

        public void RemoveAction(HotkeyLauncherAction action)
        {
            if (action != null)
            {
                action.PropertyChanged -= Action_PropertyChanged;
                HotkeyActions.Remove(action);
                SendSettings();
            }
        }

        public void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
        {
            OnPropertyChanged(propertyName);
        }

        public void RefreshEnabledState()
        {
            InitializeEnabledValue();
            OnPropertyChanged(nameof(IsEnabled));
            OnPropertyChanged(nameof(IsEnabledGpoConfigured));
        }

        public void SubscribeToActionChanges()
        {
            foreach (var action in HotkeyActions)
            {
                action.PropertyChanged -= Action_PropertyChanged;
                action.PropertyChanged += Action_PropertyChanged;
            }
        }

        private void Action_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            SendSettings();
        }

        private void SendSettings()
        {
            _moduleSettings.Properties.HotkeyActions.Value = HotkeyActions;

            SndHotkeyLauncherSettings outSettings = new SndHotkeyLauncherSettings(_moduleSettings);
            SndModuleSettings<SndHotkeyLauncherSettings> outIpcMessage = new SndModuleSettings<SndHotkeyLauncherSettings>(outSettings);

            SendConfigMSG?.Invoke(outIpcMessage.ToJsonString());
        }

        private void InitializeEnabledValue()
        {
            _enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredHotkeyLauncherEnabledValue();
            if (_enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _enabledStateIsGPOConfigured = true;
                _isEnabled = _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _isEnabled = GeneralSettingsConfig.Enabled.HotkeyLauncher;
            }
        }

        private bool _enabledStateIsGPOConfigured;
        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private HotkeyLauncherSettings _moduleSettings;
        private bool _isEnabled;
    }
}
