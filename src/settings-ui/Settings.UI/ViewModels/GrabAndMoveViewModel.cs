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
    public partial class GrabAndMoveViewModel : PageViewModelBase
    {
        protected override string ModuleName => GrabAndMoveSettings.ModuleName;

        private GeneralSettings GeneralSettingsConfig { get; set; }

        private Func<string, int> SendConfigMSG { get; }

        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private bool _enabledStateIsGPOConfigured;
        private bool _isEnabled;

        private GrabAndMoveSettings _moduleSettings;

        public GrabAndMoveSettings ModuleSettings => _moduleSettings;

        public GrabAndMoveViewModel(ISettingsRepository<GeneralSettings> settingsRepository, GrabAndMoveSettings moduleSettings, Func<string, int> ipcMSGCallBackFunc)
        {
            ArgumentNullException.ThrowIfNull(settingsRepository);

            GeneralSettingsConfig = settingsRepository.SettingsConfig;
            _moduleSettings = moduleSettings ?? new GrabAndMoveSettings();

            InitializeEnabledValue();

            SendConfigMSG = ipcMSGCallBackFunc ?? (_ => 0);
        }

        private void InitializeEnabledValue()
        {
            _enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredGrabAndMoveEnabledValue();
            if (_enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                _enabledStateIsGPOConfigured = true;
                _isEnabled = _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _isEnabled = GeneralSettingsConfig.Enabled.GrabAndMove;
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
                    GeneralSettingsConfig.Enabled.GrabAndMove = value;
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

        public bool UseAltResize
        {
            get => _moduleSettings.Properties.UseAltResize.Value;

            set
            {
                if (_moduleSettings.Properties.UseAltResize.Value != value)
                {
                    _moduleSettings.Properties.UseAltResize.Value = value;
                    NotifyModuleSettingsChanged();
                    OnPropertyChanged(nameof(UseAltResize));
                }
            }
        }

        public bool DoNotActivateOnGameMode
        {
            get => _moduleSettings.Properties.DoNotActivateOnGameMode.Value;

            set
            {
                if (_moduleSettings.Properties.DoNotActivateOnGameMode.Value != value)
                {
                    _moduleSettings.Properties.DoNotActivateOnGameMode.Value = value;
                    NotifyModuleSettingsChanged();
                    OnPropertyChanged(nameof(DoNotActivateOnGameMode));
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
            SndGrabAndMoveSettings outSettings = new(_moduleSettings);
            SndModuleSettings<SndGrabAndMoveSettings> outIpcMessage = new(outSettings);
            SendConfigMSG(outIpcMessage.ToJsonString());
        }

        public void RefreshEnabledState()
        {
            InitializeEnabledValue();
            OnPropertyChanged(nameof(IsEnabled));
        }
    }
}
