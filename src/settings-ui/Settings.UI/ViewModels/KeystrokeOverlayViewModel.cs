// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.UI;
using Windows.UI;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class KeystrokeOverlayViewModel : PageViewModelBase
    {
        protected override string ModuleName => KeystrokeOverlaySettings.ModuleName;

        private KeystrokeOverlaySettings _settings;
        private GeneralSettings _generalSettings;
        private readonly Func<string, int> _sendConfigMSG;
        private readonly ISettingsUtils _settingsUtils;

        public KeystrokeOverlayViewModel(ISettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> generalSettingsRepository, ISettingsRepository<KeystrokeOverlaySettings> moduleSettingsRepository, Func<string, int> ipcMSGCallBackFunc)
        {
            _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));
            ArgumentNullException.ThrowIfNull(generalSettingsRepository);
            ArgumentNullException.ThrowIfNull(moduleSettingsRepository);

            _generalSettings = generalSettingsRepository.SettingsConfig;
            _settings = moduleSettingsRepository.SettingsConfig;
            _sendConfigMSG = ipcMSGCallBackFunc;

            // initialize backing fields if needed (optional)
        }

        public bool IsEnabled
        {
            get => _generalSettings.Enabled?.KeystrokeOverlay ?? false;
            set
            {
                if (_generalSettings.Enabled.KeystrokeOverlay != value)
                {
                    _generalSettings.Enabled.KeystrokeOverlay = value;
                    _sendConfigMSG(new OutGoingGeneralSettings(_generalSettings).ToString());
                    OnPropertyChanged(nameof(IsEnabled));
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

        public bool IsDraggableOverlayEnabled
        {
            get => _settings.Properties.IsDraggableOverlayEnabled.Value;
            set
            {
                if (_settings.Properties.IsDraggableOverlayEnabled.Value != value)
                {
                    _settings.Properties.IsDraggableOverlayEnabled.Value = value;
                    NotifySettingsChanged();
                    OnPropertyChanged(nameof(IsDraggableOverlayEnabled));
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
                    OnPropertyChanged(nameof(DisplayMode));
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
                    OnPropertyChanged(nameof(OverlayTimeout));
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
                    OnPropertyChanged(nameof(TextSize));
                }
            }
        }

        // Backing int color values (RGB stored as int in settings)
        public int TextColor
        {
            get => _settings.Properties.TextColor.Value;
            set
            {
                if (_settings.Properties.TextColor.Value != value)
                {
                    _settings.Properties.TextColor.Value = value;
                    NotifySettingsChanged();
                    OnPropertyChanged(nameof(TextColor));
                    OnPropertyChanged(nameof(TextColorColor));
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
                    OnPropertyChanged(nameof(BackgroundColor));
                    OnPropertyChanged(nameof(BackgroundColorColor));
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

        // Color typed properties for binding to ColorPickerButton (Windows.UI.Color)
        public Color TextColorColor
        {
            get => IntToColor(_settings.Properties.TextColor.Value);
            set
            {
                int intVal = ColorToInt(value);
                if (_settings.Properties.TextColor.Value != intVal)
                {
                    _settings.Properties.TextColor.Value = intVal;
                    NotifySettingsChanged();
                    OnPropertyChanged(nameof(TextColorColor));
                    OnPropertyChanged(nameof(TextColor));
                }
            }
        }

        public Color BackgroundColorColor
        {
            get => IntToColor(_settings.Properties.BackgroundColor.Value);
            set
            {
                int intVal = ColorToInt(value);
                if (_settings.Properties.BackgroundColor.Value != intVal)
                {
                    _settings.Properties.BackgroundColor.Value = intVal;
                    NotifySettingsChanged();
                    OnPropertyChanged(nameof(BackgroundColorColor));
                    OnPropertyChanged(nameof(BackgroundColor));
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

        private void NotifySettingsChanged()
        {
            // send IPC to runner
            _sendConfigMSG(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{  \"powertoys\": { {0}: {1} } }",
                    ModuleName,
                    JsonSerializer.Serialize(_settings)));

            // save locally
            _settingsUtils.SaveSettings(_settings.ToJsonString(), ModuleName);
        }

        public void RefreshEnabledState()
        {
            OnPropertyChanged(nameof(IsEnabled));
        }
    }
}
