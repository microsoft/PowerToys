// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using global::PowerToys.GPOWrapper;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.SerializationContext;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class WinPosViewModel : PageViewModelBase
    {
        protected override string ModuleName => WinPosSettings.ModuleName;

        private GeneralSettings GeneralSettingsConfig { get; set; }

        private Func<string, int> SendConfigMSG { get; }

        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private bool _enabledStateIsGPOConfigured;
        private bool _isEnabled;

        private WinPosSettings _moduleSettings;

        public WinPosSettings ModuleSettings => _moduleSettings;

        public WinPosViewModel(ISettingsRepository<GeneralSettings> settingsRepository, WinPosSettings moduleSettings, Func<string, int> ipcMSGCallBackFunc)
        {
            ArgumentNullException.ThrowIfNull(settingsRepository);

            GeneralSettingsConfig = settingsRepository.SettingsConfig;
            _moduleSettings = moduleSettings ?? new WinPosSettings();

            InitializeEnabledValue();

            SendConfigMSG = ipcMSGCallBackFunc ?? (_ => 0);
        }

        private void InitializeEnabledValue()
        {
            _enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredWinPosEnabledValue();
            if (_enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                _enabledStateIsGPOConfigured = true;
                _isEnabled = _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _isEnabled = GeneralSettingsConfig.Enabled.WinPos;
            }
        }

        public bool IsEnabled
        {
            get => _isEnabled;

            set
            {
                if (_enabledStateIsGPOConfigured)
                {
                    return;
                }

                if (value != _isEnabled)
                {
                    _isEnabled = value;
                    GeneralSettingsConfig.Enabled.WinPos = value;
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

        public bool ShouldAbsorbAlt
        {
            get => _moduleSettings.Properties.ShouldAbsorbAlt.Value;

            set
            {
                if (_moduleSettings.Properties.ShouldAbsorbAlt.Value != value)
                {
                    _moduleSettings.Properties.ShouldAbsorbAlt.Value = value;
                    NotifyModuleSettingsChanged();
                    OnPropertyChanged(nameof(ShouldAbsorbAlt));
                }
            }
        }

        public bool ShowGeometry
        {
            get => _moduleSettings.Properties.ShowGeometry.Value;

            set
            {
                if (_moduleSettings.Properties.ShowGeometry.Value != value)
                {
                    _moduleSettings.Properties.ShowGeometry.Value = value;
                    NotifyModuleSettingsChanged();
                    OnPropertyChanged(nameof(ShowGeometry));
                }
            }
        }

        public string ExcludedApps
        {
            get => _moduleSettings.Properties.ExcludedApps.Value;

            set
            {
                if (_moduleSettings.Properties.ExcludedApps.Value != value)
                {
                    _moduleSettings.Properties.ExcludedApps.Value = value;
                    NotifyModuleSettingsChanged();
                    OnPropertyChanged(nameof(ExcludedApps));
                }
            }
        }

        private void NotifyModuleSettingsChanged()
        {
            SndWinPosSettings outSettings = new(_moduleSettings);
            SndModuleSettings<SndWinPosSettings> outIpcMessage = new(outSettings);
            SendConfigMSG(outIpcMessage.ToJsonString());
        }

        public void RefreshEnabledState()
        {
            InitializeEnabledValue();
            OnPropertyChanged(nameof(IsEnabled));
        }
    }
}
