// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using global::PowerToys.GPOWrapper;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.SerializationContext;
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

        // private GpoRuleConfigured _enabledGpoRuleConfiguration;
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
            // _enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredKeystrokeOverlayEnabledValue();
            // if (_enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            // {
            //    // Get the enabled state from GPO.
            //    _enabledStateIsGPOConfigured = true;
            //    _isEnabled = _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            // }
            // else
            // {
            _isEnabled = GeneralSettingsConfig.Enabled.KeystrokeOverlay;

            // }
        }

        public override Dictionary<string, HotkeySettings[]> GetAllHotkeySettings()
        {
            var hotkeysDict = new Dictionary<string, HotkeySettings[]>
            {
                [ModuleName] = [SwitchMonitorHotkey],
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

                    // set status in the general settings configuration
                    GeneralSettingsConfig.Enabled.KeystrokeOverlay = value;
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

        /* Color and Opacity Settings */

        public Color TextColorWithAlpha
        {
            get
            {
                Color color = IntToColor(_settings.Properties.TextColor.Value);
                byte alpha = (byte)(_settings.Properties.TextOpacity.Value * 2.55);
                return Color.FromArgb(alpha, color.R, color.G, color.B);
            }

            set
            {
                int newColorRGB = ColorToInt(value);
                int newOpacity = (int)(value.A * 2.55);

                bool changed = false;

                if (_settings.Properties.TextColor.Value != newColorRGB)
                {
                    _settings.Properties.TextColor.Value = newColorRGB;
                    OnPropertyChanged(nameof(TextColor));
                    changed = true;
                }

                if (_settings.Properties.TextOpacity.Value != newOpacity)
                {
                    _settings.Properties.TextOpacity.Value = newOpacity;
                    OnPropertyChanged(nameof(TextOpacity));
                    changed = true;
                }

                if (changed)
                {
                    NotifySettingsChanged();
                    OnPropertyChanged(nameof(TextColorWithAlpha));
                }
            }
        }

        public int TextColor
        {
            get => _settings.Properties.TextColor.Value;
            set
            {
                if (_settings.Properties.TextColor.Value != value)
                {
                    _settings.Properties.TextColor.Value = value;
                    NotifySettingsChanged();
                    OnPropertyChanged(nameof(TextColorWithAlpha));
                }
            }
        }

        public int TextOpacity
        {
            get => _settings.Properties.TextOpacity.Value;
            set
            {
                if (_settings.Properties.TextOpacity.Value != value)
                {
                    _settings.Properties.TextOpacity.Value = value;
                    NotifySettingsChanged();
                    OnPropertyChanged(nameof(TextOpacity));
                    OnPropertyChanged(nameof(TextColorWithAlpha));
                }
            }
        }

        public Color BackgroundColorWithAlpha
        {
            get
            {
                Color color = IntToColor(_settings.Properties.BackgroundColor.Value);
                byte alpha = (byte)(_settings.Properties.BackgroundOpacity.Value * 2.55);
                return Color.FromArgb(alpha, color.R, color.G, color.B);
            }

            set
            {
                int newColorRGB = ColorToInt(value);
                int newOpacity = (int)(value.A * 2.55);

                bool changed = false;

                if (_settings.Properties.BackgroundColor.Value != newColorRGB)
                {
                    _settings.Properties.BackgroundColor.Value = newColorRGB;
                    OnPropertyChanged(nameof(BackgroundColor));
                    changed = true;
                }

                if (_settings.Properties.BackgroundOpacity.Value != newOpacity)
                {
                    _settings.Properties.BackgroundOpacity.Value = newOpacity;
                    OnPropertyChanged(nameof(BackgroundOpacity));
                    changed = true;
                }

                if (changed)
                {
                    NotifySettingsChanged();
                    OnPropertyChanged(nameof(BackgroundColorWithAlpha));
                }
            }
        }

        public int BackgroundColor
        {
            get => _settings.Properties.BackgroundColor.Value;
            set
            {
                if (_settings.Properties.BackgroundColor.Value != value)
                {
                    _settings.Properties.BackgroundColor.Value = value;
                    NotifySettingsChanged();
                    OnPropertyChanged(nameof(BackgroundColorWithAlpha));
                }
            }
        }

        public int BackgroundOpacity
        {
            get => _settings.Properties.BackgroundOpacity.Value;
            set
            {
                if (_settings.Properties.BackgroundOpacity.Value != value)
                {
                    _settings.Properties.BackgroundOpacity.Value = value;
                    NotifySettingsChanged();
                    OnPropertyChanged(nameof(BackgroundOpacity));
                    OnPropertyChanged(nameof(BackgroundColorWithAlpha));
                }
            }
        }

        private static Color IntToColor(int v)
        {
            byte r = (byte)((v >> 16) & 0xFF);
            byte g = (byte)((v >> 8) & 0xFF);
            byte b = (byte)(v & 0xFF);
            return Color.FromArgb(255, r, g, b);
        }

        private static int ColorToInt(Color c)
        {
            return (c.R << 16) | (c.G << 8) | c.B;
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
