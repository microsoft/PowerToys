// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
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
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Services;
using Settings.UI.Library;
using Settings.UI.Library.Helpers;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class LightSwitchViewModel : PageViewModelBase
    {
        protected override string ModuleName => LightSwitchSettings.ModuleName;

        private Func<string, int> SendConfigMSG { get; }

        public ObservableCollection<SearchLocation> SearchLocations { get; } = new();

        public LightSwitchViewModel(LightSwitchSettings? initialSettings = null, Func<string, int>? ipcMSGCallBackFunc = null)
        {
            _moduleSettings = initialSettings ?? new LightSwitchSettings();
            SendConfigMSG = ipcMSGCallBackFunc ?? (_ => 0);

            ForceLightCommand = new RelayCommand(ForceLightNow);
            ForceDarkCommand = new RelayCommand(ForceDarkNow);

            AvailableScheduleModes = new ObservableCollection<string>
            {
                "Off",
                "FixedHours",
                "SunsetToSunrise",
                "FollowNightLight",
            };

            // Load PowerDisplay profiles
            LoadPowerDisplayProfiles();

            // Check if PowerDisplay is enabled
            CheckPowerDisplayEnabled();
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

        private void SaveSettings()
        {
            SendConfigMSG(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{{ \"powertoys\": {{ \"{0}\": {1} }} }}",
                    LightSwitchSettings.ModuleName,
                    JsonSerializer.Serialize(_moduleSettings, SourceGenerationContextContext.Default.LightSwitchSettings)));
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

        private SearchLocation? _selectedSearchLocation;

        public SearchLocation? SelectedCity
        {
            get => _selectedSearchLocation;
            set
            {
                if (_selectedSearchLocation != value)
                {
                    _selectedSearchLocation = value;
                    NotifyPropertyChanged();

                    if (_selectedSearchLocation != null)
                    {
                        UpdateSunTimes(_selectedSearchLocation.Latitude, _selectedSearchLocation.Longitude, _selectedSearchLocation.City);
                    }
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

        private double _locationPanelLatitude;
        private double _locationPanelLongitude;

        public double LocationPanelLatitude
        {
            get => _locationPanelLatitude;
            set
            {
                if (_locationPanelLatitude != value)
                {
                    _locationPanelLatitude = value;
                    NotifyPropertyChanged();
                    NotifyPropertyChanged(nameof(LocationPanelLightTime));
                }
            }
        }

        public double LocationPanelLongitude
        {
            get => _locationPanelLongitude;
            set
            {
                if (_locationPanelLongitude != value)
                {
                    _locationPanelLongitude = value;
                    NotifyPropertyChanged();
                }
            }
        }

        private int _locationPanelLightTime;
        private int _locationPanelDarkTime;

        public int LocationPanelLightTimeMinutes
        {
            get => _locationPanelLightTime;
            set
            {
                if (_locationPanelLightTime != value)
                {
                    _locationPanelLightTime = value;
                    NotifyPropertyChanged();
                    NotifyPropertyChanged(nameof(LocationPanelLightTime));
                }
            }
        }

        public int LocationPanelDarkTimeMinutes
        {
            get => _locationPanelDarkTime;
            set
            {
                if (_locationPanelDarkTime != value)
                {
                    _locationPanelDarkTime = value;
                    NotifyPropertyChanged();
                    NotifyPropertyChanged(nameof(LocationPanelDarkTime));
                }
            }
        }

        public TimeSpan LocationPanelLightTime => TimeSpan.FromMinutes(_locationPanelLightTime);

        public TimeSpan LocationPanelDarkTime => TimeSpan.FromMinutes(_locationPanelDarkTime);

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

        public void NotifyPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            Logger.LogInfo($"Changed the property {propertyName}");
            OnPropertyChanged(propertyName);
        }

        // PowerDisplay Integration Properties and Methods
        public ObservableCollection<PowerDisplayProfile> AvailableProfiles
        {
            get => _availableProfiles;
            set
            {
                if (_availableProfiles != value)
                {
                    _availableProfiles = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool IsPowerDisplayEnabled
        {
            get => _isPowerDisplayEnabled;
            set
            {
                if (_isPowerDisplayEnabled != value)
                {
                    _isPowerDisplayEnabled = value;
                    NotifyPropertyChanged();
                    NotifyPropertyChanged(nameof(ShowPowerDisplayDisabledWarning));
                }
            }
        }

        public bool ShowPowerDisplayDisabledWarning => !IsPowerDisplayEnabled;

        public bool EnableDarkModeProfile
        {
            get => ModuleSettings.Properties.EnableDarkModeProfile.Value;
            set
            {
                if (ModuleSettings.Properties.EnableDarkModeProfile.Value != value)
                {
                    ModuleSettings.Properties.EnableDarkModeProfile.Value = value;
                    NotifyPropertyChanged();
                    NotifyPropertyChanged(nameof(ShowPowerDisplayDisabledWarning));
                    SaveSettings();
                }
            }
        }

        public bool EnableLightModeProfile
        {
            get => ModuleSettings.Properties.EnableLightModeProfile.Value;
            set
            {
                if (ModuleSettings.Properties.EnableLightModeProfile.Value != value)
                {
                    ModuleSettings.Properties.EnableLightModeProfile.Value = value;
                    NotifyPropertyChanged();
                    NotifyPropertyChanged(nameof(ShowPowerDisplayDisabledWarning));
                    SaveSettings();
                }
            }
        }

        public PowerDisplayProfile? SelectedDarkModeProfile
        {
            get => _selectedDarkModeProfile;
            set
            {
                if (_selectedDarkModeProfile != value)
                {
                    _selectedDarkModeProfile = value;

                    // Sync with the string property stored in settings
                    var newProfileName = value?.Name ?? string.Empty;
                    if (ModuleSettings.Properties.DarkModeProfile.Value != newProfileName)
                    {
                        ModuleSettings.Properties.DarkModeProfile.Value = newProfileName;
                        SaveSettings();
                    }

                    NotifyPropertyChanged();
                }
            }
        }

        public PowerDisplayProfile? SelectedLightModeProfile
        {
            get => _selectedLightModeProfile;
            set
            {
                if (_selectedLightModeProfile != value)
                {
                    _selectedLightModeProfile = value;

                    // Sync with the string property stored in settings
                    var newProfileName = value?.Name ?? string.Empty;
                    if (ModuleSettings.Properties.LightModeProfile.Value != newProfileName)
                    {
                        ModuleSettings.Properties.LightModeProfile.Value = newProfileName;
                        SaveSettings();
                    }

                    NotifyPropertyChanged();
                }
            }
        }

        // Legacy string properties for backwards compatibility with settings persistence
        public string DarkModeProfile
        {
            get => ModuleSettings.Properties.DarkModeProfile.Value;
            set
            {
                if (ModuleSettings.Properties.DarkModeProfile.Value != value)
                {
                    ModuleSettings.Properties.DarkModeProfile.Value = value;

                    // Sync with the object property
                    UpdateSelectedProfileFromName(value, isDarkMode: true);

                    NotifyPropertyChanged();
                }
            }
        }

        public string LightModeProfile
        {
            get => ModuleSettings.Properties.LightModeProfile.Value;
            set
            {
                if (ModuleSettings.Properties.LightModeProfile.Value != value)
                {
                    ModuleSettings.Properties.LightModeProfile.Value = value;

                    // Sync with the object property
                    UpdateSelectedProfileFromName(value, isDarkMode: false);

                    NotifyPropertyChanged();
                }
            }
        }

        private void LoadPowerDisplayProfiles()
        {
            try
            {
                var profilesData = ProfileService.LoadProfiles();

                AvailableProfiles.Clear();

                foreach (var profile in profilesData.Profiles)
                {
                    AvailableProfiles.Add(profile);
                }

                Logger.LogInfo($"Loaded {profilesData.Profiles.Count} PowerDisplay profiles");

                // Sync selected profiles from settings
                UpdateSelectedProfileFromName(ModuleSettings.Properties.DarkModeProfile.Value, isDarkMode: true);
                UpdateSelectedProfileFromName(ModuleSettings.Properties.LightModeProfile.Value, isDarkMode: false);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load PowerDisplay profiles: {ex.Message}");
                AvailableProfiles.Clear();
            }
        }

        /// <summary>
        /// Helper method to sync the selected profile object from the profile name stored in settings.
        /// If the configured profile no longer exists, clears the selection and updates settings.
        /// </summary>
        private void UpdateSelectedProfileFromName(string profileName, bool isDarkMode)
        {
            PowerDisplayProfile? matchingProfile = null;

            if (!string.IsNullOrEmpty(profileName))
            {
                matchingProfile = AvailableProfiles.FirstOrDefault(p =>
                    p.Name.Equals(profileName, StringComparison.OrdinalIgnoreCase));

                // If the configured profile no longer exists, clear it from settings
                if (matchingProfile == null)
                {
                    Logger.LogWarning($"Configured {(isDarkMode ? "dark" : "light")} mode profile '{profileName}' no longer exists, clearing selection");

                    if (isDarkMode)
                    {
                        ModuleSettings.Properties.DarkModeProfile.Value = string.Empty;
                    }
                    else
                    {
                        ModuleSettings.Properties.LightModeProfile.Value = string.Empty;
                    }

                    SaveSettings();
                }
            }

            if (isDarkMode)
            {
                if (_selectedDarkModeProfile != matchingProfile)
                {
                    _selectedDarkModeProfile = matchingProfile;
                    NotifyPropertyChanged(nameof(SelectedDarkModeProfile));
                }
            }
            else
            {
                if (_selectedLightModeProfile != matchingProfile)
                {
                    _selectedLightModeProfile = matchingProfile;
                    NotifyPropertyChanged(nameof(SelectedLightModeProfile));
                }
            }
        }

        private void CheckPowerDisplayEnabled()
        {
            try
            {
                var settingsUtils = SettingsUtils.Default;
                var generalSettings = settingsUtils.GetSettingsOrDefault<GeneralSettings>(string.Empty);
                IsPowerDisplayEnabled = generalSettings?.Enabled?.PowerDisplay ?? false;
                Logger.LogInfo($"PowerDisplay enabled status: {IsPowerDisplayEnabled}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to check PowerDisplay enabled status: {ex.Message}");
                IsPowerDisplayEnabled = false;
            }
        }

        public void RefreshPowerDisplayStatus()
        {
            CheckPowerDisplayEnabled();
            NotifyPropertyChanged(nameof(ShowPowerDisplayDisabledWarning));
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
            OnPropertyChanged(nameof(EnableDarkModeProfile));
            OnPropertyChanged(nameof(EnableLightModeProfile));
            OnPropertyChanged(nameof(DarkModeProfile));
            OnPropertyChanged(nameof(LightModeProfile));
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
                    : $"{Latitude}°,{Longitude}°";

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
        private TimeSpan? _sunriseTimeSpan;
        private TimeSpan? _sunsetTimeSpan;

        // PowerDisplay integration
        private ObservableCollection<PowerDisplayProfile> _availableProfiles = new ObservableCollection<PowerDisplayProfile>();
        private bool _isPowerDisplayEnabled;
        private PowerDisplayProfile? _selectedDarkModeProfile;
        private PowerDisplayProfile? _selectedLightModeProfile;

        public ICommand ForceLightCommand { get; }

        public ICommand ForceDarkCommand { get; }
    }
}
