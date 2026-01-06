// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.SerializationContext;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Newtonsoft.Json.Linq;
using PowerToys.GPOWrapper;
using Settings.UI.Library;
using Settings.UI.Library.Helpers;
using Windows.Storage;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class LightSwitchViewModel : PageViewModelBase
    {
        protected override string ModuleName => LightSwitchSettings.ModuleName;

        private Func<string, int> SendConfigMSG { get; }

        private GeneralSettings GeneralSettingsConfig { get; set; }

        public ObservableCollection<SearchLocation> SearchLocations { get; } = new();

        public LightSwitchViewModel(ISettingsRepository<GeneralSettings> settingsRepository, LightSwitchSettings initialSettings = null, Func<string, int> ipcMSGCallBackFunc = null)
        {
            ArgumentNullException.ThrowIfNull(settingsRepository);
            GeneralSettingsConfig = settingsRepository.SettingsConfig;
            InitializeEnabledValue();

            _moduleSettings = initialSettings ?? new LightSwitchSettings();
            SendConfigMSG = ipcMSGCallBackFunc;

            ForceLightCommand = new RelayCommand(ForceLightNow);
            ForceDarkCommand = new RelayCommand(ForceDarkNow);

            AvailableScheduleModes = new ObservableCollection<string>
            {
                "Off",
                "FixedHours",
                "SunsetToSunrise",
                "FollowNightLight",
            };

            _toggleThemeHotkey = _moduleSettings.Properties.ToggleThemeHotkey.Value;
            PropertyChanged += WallpaperPath_Changed;
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
            OnPropertyChanged(nameof(IsWallpaperEnabled));
            OnPropertyChanged(nameof(WallpaperPathLight));
            OnPropertyChanged(nameof(WallpaperPathDark));
            OnPropertyChanged(nameof(WallpaperStyleLight));
            OnPropertyChanged(nameof(WallpaperStyleDark));
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

        public bool IsWallpaperEnabled
        {
            get
            {
                return ModuleSettings.Properties.WallpaperEnabled.Value;
            }

            set
            {
                if (ModuleSettings.Properties.WallpaperEnabled.Value != value)
                {
                    ModuleSettings.Properties.WallpaperEnabled.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool IsVirtualDesktopEnabled
        {
            get
            {
                return ModuleSettings.Properties.WallpaperVirtualDesktopEnabled.Value;
            }

            set
            {
                if (ModuleSettings.Properties.WallpaperVirtualDesktopEnabled.Value != value)
                {
                    ModuleSettings.Properties.WallpaperVirtualDesktopEnabled.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string WallpaperPathLight
        {
            get
            {
                return ModuleSettings.Properties.WallpaperPathLight.Value;
            }

            set
            {
                if (ModuleSettings.Properties.WallpaperPathLight.Value != value)
                {
                    ModuleSettings.Properties.WallpaperPathLight.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string WallpaperPathDark
        {
            get
            {
                return ModuleSettings.Properties.WallpaperPathDark.Value;
            }

            set
            {
                if (ModuleSettings.Properties.WallpaperPathDark.Value != value)
                {
                    ModuleSettings.Properties.WallpaperPathDark.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool IsLightWallpaperValid
        {
            get => _isLightWallpaperValid;

            set
            {
                if (_isLightWallpaperValid != value)
                {
                    _isLightWallpaperValid = value;
                }
            }
        }

        public bool IsDarkWallpaperValid
        {
            get => _isDarkWallpaperValid;
            set
            {
                if (_isDarkWallpaperValid != value)
                {
                    _isDarkWallpaperValid = value;
                }
            }
        }

        public ImageSource WallpaperSourceLight
        {
            get => _wallpaperSourceLight;
            set
            {
                if (_wallpaperSourceLight != value)
                {
                    _wallpaperSourceLight = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public ImageSource WallpaperSourceDark
        {
            get => _wallpaperSourceDark;
            set
            {
                if (_wallpaperSourceDark != value)
                {
                    _wallpaperSourceDark = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public int WallpaperStyleLight
        {
            get => ModuleSettings.Properties.WallpaperStyleLight.Value;
            set
            {
                if (ModuleSettings.Properties.WallpaperStyleLight.Value != value)
                {
                    ModuleSettings.Properties.WallpaperStyleLight.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public int WallpaperStyleDark
        {
            get => ModuleSettings.Properties.WallpaperStyleDark.Value;
            set
            {
                if (ModuleSettings.Properties.WallpaperStyleDark.Value != value)
                {
                    ModuleSettings.Properties.WallpaperStyleDark.Value = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public static void DeleteFile(string path)
        {
            // Prevent attackers from damaging files through specially crafted JSON
            var dataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Microsoft\\PowerToys\\LightSwitch";
            if (!string.IsNullOrEmpty(path) && path.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    File.Delete(path);
                }
                catch (Exception)
                {
                }
            }
        }

        private async void WallpaperPath_Changed(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(WallpaperPathLight))
            {
                var lightImage = new BitmapImage();
                try
                {
                    var lightFile = await StorageFile.GetFileFromPathAsync(WallpaperPathLight);
                    await lightImage.SetSourceAsync(await lightFile.OpenReadAsync()); // thrown here when the image is invalid
                    WallpaperSourceLight = lightImage;
                    IsLightWallpaperValid = true;
                }
                catch (Exception)
                {
                    DeleteFile(WallpaperPathLight);
                    WallpaperPathLight = null;
                    IsLightWallpaperValid = false;
                    WallpaperSourceLight = null;
                    IsWallpaperEnabled = false;
                }
            }
            else if (e.PropertyName == nameof(WallpaperPathDark))
            {
                var darkImage = new BitmapImage();
                try
                {
                    var darkFile = await StorageFile.GetFileFromPathAsync(WallpaperPathDark);
                    await darkImage.SetSourceAsync(await darkFile.OpenReadAsync());
                    WallpaperSourceDark = darkImage;
                    IsDarkWallpaperValid = true;
                }
                catch (Exception)
                {
                    DeleteFile(WallpaperPathDark);
                    WallpaperPathDark = null;
                    IsDarkWallpaperValid = false;
                    WallpaperSourceDark = null;
                    IsWallpaperEnabled = false;
                }
            }
        }

        private int GetRegistryBuildNumber()
        {
            var value = Win32.Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion", "CurrentBuildNumber", string.Empty);
#pragma warning disable CA1305
            return int.Parse(value as string);
#pragma warning restore CA1305
        }

        public bool Is24H2OrLater
        {
            get => GetRegistryBuildNumber() > 26100;
        }

        private bool _enabledStateIsGPOConfigured;
        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private LightSwitchSettings _moduleSettings;
        private bool _isEnabled;
        private HotkeySettings _toggleThemeHotkey;
        private TimeSpan? _sunriseTimeSpan;
        private TimeSpan? _sunsetTimeSpan;
        private bool _isLightWallpaperValid;
        private bool _isDarkWallpaperValid;
        private ImageSource _wallpaperSourceLight;
        private ImageSource _wallpaperSourceDark;

        public ICommand ForceLightCommand { get; }

        public ICommand ForceDarkCommand { get; }
    }
}
