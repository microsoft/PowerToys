// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using global::PowerToys.GPOWrapper;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class ShowDesktopViewModel : PageViewModelBase
    {
        protected override string ModuleName => ShowDesktopSettings.ModuleName;

        private SettingsUtils SettingsUtils { get; set; }

        private GeneralSettings GeneralSettingsConfig { get; set; }

        private ShowDesktopSettings Settings { get; set; }

        private Func<string, int> SendConfigMSG { get; }

        public ShowDesktopViewModel(SettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, ISettingsRepository<ShowDesktopSettings> moduleSettingsRepository, Func<string, int> ipcMSGCallBackFunc)
        {
            ArgumentNullException.ThrowIfNull(settingsUtils);

            SettingsUtils = settingsUtils;

            // To obtain the general settings configurations of PowerToys Settings.
            ArgumentNullException.ThrowIfNull(settingsRepository);

            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            InitializeEnabledValue();

            // To obtain the settings configurations of ShowDesktop.
            ArgumentNullException.ThrowIfNull(moduleSettingsRepository);

            Settings = moduleSettingsRepository.SettingsConfig;

            _peekMode = Settings.Properties.PeekMode.Value;
            _requireDoubleClick = Settings.Properties.RequireDoubleClick.Value;
            _enableTaskbarPeek = Settings.Properties.EnableTaskbarPeek.Value;
            _enableGamingDetection = Settings.Properties.EnableGamingDetection.Value;
            _flyAwayAnimationDurationMs = Settings.Properties.FlyAwayAnimationDurationMs.Value;

            // set the callback functions value to handle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;
        }

        private void InitializeEnabledValue()
        {
            _enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredShowDesktopEnabledValue();
            if (_enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _enabledStateIsGPOConfigured = true;
                _isEnabled = _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _isEnabled = GeneralSettingsConfig.Enabled.ShowDesktop;
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
                    GeneralSettingsConfig.Enabled.ShowDesktop = value;
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

        public int PeekMode
        {
            get => _peekMode;

            set
            {
                if (value != _peekMode)
                {
                    _peekMode = value;
                    Settings.Properties.PeekMode.Value = value;
                    NotifyPropertyChanged();
                    OnPropertyChanged(nameof(IsFlyAwayMode));
                }
            }
        }

        public bool RequireDoubleClick
        {
            get => _requireDoubleClick;

            set
            {
                if (value != _requireDoubleClick)
                {
                    _requireDoubleClick = value;
                    Settings.Properties.RequireDoubleClick.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool EnableTaskbarPeek
        {
            get => _enableTaskbarPeek;

            set
            {
                if (value != _enableTaskbarPeek)
                {
                    _enableTaskbarPeek = value;
                    Settings.Properties.EnableTaskbarPeek.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool EnableGamingDetection
        {
            get => _enableGamingDetection;

            set
            {
                if (value != _enableGamingDetection)
                {
                    _enableGamingDetection = value;
                    Settings.Properties.EnableGamingDetection.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public int FlyAwayAnimationDurationMs
        {
            get => _flyAwayAnimationDurationMs;

            set
            {
                if (value != _flyAwayAnimationDurationMs)
                {
                    _flyAwayAnimationDurationMs = value;
                    Settings.Properties.FlyAwayAnimationDurationMs.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool IsFlyAwayMode => PeekMode == 2;

        public void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
        {
            OnPropertyChanged(propertyName);
            SettingsUtils.SaveSettings(Settings.ToJsonString(), ShowDesktopSettings.ModuleName);
        }

        public void RefreshEnabledState()
        {
            InitializeEnabledValue();
            OnPropertyChanged(nameof(IsEnabled));
        }

        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private bool _enabledStateIsGPOConfigured;
        private bool _isEnabled;
        private int _peekMode;
        private bool _requireDoubleClick;
        private bool _enableTaskbarPeek;
        private bool _enableGamingDetection;
        private int _flyAwayAnimationDurationMs;
    }
}
