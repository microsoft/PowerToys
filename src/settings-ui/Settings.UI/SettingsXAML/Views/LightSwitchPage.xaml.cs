// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PowerToys.GPOWrapper;
using Settings.UI.Library;
using Windows.Devices.Geolocation;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class LightSwitchPage : NavigablePage, IRefreshablePage
    {
        private readonly string appName = "LightSwitch";
        private readonly SettingsUtils settingsUtils;
        private readonly Func<string, int> sendConfigMsg = ShellPage.SendDefaultIPCMessage;

        private readonly SettingsRepository<GeneralSettings> generalSettingsRepository;
        private readonly SettingsRepository<LightSwitchSettings> moduleSettingsRepository;

        private readonly IFileSystem fileSystem;
        private readonly IFileSystemWatcher fileSystemWatcher;
        private readonly DispatcherQueue dispatcherQueue;
        private bool suppressViewModelUpdates;

        private LightSwitchViewModel ViewModel { get; set; }

        public LightSwitchPage()
        {
            this.settingsUtils = new SettingsUtils();
            this.sendConfigMsg = ShellPage.SendDefaultIPCMessage;

            this.generalSettingsRepository = SettingsRepository<GeneralSettings>.GetInstance(this.settingsUtils);
            this.moduleSettingsRepository = SettingsRepository<LightSwitchSettings>.GetInstance(this.settingsUtils);

            // Get settings from JSON (or defaults if JSON missing)
            var darkSettings = this.moduleSettingsRepository.SettingsConfig;

            // Pass them into the ViewModel
            this.ViewModel = new LightSwitchViewModel(darkSettings, this.sendConfigMsg);
            this.ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            this.LoadSettings(this.generalSettingsRepository, this.moduleSettingsRepository);

            DataContext = this.ViewModel;

            var settingsPath = this.settingsUtils.GetSettingsFilePath(this.appName);

            this.dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            this.fileSystem = new FileSystem();

            this.fileSystemWatcher = this.fileSystem.FileSystemWatcher.New();
            this.fileSystemWatcher.Path = this.fileSystem.Path.GetDirectoryName(settingsPath);
            this.fileSystemWatcher.Filter = this.fileSystem.Path.GetFileName(settingsPath);
            this.fileSystemWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime;
            this.fileSystemWatcher.Changed += Settings_Changed;
            this.fileSystemWatcher.EnableRaisingEvents = true;

            this.InitializeComponent();
            Loaded += LightSwitchPage_Loaded;
            Loaded += (s, e) => this.ViewModel.OnPageLoaded();
        }

        public void RefreshEnabledState()
        {
            this.ViewModel.RefreshEnabledState();
        }

        private void LightSwitchPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (this.ViewModel.SearchLocations.Count == 0)
            {
                foreach (var city in SearchLocationLoader.GetAll())
                {
                    this.ViewModel.SearchLocations.Add(city);
                }
            }

            this.ViewModel.InitializeScheduleMode();
        }

        private async void GetGeoLocation_Click(object sender, RoutedEventArgs e)
        {
            this.LatitudeBox.IsEnabled = false;
            this.LongitudeBox.IsEnabled = false;
            this.SyncButton.IsEnabled = false;
            this.SyncLoader.IsActive = true;
            this.SyncLoader.Visibility = Visibility.Visible;
            this.LocationResultPanel.Visibility = Visibility.Collapsed;

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

                ViewModel.LocationPanelLatitude = latitude;
                ViewModel.LocationPanelLongitude = longitude;

                var result = SunCalc.CalculateSunriseSunset(latitude, longitude, DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day);

                this.ViewModel.LocationPanelLightTimeMinutes = (result.SunriseHour * 60) + result.SunriseMinute;
                this.ViewModel.LocationPanelDarkTimeMinutes = (result.SunsetHour * 60) + result.SunsetMinute;

                // Since we use this mode, we can remove the selected city data.
                this.ViewModel.SelectedCity = null;

                // ViewModel.CityTimesText = $"Sunrise: {result.SunriseHour}:{result.SunriseMinute:D2}\n" + $"Sunset: {result.SunsetHour}:{result.SunsetMinute:D2}";
                this.SyncButton.IsEnabled = true;
                this.SyncLoader.IsActive = false;
                this.SyncLoader.Visibility = Visibility.Collapsed;
                this.LocationDialog.IsPrimaryButtonEnabled = true;
                this.LatitudeBox.IsEnabled = true;
                this.LongitudeBox.IsEnabled = true;
                this.LocationResultPanel.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                this.SyncButton.IsEnabled = true;
                this.SyncLoader.IsActive = false;
                this.SyncLoader.Visibility = Visibility.Collapsed;
                this.LocationResultPanel.Visibility = Visibility.Collapsed;
                this.LatitudeBox.IsEnabled = true;
                this.LongitudeBox.IsEnabled = true;
                Logger.LogInfo($"Location error: " + ex.Message);
            }
        }

        private void LatLonBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            double latitude = this.LatitudeBox.Value;
            double longitude = this.LongitudeBox.Value;

            if (double.IsNaN(latitude) || double.IsNaN(longitude) || (latitude == 0 && longitude == 0))
            {
                return;
            }

            var result = SunCalc.CalculateSunriseSunset(latitude, longitude, DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day);

            this.ViewModel.LocationPanelLightTimeMinutes = (result.SunriseHour * 60) + result.SunriseMinute;
            this.ViewModel.LocationPanelDarkTimeMinutes = (result.SunsetHour * 60) + result.SunsetMinute;

            this.LocationResultPanel.Visibility = Visibility.Visible;
            if (this.LocationDialog != null)
            {
                this.LocationDialog.IsPrimaryButtonEnabled = true;
            }
        }

        private void LocationDialog_PrimaryButtonClick(object sender, ContentDialogButtonClickEventArgs args)
        {
            if (double.IsNaN(this.LatitudeBox.Value) || double.IsNaN(this.LongitudeBox.Value))
            {
                return;
            }

            double latitude = this.LatitudeBox.Value;
            double longitude = this.LongitudeBox.Value;

            // need to save the values
            this.ViewModel.Latitude = latitude.ToString(CultureInfo.InvariantCulture);
            this.ViewModel.Longitude = longitude.ToString(CultureInfo.InvariantCulture);
            this.ViewModel.SyncButtonInformation = $"{this.ViewModel.Latitude}°, {this.ViewModel.Longitude}°";

            var result = SunCalc.CalculateSunriseSunset(latitude, longitude, DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day);

            this.ViewModel.LightTime = (result.SunriseHour * 60) + result.SunriseMinute;
            this.ViewModel.DarkTime = (result.SunsetHour * 60) + result.SunsetMinute;

            this.SunriseModeChartState();
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (this.suppressViewModelUpdates)
            {
                return;
            }

            if (e.PropertyName == "IsEnabled")
            {
                if (this.ViewModel.IsEnabled != this.generalSettingsRepository.SettingsConfig.Enabled.LightSwitch)
                {
                    this.generalSettingsRepository.SettingsConfig.Enabled.LightSwitch = this.ViewModel.IsEnabled;

                    var generalSettingsMessage = new OutGoingGeneralSettings(this.generalSettingsRepository.SettingsConfig).ToString();
                    Logger.LogInfo($"Saved general settings from Light Switch page.");

                    this.sendConfigMsg?.Invoke(generalSettingsMessage);
                }
            }
            else
            {
                if (this.ViewModel.ModuleSettings != null)
                {
                    SndLightSwitchSettings currentSettings = new(this.moduleSettingsRepository.SettingsConfig);
                    SndModuleSettings<SndLightSwitchSettings> csIpcMessage = new(currentSettings);

                    SndLightSwitchSettings outSettings = new(this.ViewModel.ModuleSettings);
                    SndModuleSettings<SndLightSwitchSettings> outIpcMessage = new(outSettings);

                    string csMessage = csIpcMessage.ToJsonString();
                    string outMessage = outIpcMessage.ToJsonString();

                    if (!csMessage.Equals(outMessage, StringComparison.Ordinal))
                    {
                        Logger.LogInfo($"Saved Light Switch settings from Light Switch page.");

                        this.sendConfigMsg?.Invoke(outMessage);
                    }
                }
            }
        }

        private void LoadSettings(SettingsRepository<GeneralSettings> generalSettingsRepository, SettingsRepository<LightSwitchSettings> moduleSettingsRepository)
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
                    this.ViewModel.IsEnabled = generalSettings.Enabled.LightSwitch;
                    this.ViewModel.ModuleSettings = (LightSwitchSettings)lightSwitchSettings.Clone();

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
            this.dispatcherQueue.TryEnqueue(() =>
            {
                this.suppressViewModelUpdates = true;

                this.moduleSettingsRepository.ReloadSettings();
                this.LoadSettings(this.generalSettingsRepository, this.moduleSettingsRepository);

                this.suppressViewModelUpdates = false;
            });
        }

        private void UpdateEnabledState(bool recommendedState)
        {
            var enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredLightSwitchEnabledValue();

            if (enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                this.ViewModel.IsEnabledGpoConfigured = true;
                this.ViewModel.EnabledGPOConfiguration = enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                this.ViewModel.IsEnabled = recommendedState;
            }
        }

        private async void SyncLocationButton_Click(object sender, RoutedEventArgs e)
        {
            this.LocationDialog.IsPrimaryButtonEnabled = false;
            this.LocationResultPanel.Visibility = Visibility.Collapsed;
            await this.LocationDialog.ShowAsync();
        }

        private void CityAutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput && !string.IsNullOrWhiteSpace(sender.Text))
            {
                string query = sender.Text.ToLower(CultureInfo.CurrentCulture);

                // Filter your cities (assuming ViewModel.Cities is a List<City>)
                var filtered = this.ViewModel.SearchLocations
                    .Where(c =>
                        (c.City?.Contains(query, StringComparison.CurrentCultureIgnoreCase) ?? false) ||
                        (c.Country?.Contains(query, StringComparison.CurrentCultureIgnoreCase) ?? false))
                    .ToList();

                sender.ItemsSource = filtered;
            }
        }

        /* private void CityAutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (args.SelectedItem is SearchLocation location)
            {
                ViewModel.SelectedCity = location;

                // CityAutoSuggestBox.Text = $"{location.City}, {location.Country}";
                LocationDialog.IsPrimaryButtonEnabled = true;
                LocationResultPanel.Visibility = Visibility.Visible;
            }
        } */

        private void ModeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (this.ViewModel.ScheduleMode)
            {
                case "FixedHours":
                    VisualStateManager.GoToState(this, "ManualState", true);
                    this.TimelineCard.Visibility = Visibility.Visible;
                    break;
                case "SunsetToSunrise":
                    VisualStateManager.GoToState(this, "SunsetToSunriseState", true);
                    this.SunriseModeChartState();
                    break;
                default:
                    VisualStateManager.GoToState(this, "OffState", true);
                    this.TimelineCard.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        private void SunriseModeChartState()
        {
            if (this.ViewModel.Latitude != "0.0" && this.ViewModel.Longitude != "0.0")
            {
                this.TimelineCard.Visibility = Visibility.Visible;
                this.LocationWarningBar.Visibility = Visibility.Collapsed;
            }
            else
            {
                this.TimelineCard.Visibility = Visibility.Collapsed;
                this.LocationWarningBar.Visibility = Visibility.Visible;
            }
        }
    }
}
