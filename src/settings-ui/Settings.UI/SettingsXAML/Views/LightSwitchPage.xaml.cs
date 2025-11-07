// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PowerToys.GPOWrapper;
using Settings.UI.Library;
using Settings.UI.Library.Helpers;
using Windows.Devices.Geolocation;
using Windows.Services.Maps;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class LightSwitchPage : Page
    {
        private readonly string _appName = "LightSwitch";
        private readonly SettingsUtils _settingsUtils;
        private readonly Func<string, int> _sendConfigMsg = ShellPage.SendDefaultIPCMessage;

        private readonly ISettingsRepository<GeneralSettings> _generalSettingsRepository;
        private readonly ISettingsRepository<LightSwitchSettings> _moduleSettingsRepository;

        private readonly IFileSystem _fileSystem;
        private readonly IFileSystemWatcher _fileSystemWatcher;
        private readonly DispatcherQueue _dispatcherQueue;
        private bool _suppressViewModelUpdates;
        private bool _suppressLatLonChange = true;
        private bool _latLoaded;
        private bool _lonLoaded;

        private LightSwitchViewModel ViewModel { get; set; }

        public LightSwitchPage()
        {
            _settingsUtils = new SettingsUtils();
            _sendConfigMsg = ShellPage.SendDefaultIPCMessage;

            _generalSettingsRepository = SettingsRepository<GeneralSettings>.GetInstance(_settingsUtils);
            _moduleSettingsRepository = SettingsRepository<LightSwitchSettings>.GetInstance(_settingsUtils);

            // Get settings from JSON (or defaults if JSON missing)
            var darkSettings = _moduleSettingsRepository.SettingsConfig;

            // Pass them into the ViewModel
            ViewModel = new LightSwitchViewModel(darkSettings, ShellPage.SendDefaultIPCMessage);
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            LoadSettings(_generalSettingsRepository, _moduleSettingsRepository);

            DataContext = ViewModel;

            var settingsPath = _settingsUtils.GetSettingsFilePath(_appName);

            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            _fileSystem = new FileSystem();

            _fileSystemWatcher = _fileSystem.FileSystemWatcher.New();
            _fileSystemWatcher.Path = _fileSystem.Path.GetDirectoryName(settingsPath);
            _fileSystemWatcher.Filter = _fileSystem.Path.GetFileName(settingsPath);
            _fileSystemWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime;
            _fileSystemWatcher.Changed += Settings_Changed;
            _fileSystemWatcher.EnableRaisingEvents = true;

            this.InitializeComponent();
            this.Loaded += LightSwitchPage_Loaded;
            this.Loaded += (s, e) => ViewModel.OnPageLoaded();
        }

        private void LightSwitchPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SearchLocations.Count == 0)
            {
                foreach (var city in SearchLocationLoader.GetAll())
                {
                    ViewModel.SearchLocations.Add(city);
                }
            }

            ViewModel.InitializeScheduleMode();
        }

        private async void GetGeoLocation_Click(object sender, RoutedEventArgs e)
        {
            LatitudeBox.IsEnabled = false;
            LongitudeBox.IsEnabled = false;
            SyncButton.IsEnabled = false;
            SyncLoader.IsActive = true;
            SyncLoader.Visibility = Visibility.Visible;
            LocationResultPanel.Visibility = Visibility.Collapsed;

            try
            {
                // Request access
                var accessStatus = await Geolocator.RequestAccessAsync();
                if (accessStatus != GeolocationAccessStatus.Allowed)
                {
                    // User denied location or it's not available
                    return;
                }

                var geolocator = new Geolocator { DesiredAccuracy = PositionAccuracy.Default };

                Geoposition pos = await geolocator.GetGeopositionAsync();

                double latitude = Math.Round(pos.Coordinate.Point.Position.Latitude);
                double longitude = Math.Round(pos.Coordinate.Point.Position.Longitude);
                ViewModel.Latitude = latitude.ToString(CultureInfo.InvariantCulture);
                ViewModel.Longitude = longitude.ToString(CultureInfo.InvariantCulture);

                // Since we use this mode, we can remove the selected city data.
                ViewModel.SelectedCity = null;

                // CityAutoSuggestBox.Text = string.Empty;
                ViewModel.SyncButtonInformation = $"{ViewModel.Latitude}°, {ViewModel.Longitude}°";

                _suppressLatLonChange = false;

                // ViewModel.CityTimesText = $"Sunrise: {result.SunriseHour}:{result.SunriseMinute:D2}\n" + $"Sunset: {result.SunsetHour}:{result.SunsetMinute:D2}";
                SyncButton.IsEnabled = true;
                SyncLoader.IsActive = false;
                SyncLoader.Visibility = Visibility.Collapsed;
                LocationDialog.IsPrimaryButtonEnabled = true;
                LatitudeBox.IsEnabled = true;
                LongitudeBox.IsEnabled = true;
                LocationResultPanel.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                SyncButton.IsEnabled = true;
                SyncLoader.IsActive = false;
                SyncLoader.Visibility = Visibility.Collapsed;
                LocationResultPanel.Visibility = Visibility.Collapsed;
                LatitudeBox.IsEnabled = true;
                LongitudeBox.IsEnabled = true;
                System.Diagnostics.Debug.WriteLine("Location error: " + ex.Message);
            }
        }

        private void LatLonBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_suppressLatLonChange)
            {
                return;
            }

            double latitude = LatitudeBox.Value;
            double longitude = LongitudeBox.Value;

            if (double.IsNaN(latitude) || double.IsNaN(longitude))
            {
                return;
            }

            double viewModelLatitude = double.TryParse(ViewModel.Latitude, out var lat) ? lat : 0.0;
            double viewModelLongitude = double.TryParse(ViewModel.Longitude, out var lon) ? lon : 0.0;

            if (latitude == viewModelLatitude && longitude == viewModelLongitude)
            {
                return;
            }

            ViewModel.Latitude = latitude.ToString(CultureInfo.InvariantCulture);
            ViewModel.Longitude = longitude.ToString(CultureInfo.InvariantCulture);
            ViewModel.SyncButtonInformation = $"{ViewModel.Latitude}°, {ViewModel.Longitude}°";

            var result = SunCalc.CalculateSunriseSunset(latitude, longitude, DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day);

            // Temporarily display these preview times
            ViewModel.LightTime = (result.SunriseHour * 60) + result.SunriseMinute;
            ViewModel.DarkTime = (result.SunsetHour * 60) + result.SunsetMinute;

            // Show the panel with these values
            LocationResultPanel.Visibility = Visibility.Visible;
            if (LocationDialog != null)
            {
                LocationDialog.IsPrimaryButtonEnabled = true;
            }
        }

        private void LocationDialog_PrimaryButtonClick(object sender, ContentDialogButtonClickEventArgs args)
        {
            /* if (ViewModel.ScheduleMode == "SunriseToSunsetUser")
            {
                ViewModel.SyncButtonInformation = ViewModel.SelectedCity.City;
            }
            else if (ViewModel.ScheduleMode == "SunriseToSunsetGeo")
            {
                ViewModel.SyncButtonInformation = $"{ViewModel.Latitude}°, {ViewModel.Longitude}°";
            } */

            SunriseModeChartState();
        }

        private void LocationDialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            LatitudeBox.Loaded += LatLonBox_Loaded;
            LongitudeBox.Loaded += LatLonBox_Loaded;
        }

        private void LatLonBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender as NumberBox == LatitudeBox)
            {
                _latLoaded = true;
            }
            else if (sender as NumberBox == LongitudeBox)
            {
                _lonLoaded = true;
            }

            if (_latLoaded && _lonLoaded)
            {
                _suppressLatLonChange = false;
            }
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (_suppressViewModelUpdates)
            {
                return;
            }

            if (e.PropertyName == "IsEnabled")
            {
                if (ViewModel.IsEnabled != _generalSettingsRepository.SettingsConfig.Enabled.LightSwitch)
                {
                    _generalSettingsRepository.SettingsConfig.Enabled.LightSwitch = ViewModel.IsEnabled;

                    var generalSettingsMessage = new OutGoingGeneralSettings(_generalSettingsRepository.SettingsConfig).ToString();
                    Logger.LogInfo($"Saved general settings from Light Switch page.");

                    _sendConfigMsg?.Invoke(generalSettingsMessage);
                }
            }
            else
            {
                if (ViewModel.ModuleSettings != null)
                {
                    SndLightSwitchSettings currentSettings = new(_moduleSettingsRepository.SettingsConfig);
                    SndModuleSettings<SndLightSwitchSettings> csIpcMessage = new(currentSettings);

                    SndLightSwitchSettings outSettings = new(ViewModel.ModuleSettings);
                    SndModuleSettings<SndLightSwitchSettings> outIpcMessage = new(outSettings);

                    string csMessage = csIpcMessage.ToJsonString();
                    string outMessage = outIpcMessage.ToJsonString();

                    if (!csMessage.Equals(outMessage, StringComparison.Ordinal))
                    {
                        Logger.LogInfo($"Saved Light Switch settings from Light Switch page.");

                        _sendConfigMsg?.Invoke(outMessage);
                    }
                }
            }
        }

        private void LoadSettings(ISettingsRepository<GeneralSettings> generalSettingsRepository, ISettingsRepository<LightSwitchSettings> moduleSettingsRepository)
        {
            if (generalSettingsRepository != null)
            {
                if (moduleSettingsRepository != null)
                {
                    UpdateViewModelSettings(moduleSettingsRepository.SettingsConfig, generalSettingsRepository.SettingsConfig);
                }
                else
                {
                    throw new ArgumentNullException(nameof(moduleSettingsRepository));
                }
            }
            else
            {
                throw new ArgumentNullException(nameof(generalSettingsRepository));
            }
        }

        private void UpdateViewModelSettings(LightSwitchSettings lightSwitchSettings, GeneralSettings generalSettings)
        {
            if (lightSwitchSettings != null)
            {
                if (generalSettings != null)
                {
                    ViewModel.IsEnabled = generalSettings.Enabled.LightSwitch;
                    ViewModel.ModuleSettings = (LightSwitchSettings)lightSwitchSettings.Clone();

                    UpdateEnabledState(generalSettings.Enabled.LightSwitch);
                }
                else
                {
                    throw new ArgumentNullException(nameof(generalSettings));
                }
            }
            else
            {
                throw new ArgumentNullException(nameof(lightSwitchSettings));
            }
        }

        private void Settings_Changed(object sender, FileSystemEventArgs e)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                _suppressViewModelUpdates = true;

                _moduleSettingsRepository.ReloadSettings();
                LoadSettings(_generalSettingsRepository, _moduleSettingsRepository);

                _suppressViewModelUpdates = false;
            });
        }

        private void UpdateEnabledState(bool recommendedState)
        {
            var enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredLightSwitchEnabledValue();

            if (enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                ViewModel.IsEnabledGpoConfigured = true;
                ViewModel.EnabledGPOConfiguration = enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                ViewModel.IsEnabled = recommendedState;
            }
        }

        private async void SyncLocationButton_Click(object sender, RoutedEventArgs e)
        {
            LocationDialog.IsPrimaryButtonEnabled = false;
            LocationResultPanel.Visibility = Visibility.Collapsed;
            await LocationDialog.ShowAsync();
        }

        private void CityAutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput && !string.IsNullOrWhiteSpace(sender.Text))
            {
                string query = sender.Text.ToLower(CultureInfo.CurrentCulture);

                // Filter your cities (assuming ViewModel.Cities is a List<City>)
                var filtered = ViewModel.SearchLocations
                    .Where(c =>
                        (c.City?.Contains(query, StringComparison.CurrentCultureIgnoreCase) ?? false) ||
                        (c.Country?.Contains(query, StringComparison.CurrentCultureIgnoreCase) ?? false))
                    .ToList();

                sender.ItemsSource = filtered;
            }
        }

        private void CityAutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (args.SelectedItem is SearchLocation location)
            {
                ViewModel.SelectedCity = location;

                // CityAutoSuggestBox.Text = $"{location.City}, {location.Country}";
                LocationDialog.IsPrimaryButtonEnabled = true;
                LocationResultPanel.Visibility = Visibility.Visible;
            }
        }

        private void ModeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (ViewModel.ScheduleMode)
            {
                case "FixedHours":
                    VisualStateManager.GoToState(this, "ManualState", true);
                    TimelineCard.Visibility = Visibility.Visible;
                    break;
                case "SunsetToSunrise":
                    VisualStateManager.GoToState(this, "SunsetToSunriseState", true);
                    SunriseModeChartState();
                    break;
                default:
                    VisualStateManager.GoToState(this, "OffState", true);
                    TimelineCard.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        private void SunriseModeChartState()
        {
            if (ViewModel.Latitude != "0.0" && ViewModel.Longitude != "0.0")
            {
                TimelineCard.Visibility = Visibility.Visible;
                LocationWarningBar.Visibility = Visibility.Collapsed;
            }
            else
            {
                TimelineCard.Visibility = Visibility.Collapsed;
                LocationWarningBar.Visibility = Visibility.Visible;
            }
        }
    }
}
