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

        // =========================================================
        //  UPDATED COLOR LOGIC
        // =========================================================
        private static Color ParseColor(string hex)
        {
            if (string.IsNullOrEmpty(hex))
            {
                return Colors.Black;
            }

            // Convert to Span to avoid allocations
            ReadOnlySpan<char> hexSpan = hex.AsSpan();

            // Skip the '#' if it exists
            if (hexSpan.Length > 0 && hexSpan[0] == '#')
            {
                hexSpan = hexSpan.Slice(1);
            }

            // Sanity check: Ensure we have exactly 6 characters left (RRGGBB)
            if (hexSpan.Length != 6)
            {
                return Colors.Black;
            }

            try
            {
                // Parse directly from the span slices
                byte r = byte.Parse(hexSpan.Slice(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                byte g = byte.Parse(hexSpan.Slice(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                byte b = byte.Parse(hexSpan.Slice(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

                return Color.FromArgb(255, r, g, b);
            }
            catch
            {
                return Colors.Black;
            }
        }

        // Helper: Convert Windows.UI.Color to "#RRGGBB" string
        private static string ColorToHex(Color c)
        {
            return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }

        public Color TextColorWithAlpha
        {
            get
            {
                // Convert string hex to Color struct
                Color color = ParseColor(_settings.Properties.TextColor.Value);

                // Apply the separate Opacity setting
                byte alpha = (byte)(_settings.Properties.TextOpacity.Value * 2.55);
                return Color.FromArgb(alpha, color.R, color.G, color.B);
            }

            set
            {
                string newColorHex = ColorToHex(value);
                int newOpacity = (int)(value.A / 2.55);

                bool changed = false;

                // Compare Strings
                if (_settings.Properties.TextColor.Value != newColorHex)
                {
                    _settings.Properties.TextColor.Value = newColorHex;
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

        // Changed type from int to string
        public string TextColor
        {
            get => _settings.Properties.TextColor.Value;
            set
            {
                if (_settings.Properties.TextColor.Value != value)
                {
                    _settings.Properties.TextColor.Value = value;
                    NotifySettingsChanged();
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
                }
            }
        }

        public Color BackgroundColorWithAlpha
        {
            get
            {
                Color color = ParseColor(_settings.Properties.BackgroundColor.Value);
                byte alpha = (byte)(_settings.Properties.BackgroundOpacity.Value * 2.55);
                return Color.FromArgb(alpha, color.R, color.G, color.B);
            }

            set
            {
                string newColorHex = ColorToHex(value);
                int newOpacity = (int)(value.A / 2.55);

                bool changed = false;

                if (_settings.Properties.BackgroundColor.Value != newColorHex)
                {
                    _settings.Properties.BackgroundColor.Value = newColorHex;
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

        // Changed type from int to string
        public string BackgroundColor
        {
            get => _settings.Properties.BackgroundColor.Value;
            set
            {
                if (_settings.Properties.BackgroundColor.Value != value)
                {
                    _settings.Properties.BackgroundColor.Value = value;
                    NotifySettingsChanged();
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
