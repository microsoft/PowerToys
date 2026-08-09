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
using Settings.UI.Library.Enumerations;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class MeasureToolViewModel : PageViewModelBase
    {
        private const int UnitsOfMeasureItemCount = 4;
        private const int MeasureStyleItemCount = 5;
        private const int ToolbarPositionItemCount = 6;
        private const int DefaultToolbarPositionIndex = 1;

        protected override string ModuleName => MeasureToolSettings.ModuleName;

        private SettingsUtils SettingsUtils { get; set; }

        private GeneralSettings GeneralSettingsConfig { get; set; }

        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private bool _enabledStateIsGPOConfigured;
        private bool _isEnabled;

        private MeasureToolSettings Settings { get; set; }

        public MeasureToolViewModel(SettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, ISettingsRepository<MeasureToolSettings> measureToolSettingsRepository, Func<string, int> ipcMSGCallBackFunc)
        {
            SettingsUtils = settingsUtils;

            ArgumentNullException.ThrowIfNull(settingsRepository);

            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            InitializeEnabledValue();

            ArgumentNullException.ThrowIfNull(measureToolSettingsRepository);

            Settings = measureToolSettingsRepository.SettingsConfig;
            NormalizeToolbarPosition(persistCorrection: true);

            SendConfigMSG = ipcMSGCallBackFunc;
        }

        private void InitializeEnabledValue()
        {
            _enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredScreenRulerEnabledValue();
            if (_enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _enabledStateIsGPOConfigured = true;
                _isEnabled = _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _isEnabled = GeneralSettingsConfig.Enabled.MeasureTool;
            }
        }

        public override Dictionary<string, HotkeySettings[]> GetAllHotkeySettings()
        {
            var hotkeysDict = new Dictionary<string, HotkeySettings[]>
            {
                [ModuleName] = [ActivationShortcut],
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

                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    GeneralSettingsConfig.Enabled.MeasureTool = value;
                    OnPropertyChanged(nameof(IsEnabled));

                    OutGoingGeneralSettings outgoing = new OutGoingGeneralSettings(GeneralSettingsConfig);
                    SendConfigMSG(outgoing.ToString());

                    NotifyPropertyChanged();
                    NotifyPropertyChanged(nameof(ShowContinuousCaptureWarning));
                }
            }
        }

        public bool IsEnabledGpoConfigured
        {
            get => _enabledStateIsGPOConfigured;
        }

        public bool ContinuousCapture
        {
            get
            {
                return Settings.Properties.ContinuousCapture;
            }

            set
            {
                if (Settings.Properties.ContinuousCapture != value)
                {
                    Settings.Properties.ContinuousCapture = value;
                    NotifyPropertyChanged();
                    NotifyPropertyChanged(nameof(ShowContinuousCaptureWarning));
                }
            }
        }

        public bool DrawFeetOnCross
        {
            get
            {
                return Settings.Properties.DrawFeetOnCross;
            }

            set
            {
                if (Settings.Properties.DrawFeetOnCross != value)
                {
                    Settings.Properties.DrawFeetOnCross = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string CrossColor
        {
            get
            {
                return Settings.Properties.MeasureCrossColor.Value;
            }

            set
            {
                value = (value != null) ? SettingsUtilities.ToRGBHex(value) : "#FF4500";
                if (!value.Equals(Settings.Properties.MeasureCrossColor.Value, StringComparison.OrdinalIgnoreCase))
                {
                    Settings.Properties.MeasureCrossColor.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool PerColorChannelEdgeDetection
        {
            get
            {
                return Settings.Properties.PerColorChannelEdgeDetection;
            }

            set
            {
                if (Settings.Properties.PerColorChannelEdgeDetection != value)
                {
                    Settings.Properties.PerColorChannelEdgeDetection = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public int UnitsOfMeasure
        {
            get
            {
                return NormalizeSelectedIndex(Settings.Properties.UnitsOfMeasure.Value, UnitsOfMeasureItemCount);
            }

            set
            {
                int normalizedValue = NormalizeSelectedIndex(value, UnitsOfMeasureItemCount);
                if (Settings.Properties.UnitsOfMeasure.Value != normalizedValue)
                {
                    Settings.Properties.UnitsOfMeasure.Value = normalizedValue;
                    NotifyPropertyChanged();
                }
            }
        }

        public int PixelTolerance
        {
            get
            {
                return Settings.Properties.PixelTolerance.Value;
            }

            set
            {
                if (Settings.Properties.PixelTolerance.Value != value)
                {
                    Settings.Properties.PixelTolerance.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public HotkeySettings ActivationShortcut
        {
            get
            {
                return Settings.Properties.ActivationShortcut;
            }

            set
            {
                if (Settings.Properties.ActivationShortcut != value)
                {
                    Settings.Properties.ActivationShortcut = value ?? Settings.Properties.DefaultActivationShortcut;

                    NotifyPropertyChanged();

                    SendConfigMSG(
                         string.Format(
                         CultureInfo.InvariantCulture,
                         "{{ \"powertoys\": {{ \"{0}\": {1} }} }}",
                         MeasureToolSettings.ModuleName,
                         JsonSerializer.Serialize(Settings, SourceGenerationContextContext.Default.MeasureToolSettings)));
                }
            }
        }

        public int DefaultMeasureStyle
        {
            get
            {
                return NormalizeSelectedIndex(Settings.Properties.DefaultMeasureStyle.Value, MeasureStyleItemCount);
            }

            set
            {
                int normalizedValue = NormalizeSelectedIndex(value, MeasureStyleItemCount);
                if (Settings.Properties.DefaultMeasureStyle.Value != normalizedValue)
                {
                    Settings.Properties.DefaultMeasureStyle.Value = normalizedValue;
                    NotifyPropertyChanged();
                }
            }
        }

        public int ToolbarPosition
        {
            get
            {
                return NormalizeToolbarPosition(persistCorrection: false);
            }

            set
            {
                int normalizedIndex = NormalizeToolbarPositionIndex(value);
                int persistedValue = (int)GetToolbarPositionFromSelectedIndex(normalizedIndex);
                if (Settings.Properties.ToolbarPosition.Value != persistedValue || value != normalizedIndex)
                {
                    Settings.Properties.ToolbarPosition.Value = persistedValue;
                    NotifyPropertyChanged();
                }
            }
        }

        private int NormalizeToolbarPosition(bool persistCorrection)
        {
            int storedValue = Settings.Properties.ToolbarPosition.Value;
            MeasureToolToolbarPosition normalizedPosition = MeasureToolToolbarPlacement.Normalize(storedValue);
            int normalizedValue = (int)normalizedPosition;
            if (storedValue != normalizedValue)
            {
                Settings.Properties.ToolbarPosition.Value = normalizedValue;
                if (persistCorrection)
                {
                    SettingsUtils.SaveSettings(Settings.ToJsonString(), MeasureToolSettings.ModuleName);
                }
            }

            return GetSelectedIndexFromToolbarPosition(normalizedPosition);
        }

        private static int NormalizeToolbarPositionIndex(int value)
        {
            return value >= 0 && value < ToolbarPositionItemCount ? value : DefaultToolbarPositionIndex;
        }

        private static MeasureToolToolbarPosition GetToolbarPositionFromSelectedIndex(int value) => value switch
        {
            0 => MeasureToolToolbarPosition.TopLeft,
            1 => MeasureToolToolbarPosition.TopCenter,
            2 => MeasureToolToolbarPosition.TopRight,
            3 => MeasureToolToolbarPosition.BottomLeft,
            4 => MeasureToolToolbarPosition.BottomCenter,
            5 => MeasureToolToolbarPosition.BottomRight,
            _ => MeasureToolToolbarPosition.TopCenter,
        };

        private static int GetSelectedIndexFromToolbarPosition(MeasureToolToolbarPosition value) => value switch
        {
            MeasureToolToolbarPosition.TopLeft => 0,
            MeasureToolToolbarPosition.TopCenter => 1,
            MeasureToolToolbarPosition.TopRight => 2,
            MeasureToolToolbarPosition.BottomLeft => 3,
            MeasureToolToolbarPosition.BottomCenter => 4,
            MeasureToolToolbarPosition.BottomRight => 5,
            _ => DefaultToolbarPositionIndex,
        };

        private static int NormalizeSelectedIndex(int value, int itemCount)
        {
            return value >= 0 && value < itemCount ? value : 0;
        }

        public void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
        {
            OnPropertyChanged(propertyName);
            if (propertyName == nameof(ShowContinuousCaptureWarning))
            {
                // Don't trigger a settings update if the changed property is for visual notification.
                return;
            }

            SettingsUtils.SaveSettings(Settings.ToJsonString(), MeasureToolSettings.ModuleName);
        }

        public void RefreshEnabledState()
        {
            InitializeEnabledValue();
            OnPropertyChanged(nameof(IsEnabled));
            OnPropertyChanged(nameof(ShowContinuousCaptureWarning));
        }

        public bool ShowContinuousCaptureWarning
        {
            get => IsEnabled && ContinuousCapture;
        }

        private Func<string, int> SendConfigMSG { get; }
    }
}
