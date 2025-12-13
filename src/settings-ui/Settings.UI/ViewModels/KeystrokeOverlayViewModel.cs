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
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.SerializationContext;
using Microsoft.UI;
using Windows.UI;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class KeystrokeOverlayViewModel : PageViewModelBase
    {
        protected override string ModuleName => KeystrokeOverlaySettings.ModuleName;

        private GeneralSettings GeneralSettingsConfig { get; set; }

        private readonly KeystrokeOverlaySettings _settings;
        private readonly ISettingsUtils _settingsUtils;

        private Func<string, int> SendConfigMSG { get; }

        private bool _enabledStateIsGPOConfigured;
        private bool _isEnabled;

        public KeystrokeOverlayViewModel(
            ISettingsUtils settingsUtils,
            ISettingsRepository<GeneralSettings> generalSettingsRepository,
            ISettingsRepository<KeystrokeOverlaySettings> moduleSettingsRepository,
            Func<string, int> ipcMSGCallBackFunc)
        {
            _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));
            ArgumentNullException.ThrowIfNull(generalSettingsRepository);
            ArgumentNullException.ThrowIfNull(moduleSettingsRepository);

            GeneralSettingsConfig = generalSettingsRepository.SettingsConfig;
            _settings = moduleSettingsRepository.SettingsConfig;
            SendConfigMSG = ipcMSGCallBackFunc;

            _enabledStateIsGPOConfigured = false;

            InitializeEnabledValue();
        }

        private void InitializeEnabledValue()
        {
            _isEnabled = GeneralSettingsConfig.Enabled.KeystrokeOverlay;
        }

        public override Dictionary<string, HotkeySettings[]> GetAllHotkeySettings()
        {
            var hotkeysDict = new Dictionary<string, HotkeySettings[]>
            {
                [ModuleName] = new[] { SwitchMonitorHotkey },
                [ModuleName + "_Activation"] = new[] { ActivationShortcut },
                [ModuleName + "_SwitchDisplayMode"] = new[] { SwitchDisplayModeHotkey },
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
                    return;
                }

                if (value != _isEnabled)
                {
                    _isEnabled = value;
                    GeneralSettingsConfig.Enabled.KeystrokeOverlay = value;
                    OutGoingGeneralSettings snd = new OutGoingGeneralSettings(GeneralSettingsConfig);
                    SendConfigMSG(snd.ToString());
                    OnPropertyChanged(nameof(IsEnabled));
                }
            }
        }

        public bool IsEnabledGpoConfigured => _enabledStateIsGPOConfigured;

        public HotkeySettings ActivationShortcut
        {
            get => _settings.Properties.ActivationShortcut;
            set
            {
                if (_settings.Properties.ActivationShortcut != value)
                {
                    _settings.Properties.ActivationShortcut = value ?? _settings.Properties.ActivationShortcut;
                    NotifySettingsChanged();
                }
            }
        }

        public HotkeySettings SwitchMonitorHotkey
        {
            get => _settings.Properties.SwitchMonitorHotkey;
            set
            {
                if (_settings.Properties.SwitchMonitorHotkey != value)
                {
                    _settings.Properties.SwitchMonitorHotkey = value ?? _settings.Properties.DefaultSwitchMonitorHotkey;
                    NotifySettingsChanged();
                }
            }
        }

        public HotkeySettings SwitchDisplayModeHotkey
        {
            get => _settings.Properties.SwitchDisplayModeHotkey;
            set
            {
                if (_settings.Properties.SwitchDisplayModeHotkey != value)
                {
                    _settings.Properties.SwitchDisplayModeHotkey = value ?? _settings.Properties.DefaultSwitchDisplayModeHotkey;
                    NotifySettingsChanged();
                }
            }
        }

        public bool IsDraggableOverlayEnabled
        {
            get => _settings.Properties.IsDraggableOverlayEnabled.Value;
            set
            {
                if (_settings.Properties.IsDraggableOverlayEnabled.Value != value)
                {
                    _settings.Properties.IsDraggableOverlayEnabled.Value = value;
                    NotifySettingsChanged();
                }
            }
        }

        public int DisplayMode
        {
            get => _settings.Properties.DisplayMode.Value;
            set
            {
                if (_settings.Properties.DisplayMode.Value != value)
                {
                    _settings.Properties.DisplayMode.Value = value;
                    NotifySettingsChanged();
                }
            }
        }

        public int OverlayTimeout
        {
            get => _settings.Properties.OverlayTimeout.Value;
            set
            {
                if (_settings.Properties.OverlayTimeout.Value != value)
                {
                    _settings.Properties.OverlayTimeout.Value = value;
                    NotifySettingsChanged();
                }
            }
        }

        public int TextSize
        {
            get => _settings.Properties.TextSize.Value;
            set
            {
                if (_settings.Properties.TextSize.Value != value)
                {
                    _settings.Properties.TextSize.Value = value;
                    NotifySettingsChanged();
                }
            }
        }

        // =========================================================
        //  COLOR SETTINGS (Hex String #AARRGGBB)
        // =========================================================
        public string TextColor
        {
            get => _settings.Properties.TextColor.Value;
            set
            {
                // Ensure value is a valid Hex string; default to Black if null
                value = (value != null) ? SettingsUtilities.ToARGBHex(value) : "#FF000000";

                if (!_settings.Properties.TextColor.Value.Equals(value, StringComparison.OrdinalIgnoreCase))
                {
                    _settings.Properties.TextColor.Value = value;
                    NotifySettingsChanged();
                }
            }
        }

        public string BackgroundColor
        {
            get => _settings.Properties.BackgroundColor.Value;
            set
            {
                // Ensure value is a valid Hex string; default to Transparent if null
                value = (value != null) ? SettingsUtilities.ToARGBHex(value) : "#00000000";

                if (!_settings.Properties.BackgroundColor.Value.Equals(value, StringComparison.OrdinalIgnoreCase))
                {
                    _settings.Properties.BackgroundColor.Value = value;
                    NotifySettingsChanged();
                }
            }
        }

        /* Notify Settings Changed */

        private void NotifySettingsChanged([CallerMemberName] string propertyName = null)
        {
            OnPropertyChanged(propertyName);

            // send settings to powertoys process
            string jsonSettings = JsonSerializer.Serialize(_settings);
            string ipcMessage = $"{{\"powertoys\":{{\"{ModuleName}\":{jsonSettings}}}}}";
            SendConfigMSG(ipcMessage.ToString());

            // save locally
            _settingsUtils.SaveSettings(_settings.ToJsonString(), ModuleName);
        }

        public void RefreshEnabledState()
        {
            InitializeEnabledValue();
            OnPropertyChanged(nameof(IsEnabled));
        }
    }
}
