// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.SerializationContext;
using Settings.UI.Library;
using Settings.UI.Library.Helpers;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class LightSwitchViewModel : Observable
    {
        private Func<string, int> SendConfigMSG { get; }

        public ObservableCollection<City> Cities { get; } = new();

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
                if (ModuleSettings.Properties.ScheduleMode.Value != value)
                {
                    ModuleSettings.Properties.ScheduleMode.Value = value;
                    OnPropertyChanged(nameof(ScheduleMode));
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
                }
            }
        }

        public int Offset
        {
            get => ModuleSettings.Properties.Offset.Value;
            set
            {
                if (ModuleSettings.Properties.Offset.Value != value)
                {
                    ModuleSettings.Properties.Offset.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public TimeSpan LightTimeTimeSpan
        {
            get => TimeSpan.FromMinutes(LightTime);
            set
            {
                int minutes = (int)value.TotalMinutes;
                if (LightTime != minutes)
                {
                    LightTime = minutes;
                    NotifyPropertyChanged();
                    OnPropertyChanged(nameof(LightTimeTimeSpan));
                }
            }
        }

        public TimeSpan DarkTimeTimeSpan
        {
            get => TimeSpan.FromMinutes(DarkTime);
            set
            {
                int minutes = (int)value.TotalMinutes;
                if (DarkTime != minutes)
                {
                    DarkTime = minutes;
                    NotifyPropertyChanged();
                    OnPropertyChanged(nameof(DarkTimeTimeSpan));
                }
            }
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

        private City _selectedCity;

        public City SelectedCity
        {
            get => _selectedCity;
            set
            {
                if (_selectedCity != value)
                {
                    _selectedCity = value;
                    NotifyPropertyChanged();

                    UpdateSunTimesForSelectedCity();
                }
            }
        }

        private string _searchText = string.Empty;

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    NotifyPropertyChanged();

                    // optional: clear SelectedCity if text no longer matches
                    if (SelectedCity != null && !SelectedCity.Display.Equals(_searchText, StringComparison.OrdinalIgnoreCase))
                    {
                        SelectedCity = null;
                    }
                }
            }
        }

        private string _cityTimesText = "Please sync your location";

        public string CityTimesText
        {
            get => _cityTimesText;
            set
            {
                if (_cityTimesText != value)
                {
                    _cityTimesText = value;
                    NotifyPropertyChanged();
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
            get => _toggleThemeHotkey;

            set
            {
                if (value != _toggleThemeHotkey)
                {
                    if (value == null)
                    {
                        _toggleThemeHotkey = LightSwitchProperties.DefaultToggleThemeHotkey;
                    }
                    else
                    {
                        _toggleThemeHotkey = value;
                    }

                    _moduleSettings.Properties.ToggleThemeHotkey.Value = _toggleThemeHotkey;
                    NotifyPropertyChanged();

                    SendConfigMSG(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "{{ \"powertoys\": {{ \"{0}\": {1} }} }}",
                            LightSwitchSettings.ModuleName,
                            JsonSerializer.Serialize(_moduleSettings, (System.Text.Json.Serialization.Metadata.JsonTypeInfo<LightSwitchSettings>)SourceGenerationContextContext.Default.LightSwitchSettings)));
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
            OnPropertyChanged(nameof(Offset));
            OnPropertyChanged(nameof(Latitude));
            OnPropertyChanged(nameof(Longitude));
            OnPropertyChanged(nameof(ScheduleMode));
        }

        public void UpdateSunTimesForSelectedCity()
        {
            if (SelectedCity == null)
            {
                return;
            }

            SunTimes result = SunCalc.CalculateSunriseSunset(
                SelectedCity.Latitude,
                SelectedCity.Longitude,
                DateTime.Now.Year,
                DateTime.Now.Month,
                DateTime.Now.Day);

            LightTime = (result.SunriseHour * 60) + result.SunriseMinute;
            DarkTime = (result.SunsetHour * 60) + result.SunsetMinute;
            Latitude = SelectedCity.Latitude.ToString(CultureInfo.InvariantCulture);
            Longitude = SelectedCity.Longitude.ToString(CultureInfo.InvariantCulture);

            // CityTimesText = $"Sunrise: {result.SunriseHour}:{result.SunriseMinute:D2}\n" + $"Sunset: {result.SunsetHour}:{result.SunsetMinute:D2}";
            SyncButtonInformation = SelectedCity.Name;
            NotifyPropertyChanged();
        }

        private bool _enabledStateIsGPOConfigured;
        private bool _enabledGPOConfiguration;
        private LightSwitchSettings _moduleSettings;
        private bool _isEnabled;
        private HotkeySettings _toggleThemeHotkey;

        public ICommand ForceLightCommand { get; }

        public ICommand ForceDarkCommand { get; }
    }
}
