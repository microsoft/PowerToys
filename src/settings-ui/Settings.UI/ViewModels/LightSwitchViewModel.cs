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
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.SerializationContext;
using Newtonsoft.Json.Linq;
using PowerDisplay.Models;
using PowerToys.GPOWrapper;
using Settings.UI.Library;
using Settings.UI.Library.Helpers;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class LightSwitchViewModel : PageViewModelBase
    {
        private bool _isProfilesLoading;

        protected override string ModuleName => LightSwitchSettings.ModuleName;

        private Func<string, int> SendConfigMSG { get; }

        private GeneralSettings GeneralSettingsConfig { get; set; }

        public ObservableCollection<SearchLocation> SearchLocations { get; } = new();

        public LightSwitchViewModel(ISettingsRepository<GeneralSettings> settingsRepository, LightSwitchSettings? initialSettings = null, Func<string, int>? ipcMSGCallBackFunc = null)
        {
            ArgumentNullException.ThrowIfNull(settingsRepository);
            GeneralSettingsConfig = settingsRepository.SettingsConfig;
            InitializeEnabledValue();

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

        private void InitializeEnabledValue()
        {
            _enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredLightSwitchEnabledValue();
            if (_enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _enabledStateIsGPOConfigured = true;
                _isEnabled = _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _isEnabled = GeneralSettingsConfig.Enabled.LightSwitch;
            }
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
                    GeneralSettingsConfig.Enabled.LightSwitch = value;
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

        public GpoRuleConfigured EnabledGPOConfiguration
        {
            get => _enabledGpoRuleConfiguration;
            set
            {
                if (_enabledGpoRuleConfiguration != value)
                {
                    _enabledGpoRuleConfiguration = value;
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
                    OnPropertyChanged(nameof(SunriseOffsetMin));
                    OnPropertyChanged(nameof(SunsetOffsetMin));

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
                    OnPropertyChanged(nameof(SunriseOffsetMax));
                    OnPropertyChanged(nameof(SunsetOffsetMax));

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
                    OnPropertyChanged(nameof(SunsetOffsetMin));
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
                    OnPropertyChanged(nameof(SunriseOffsetMax));
                }
            }
        }

        public int SunriseOffsetMin
        {
            get
            {
                // Minimum: don't let adjusted sunrise go before 00:00
                return -LightTime;
            }
        }

        public int SunriseOffsetMax
        {
            get
            {
                // Maximum: adjusted sunrise must stay before adjusted sunset
                int adjustedSunset = DarkTime + SunsetOffset;
                return Math.Max(0, adjustedSunset - LightTime - 1);
            }
        }

        public int SunsetOffsetMin
        {
            get
            {
                // Minimum: adjusted sunset must stay after adjusted sunrise
                int adjustedSunrise = LightTime + SunriseOffset;
                return Math.Min(0, adjustedSunrise - DarkTime + 1);
            }
        }

        public int SunsetOffsetMax
        {
            get
            {
                // Maximum: don't let adjusted sunset go past 23:59 (1439 minutes)
                return 1439 - DarkTime;
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
                    NotifyPropertyChanged(nameof(CanSelectPowerDisplayProfile));
                }
            }
        }

        public bool ShowPowerDisplayDisabledWarning => !IsPowerDisplayEnabled;

        public bool IsProfilesLoading
        {
            get => _isProfilesLoading;
            private set
            {
                if (_isProfilesLoading == value)
                {
                    return;
                }

                _isProfilesLoading = value;
                NotifyPropertyChanged(nameof(IsProfilesLoading));
                NotifyPropertyChanged(nameof(CanSelectPowerDisplayProfile));
            }
        }

        public bool CanSelectPowerDisplayProfile => IsPowerDisplayEnabled && !IsProfilesLoading;

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
            set => SetSelectedProfile(ref _selectedDarkModeProfile, value, isDarkMode: true, nameof(SelectedDarkModeProfile));
        }

        public PowerDisplayProfile? SelectedLightModeProfile
        {
            get => _selectedLightModeProfile;
            set => SetSelectedProfile(ref _selectedLightModeProfile, value, isDarkMode: false, nameof(SelectedLightModeProfile));
        }

        /// <summary>
        /// Backing setter for the two theme profile selections: stores the object, persists the
        /// canonical id while clearing any legacy name (saving only on change), and raises the
        /// change notification for <paramref name="propertyName"/>.
        /// </summary>
        private void SetSelectedProfile(ref PowerDisplayProfile? field, PowerDisplayProfile? value, bool isDarkMode, string propertyName)
        {
            if (field == value)
            {
                return;
            }

            field = value;

            if (_suppressProfileSelectionPersistence)
            {
                NotifyPropertyChanged(propertyName);
                return;
            }

            var newId = value?.Id ?? 0;
            var idProperty = isDarkMode
                ? ModuleSettings.Properties.DarkModeProfileId
                : ModuleSettings.Properties.LightModeProfileId;
            var legacyNameProperty = isDarkMode
                ? ModuleSettings.Properties.DarkModeProfile
                : ModuleSettings.Properties.LightModeProfile;

            if (LightSwitchProfileReferenceHelper.SetProfileId(
                idProperty,
                legacyNameProperty,
                newId))
            {
                SaveSettings();
            }

            NotifyPropertyChanged(propertyName);
        }

        public async Task InitializeProfilesAsync(CancellationToken cancellationToken = default)
        {
            if (IsProfilesLoading)
            {
                return;
            }

            IsProfilesLoading = true;
            _suppressProfileSelectionPersistence = true;
            try
            {
                var profilesData = await ProfileHelper.LoadProfilesAsync(cancellationToken);

                AvailableProfiles.Clear();
                foreach (var profile in profilesData.GetAssignedProfiles())
                {
                    AvailableProfiles.Add(profile);
                }

                SelectByStoredReference(isDarkMode: true);
                SelectByStoredReference(isDarkMode: false);
            }
            catch (Exception ex)
            {
                AvailableProfiles.Clear();
                SetSelectedProfilesWithoutPersisting(null, null);
                Logger.LogError($"Failed to load PowerDisplay profiles: {ex.Message}");
            }
            finally
            {
                _suppressProfileSelectionPersistence = false;
                IsProfilesLoading = false;
            }
        }

        private void SetSelectedProfilesWithoutPersisting(
            PowerDisplayProfile? darkProfile,
            PowerDisplayProfile? lightProfile)
        {
            _selectedDarkModeProfile = darkProfile;
            _selectedLightModeProfile = lightProfile;
            NotifyPropertyChanged(nameof(SelectedDarkModeProfile));
            NotifyPropertyChanged(nameof(SelectedLightModeProfile));
        }

        /// <summary>
        /// Selects the profile object for the given theme from settings by stored profile id. Zero
        /// or a missing id produces no selection.
        /// </summary>
        private void SelectByStoredReference(bool isDarkMode)
        {
            var storedId = isDarkMode
                ? ModuleSettings.Properties.DarkModeProfileId.Value
                : ModuleSettings.Properties.LightModeProfileId.Value;
            var match = storedId >= 1
                ? AvailableProfiles.FirstOrDefault(profile => profile.Id == storedId)
                : null;

            if (isDarkMode)
            {
                if (_selectedDarkModeProfile != match)
                {
                    _selectedDarkModeProfile = match;
                    NotifyPropertyChanged(nameof(SelectedDarkModeProfile));
                }
            }
            else
            {
                if (_selectedLightModeProfile != match)
                {
                    _selectedLightModeProfile = match;
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
        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private LightSwitchSettings _moduleSettings;
        private bool _isEnabled;
        private TimeSpan? _sunriseTimeSpan;
        private TimeSpan? _sunsetTimeSpan;

        // PowerDisplay integration
        private ObservableCollection<PowerDisplayProfile> _availableProfiles = new ObservableCollection<PowerDisplayProfile>();
        private bool _isPowerDisplayEnabled;
        private PowerDisplayProfile? _selectedDarkModeProfile;
        private PowerDisplayProfile? _selectedLightModeProfile;
        private bool _suppressProfileSelectionPersistence;

        public ICommand ForceLightCommand { get; }

        public ICommand ForceDarkCommand { get; }
    }
}
