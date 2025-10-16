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
using System.Windows.Input;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.SerializationContext;
using Newtonsoft.Json.Linq;
using Settings.UI.Library;
using Settings.UI.Library.Helpers;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class LightSwitchViewModel : PageViewModelBase
    {
        protected override string ModuleName => LightSwitchSettings.ModuleName;

        private Func<string, int> SendConfigMSG { get; }

        public ObservableCollection<SearchLocation> SearchLocations { get; } = new();

        public LightSwitchViewModel(LightSwitchSettings initialSettings = null, Func<string, int> ipcMSGCallBackFunc = null)
        {
            _moduleSettings = initialSettings ?? new LightSwitchSettings();
            SendConfigMSG = ipcMSGCallBackFunc;

            ForceLightCommand = new RelayCommand(ForceLightNow);
            ForceDarkCommand = new RelayCommand(ForceDarkNow);

            AvailableScheduleModes = new ObservableCollection<string>
            {
                "FixedHours",
                "SunsetToSunrise",
            };

            _toggleThemeHotkey = _moduleSettings.Properties.ToggleThemeHotkey.Value;
        }

        public override Dictionary<string, HotkeySettings[]> GetAllHotkeySettings()
        {
            var hotkeysDict = new Dictionary<string, HotkeySettings[]>
            {
                [ModuleName] = [ToggleThemeActivationShortcut],
            };

            return hotkeysDict;
        }

        private void ForceLightNow()
        {
            Logger.LogInfo("Sending custom action: forceLight");
            SendCustomAction("forceLight");
        }

        private void ForceDarkNow()
        {
            Logger.LogInfo("Sending custom action: forceDark");
            SendCustomAction("forceDark");
        }

        private void SendCustomAction(string actionName)
        {
            SendConfigMSG("{\"action\":{\"LightSwitch\":{\"action_name\":\"" + actionName + "\", \"value\":\"\"}}}");
        }

        public LightSwitchSettings ModuleSettings
        {
            get => _moduleSettings;
            set
            {
                if (_moduleSettings != value)
                {
                    _moduleSettings = value;

                    OnPropertyChanged(nameof(ModuleSettings));
                    RefreshModuleSettings();
                    RefreshEnabledState();
                }
            }
        }

        public bool IsEnabled
        {
            get
            {
                if (_enabledStateIsGPOConfigured)
                {
                    return _enabledGPOConfiguration;
                }
                else
                {
                    return _isEnabled;
                }
            }

            set
            {
                if (_isEnabled != value)
                {
                    if (_enabledStateIsGPOConfigured)
                    {
                        // If it's GPO configured, shouldn't be able to change this state.
                        return;
                    }

                    _isEnabled = value;

                    RefreshEnabledState();

                    NotifyPropertyChanged();
                }
            }
        }

        public bool IsEnabledGpoConfigured
        {
            get => _enabledStateIsGPOConfigured;
            set
            {
                if (_enabledStateIsGPOConfigured != value)
                {
                    _enabledStateIsGPOConfigured = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool EnabledGPOConfiguration
        {
            get => _enabledGPOConfiguration;
            set
            {
                if (_enabledGPOConfiguration != value)
                {
                    _enabledGPOConfiguration = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string ScheduleMode
        {
            get => ModuleSettings.Properties.ScheduleMode.Value;
            set
            {
                var oldMode = ModuleSettings.Properties.ScheduleMode.Value;
                if (ModuleSettings.Properties.ScheduleMode.Value != value)
                {
                    ModuleSettings.Properties.ScheduleMode.Value = value;
                    OnPropertyChanged(nameof(ScheduleMode));
                }

                if (ModuleSettings.Properties.ScheduleMode.Value == "FixedHours" && oldMode != "FixedHours")
                {
                    LightTime = 360;
                    DarkTime = 1080;
                    SunsetTimeSpan = null;
                    SunriseTimeSpan = null;

                    OnPropertyChanged(nameof(LightTimePickerValue));
                    OnPropertyChanged(nameof(DarkTimePickerValue));
                }

                if (ModuleSettings.Properties.ScheduleMode.Value == "SunsetToSunrise")
                {
                    if (ModuleSettings.Properties.Latitude != "0.0" && ModuleSettings.Properties.Longitude != "0.0")
                    {
                        double lat = double.Parse(ModuleSettings.Properties.Latitude.Value, CultureInfo.InvariantCulture);
                        double lon = double.Parse(ModuleSettings.Properties.Longitude.Value, CultureInfo.InvariantCulture);
                        UpdateSunTimes(lat, lon);
                    }
                }
            }
        }

        public ObservableCollection<string> AvailableScheduleModes { get; }

        public bool ChangeSystem
        {
            get => ModuleSettings.Properties.ChangeSystem.Value;
            set
            {
                if (ModuleSettings.Properties.ChangeSystem.Value != value)
                {
                    ModuleSettings.Properties.ChangeSystem.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool ChangeApps
        {
            get => ModuleSettings.Properties.ChangeApps.Value;
            set
            {
                if (ModuleSettings.Properties.ChangeApps.Value != value)
                {
                    ModuleSettings.Properties.ChangeApps.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public int LightTime
        {
            get => ModuleSettings.Properties.LightTime.Value;
            set
            {
                if (ModuleSettings.Properties.LightTime.Value != value)
                {
                    ModuleSettings.Properties.LightTime.Value = value;
                    NotifyPropertyChanged();

                    OnPropertyChanged(nameof(LightTimeTimeSpan));

                    if (ScheduleMode == "SunsetToSunrise")
                    {
                        SunriseTimeSpan = TimeSpan.FromMinutes(value);
                    }
                }
            }
        }

        public int DarkTime
        {
            get => ModuleSettings.Properties.DarkTime.Value;
            set
            {
                if (ModuleSettings.Properties.DarkTime.Value != value)
                {
                    ModuleSettings.Properties.DarkTime.Value = value;
                    NotifyPropertyChanged();

                    OnPropertyChanged(nameof(DarkTimeTimeSpan));

                    if (ScheduleMode == "SunsetToSunrise")
                    {
                        SunsetTimeSpan = TimeSpan.FromMinutes(value);
                    }
                }
            }
        }

        public int SunriseOffset
        {
            get => ModuleSettings.Properties.SunriseOffset.Value;
            set
            {
                if (ModuleSettings.Properties.SunriseOffset.Value != value)
                {
                    ModuleSettings.Properties.SunriseOffset.Value = value;
                    OnPropertyChanged(nameof(LightTimeTimeSpan));
                }
            }
        }

        public int SunsetOffset
        {
            get => ModuleSettings.Properties.SunsetOffset.Value;
            set
            {
                if (ModuleSettings.Properties.SunsetOffset.Value != value)
                {
                    ModuleSettings.Properties.SunsetOffset.Value = value;
                    OnPropertyChanged(nameof(DarkTimeTimeSpan));
                }
            }
        }

        // === Computed projections (OneWay bindings only) ===
        public TimeSpan LightTimeTimeSpan
        {
            get
            {
                if (ScheduleMode == "SunsetToSunrise")
                {
                    return TimeSpan.FromMinutes(LightTime + SunriseOffset);
                }
                else
                {
                    return TimeSpan.FromMinutes(LightTime);
                }
            }
        }

        public TimeSpan DarkTimeTimeSpan
        {
            get
            {
                if (ScheduleMode == "SunsetToSunrise")
                {
                    return TimeSpan.FromMinutes(DarkTime + SunsetOffset);
                }
                else
                {
                    return TimeSpan.FromMinutes(DarkTime);
                }
            }
        }

        // === Values to pass to timeline ===
        public TimeSpan? SunriseTimeSpan
        {
            get => _sunriseTimeSpan;
            set
            {
                if (_sunriseTimeSpan != value)
                {
                    _sunriseTimeSpan = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public TimeSpan? SunsetTimeSpan
        {
            get => _sunsetTimeSpan;
            set
            {
                if (_sunsetTimeSpan != value)
                {
                    _sunsetTimeSpan = value;
                    NotifyPropertyChanged();
                }
            }
        }

        // === Picker values (TwoWay binding targets for TimePickers) ===
        public TimeSpan LightTimePickerValue
        {
            get => TimeSpan.FromMinutes(LightTime);
            set => LightTime = (int)value.TotalMinutes;
        }

        public TimeSpan DarkTimePickerValue
        {
            get => TimeSpan.FromMinutes(DarkTime);
            set => DarkTime = (int)value.TotalMinutes;
        }

        public string Latitude
        {
            get => ModuleSettings.Properties.Latitude.Value;
            set
            {
                if (ModuleSettings.Properties.Latitude.Value != value)
                {
                    ModuleSettings.Properties.Latitude.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string Longitude
        {
            get => ModuleSettings.Properties.Longitude.Value;
            set
            {
                if (ModuleSettings.Properties.Longitude.Value != value)
                {
                    ModuleSettings.Properties.Longitude.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        private SearchLocation _selectedSearchLocation;

        public SearchLocation SelectedCity
        {
            get => _selectedSearchLocation;
            set
            {
                if (_selectedSearchLocation != value)
                {
                    _selectedSearchLocation = value;
                    NotifyPropertyChanged();

                    UpdateSunTimes(_selectedSearchLocation.Latitude, _selectedSearchLocation.Longitude, _selectedSearchLocation.City);
                }
            }
        }

        private string _syncButtonInformation = "Please sync your location";

        public string SyncButtonInformation
        {
            get => _syncButtonInformation;
            set
            {
                if (_syncButtonInformation != value)
                {
                    _syncButtonInformation = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public HotkeySettings ToggleThemeActivationShortcut
        {
            get => ModuleSettings.Properties.ToggleThemeHotkey.Value;

            set
            {
                if (value != ModuleSettings.Properties.ToggleThemeHotkey.Value)
                {
                    if (value == null)
                    {
                        ModuleSettings.Properties.ToggleThemeHotkey.Value = LightSwitchProperties.DefaultToggleThemeHotkey;
                    }
                    else
                    {
                        ModuleSettings.Properties.ToggleThemeHotkey.Value = value;
                    }

                    NotifyPropertyChanged();

                    SendConfigMSG(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "{{ \"powertoys\": {{ \"{0}\": {1} }} }}",
                            LightSwitchSettings.ModuleName,
                            JsonSerializer.Serialize(_moduleSettings, SourceGenerationContextContext.Default.LightSwitchSettings)));
                }
            }
        }

        public void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
        {
            Logger.LogInfo($"Changed the property {propertyName}");
            OnPropertyChanged(propertyName);
        }

        public void RefreshEnabledState()
        {
            OnPropertyChanged(nameof(IsEnabled));
        }

        public void RefreshModuleSettings()
        {
            OnPropertyChanged(nameof(ChangeSystem));
            OnPropertyChanged(nameof(ChangeApps));
            OnPropertyChanged(nameof(LightTime));
            OnPropertyChanged(nameof(DarkTime));
            OnPropertyChanged(nameof(SunriseOffset));
            OnPropertyChanged(nameof(SunsetOffset));
            OnPropertyChanged(nameof(Latitude));
            OnPropertyChanged(nameof(Longitude));
            OnPropertyChanged(nameof(ScheduleMode));
        }

        private void UpdateSunTimes(double latitude, double longitude, string city = "n/a")
        {
            SunTimes result = SunCalc.CalculateSunriseSunset(
                latitude,
                longitude,
                DateTime.Now.Year,
                DateTime.Now.Month,
                DateTime.Now.Day);

            LightTime = (result.SunriseHour * 60) + result.SunriseMinute;
            DarkTime = (result.SunsetHour * 60) + result.SunsetMinute;
            Latitude = latitude.ToString(CultureInfo.InvariantCulture);
            Longitude = longitude.ToString(CultureInfo.InvariantCulture);

            if (city != "n/a")
            {
                SyncButtonInformation = city;
            }
        }

        public void InitializeScheduleMode()
        {
            if (ScheduleMode == "SunsetToSunrise" &&
                double.TryParse(Latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out double savedLat) &&
                double.TryParse(Longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out double savedLng))
            {
                var match = SearchLocations.FirstOrDefault(c =>
                    Math.Abs(c.Latitude - savedLat) < 0.0001 &&
                    Math.Abs(c.Longitude - savedLng) < 0.0001);

                if (match != null)
                {
                    SelectedCity = match;
                }

                SyncButtonInformation = SelectedCity != null
                    ? SelectedCity.City
                    : $"{Latitude},{Longitude}";

                double lat = double.Parse(ModuleSettings.Properties.Latitude.Value, CultureInfo.InvariantCulture);
                double lon = double.Parse(ModuleSettings.Properties.Longitude.Value, CultureInfo.InvariantCulture);
                UpdateSunTimes(lat, lon);

                SunriseTimeSpan = TimeSpan.FromMinutes(LightTime);
                SunsetTimeSpan = TimeSpan.FromMinutes(DarkTime);
            }
        }

        private bool _enabledStateIsGPOConfigured;
        private bool _enabledGPOConfiguration;
        private LightSwitchSettings _moduleSettings;
        private bool _isEnabled;
        private HotkeySettings _toggleThemeHotkey;
        private TimeSpan? _sunriseTimeSpan;
        private TimeSpan? _sunsetTimeSpan;

        public ICommand ForceLightCommand { get; }

        public ICommand ForceDarkCommand { get; }
    }
}
