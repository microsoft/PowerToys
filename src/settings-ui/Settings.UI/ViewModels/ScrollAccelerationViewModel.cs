// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using global::PowerToys.GPOWrapper;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class ScrollAccelerationViewModel : PageViewModelBase
    {
        protected override string ModuleName => ScrollAccelerationSettings.ModuleName;

        private GeneralSettings GeneralSettingsConfig { get; set; }

        private Func<string, int> SendConfigMSG { get; }

        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private bool _enabledStateIsGPOConfigured;
        private bool _isEnabled;

        private ScrollAccelerationSettings _moduleSettings;

        public ScrollAccelerationSettings ModuleSettings => _moduleSettings;

        public ScrollAccelerationViewModel(ISettingsRepository<GeneralSettings> settingsRepository, ScrollAccelerationSettings moduleSettings, Func<string, int> ipcMSGCallBackFunc)
        {
            ArgumentNullException.ThrowIfNull(settingsRepository);

            GeneralSettingsConfig = settingsRepository.SettingsConfig;
            _moduleSettings = moduleSettings ?? new ScrollAccelerationSettings();

            InitializeEnabledValue();

            SendConfigMSG = ipcMSGCallBackFunc ?? (_ => 0);
        }

        private void InitializeEnabledValue()
        {
            _enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredScrollAccelerationEnabledValue();
            if (_enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                _enabledStateIsGPOConfigured = true;
                _isEnabled = _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _isEnabled = GeneralSettingsConfig.Enabled.ScrollAcceleration;
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
                    GeneralSettingsConfig.Enabled.ScrollAcceleration = value;
                    OutGoingGeneralSettings snd = new OutGoingGeneralSettings(GeneralSettingsConfig);
                    SendConfigMSG(snd.ToString());
                    OnPropertyChanged(nameof(IsEnabled));
                }
            }
        }

        public bool IsEnabledGpoConfigured => _enabledStateIsGPOConfigured;

        public HotkeySettings ActivationShortcut
        {
            get => _moduleSettings.Properties.ActivationShortcut;

            set
            {
                if (_moduleSettings.Properties.ActivationShortcut != value)
                {
                    _moduleSettings.Properties.ActivationShortcut = value ?? _moduleSettings.Properties.DefaultActivationShortcut;
                    NotifyModuleSettingsChanged();
                    OnPropertyChanged(nameof(ActivationShortcut));
                }
            }
        }

        public HotkeySettings ExcludeAppShortcut
        {
            get => _moduleSettings.Properties.ExcludeAppShortcut;

            set
            {
                if (_moduleSettings.Properties.ExcludeAppShortcut != value)
                {
                    _moduleSettings.Properties.ExcludeAppShortcut = value ?? _moduleSettings.Properties.DefaultExcludeAppShortcut;
                    NotifyModuleSettingsChanged();
                    OnPropertyChanged(nameof(ExcludeAppShortcut));
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

        // The utility edits the list itself when the exclude-app shortcut is used; take over its value without
        // sending the settings back.
        public void UpdateExcludedAppsFromFile(string value)
        {
            if (_moduleSettings.Properties.ExcludedApps.Value != value)
            {
                _moduleSettings.Properties.ExcludedApps.Value = value;
                OnPropertyChanged(nameof(ExcludedApps));
            }
        }

        public override Dictionary<string, HotkeySettings[]> GetAllHotkeySettings()
        {
            return new Dictionary<string, HotkeySettings[]>
            {
                [ScrollAccelerationSettings.ModuleName] = [ActivationShortcut, ExcludeAppShortcut],
            };
        }

        public int TimeThresholdMs
        {
            get => _moduleSettings.Properties.TimeThresholdMs.Value;
            set => SetInt(_moduleSettings.Properties.TimeThresholdMs, value, nameof(TimeThresholdMs));
        }

        public int AccelerationPercent
        {
            get => _moduleSettings.Properties.AccelerationPercent.Value;
            set => SetInt(_moduleSettings.Properties.AccelerationPercent, value, nameof(AccelerationPercent));
        }

        public int SlowScrollPercent
        {
            get => _moduleSettings.Properties.SlowScrollPercent.Value;
            set => SetInt(_moduleSettings.Properties.SlowScrollPercent, value, nameof(SlowScrollPercent));
        }

        public int MaxMultiplier
        {
            get => _moduleSettings.Properties.MaxMultiplier.Value;
            set => SetInt(_moduleSettings.Properties.MaxMultiplier, value, nameof(MaxMultiplier));
        }

        public bool ApplyToHorizontalWheel
        {
            get => _moduleSettings.Properties.ApplyToHorizontalWheel.Value;

            set
            {
                if (_moduleSettings.Properties.ApplyToHorizontalWheel.Value != value)
                {
                    _moduleSettings.Properties.ApplyToHorizontalWheel.Value = value;
                    NotifyModuleSettingsChanged();
                    OnPropertyChanged(nameof(ApplyToHorizontalWheel));
                }
            }
        }

        public bool IgnoreWithCtrl
        {
            get => _moduleSettings.Properties.IgnoreWithCtrl.Value;

            set
            {
                if (_moduleSettings.Properties.IgnoreWithCtrl.Value != value)
                {
                    _moduleSettings.Properties.IgnoreWithCtrl.Value = value;
                    NotifyModuleSettingsChanged();
                    OnPropertyChanged(nameof(IgnoreWithCtrl));
                }
            }
        }

        public bool MomentumEnabled
        {
            get => _moduleSettings.Properties.MomentumEnabled.Value;

            set
            {
                if (_moduleSettings.Properties.MomentumEnabled.Value != value)
                {
                    _moduleSettings.Properties.MomentumEnabled.Value = value;
                    NotifyModuleSettingsChanged();
                    OnPropertyChanged(nameof(MomentumEnabled));
                }
            }
        }

        public int MomentumDurationMs
        {
            get => _moduleSettings.Properties.MomentumDurationMs.Value;
            set => SetInt(_moduleSettings.Properties.MomentumDurationMs, value, nameof(MomentumDurationMs));
        }

        private void SetInt(IntProperty property, int value, string propertyName)
        {
            if (property.Value != value)
            {
                property.Value = value;
                NotifyModuleSettingsChanged();
                OnPropertyChanged(propertyName);
            }
        }

        private void NotifyModuleSettingsChanged()
        {
            SndScrollAccelerationSettings outSettings = new(_moduleSettings);
            SndModuleSettings<SndScrollAccelerationSettings> outIpcMessage = new(outSettings);
            SendConfigMSG(outIpcMessage.ToJsonString());
        }

        public void RefreshEnabledState()
        {
            InitializeEnabledValue();
            OnPropertyChanged(nameof(IsEnabled));
        }
    }
}
