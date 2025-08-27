// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PowerToys.GPOWrapper;
using Settings.UI.Library;
using Windows.Devices.Geolocation;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class DarkModePage : Page
    {
        private readonly string _appName = "DarkMode";
        private readonly SettingsUtils _settingsUtils;
        private readonly Func<string, int> _sendConfigMsg = ShellPage.SendDefaultIPCMessage;

        private readonly ISettingsRepository<GeneralSettings> _generalSettingsRepository;
        private readonly ISettingsRepository<DarkModeSettings> _moduleSettingsRepository;

        private readonly IFileSystem _fileSystem;
        private readonly IFileSystemWatcher _fileSystemWatcher;
        private readonly DispatcherQueue _dispatcherQueue;

        private DarkModeViewModel ViewModel { get; set; }

        public DarkModePage()
        {
            _settingsUtils = new SettingsUtils();
            _sendConfigMsg = ShellPage.SendDefaultIPCMessage;

            _generalSettingsRepository = SettingsRepository<GeneralSettings>.GetInstance(_settingsUtils);
            _moduleSettingsRepository = SettingsRepository<DarkModeSettings>.GetInstance(_settingsUtils);

            // Get settings from JSON (or defaults if JSON missing)
            var darkSettings = _moduleSettingsRepository.SettingsConfig;

            // Pass them into the ViewModel
            ViewModel = new DarkModeViewModel(darkSettings, ShellPage.SendDefaultIPCMessage);
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
            this.Loaded += DarkModePage_Loaded;
        }

        private void DarkModePage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ViewModel.ScheduleMode == "SunsetToSunrise")
                {
                    SunTimes.Text = $"Sunrise: {ViewModel.LightTime / 60:D2}:{ViewModel.LightTime % 60:D2} " +
                                    $"Sunset: {ViewModel.DarkTime / 60:D2}:{ViewModel.DarkTime % 60:D2}";
                    return;
                }

                // fallback text
                SunTimes.Text = "Please Sync to update Sunrise/Sunset times";
            }
            catch
            {
                SunTimes.Text = "Please Sync to update Sunrise/Sunset times";
            }
        }

        private async void GetLocation_Click(object sender, RoutedEventArgs e)
        {
            SyncButton.IsEnabled = false;
            SyncLoader.IsActive = true;
            SyncLoader.Visibility = Visibility.Visible;
            SunTimes.Text = "Loading...";

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

                // Get the position
                Geoposition pos = await geolocator.GetGeopositionAsync();

                double latitude = Math.Round(pos.Coordinate.Point.Position.Latitude);
                double longitude = Math.Round(pos.Coordinate.Point.Position.Longitude);

                SunTimes result = SunCalc.CalculateSunriseSunset(
                    latitude,
                    longitude,
                    DateTime.Now.Year,
                    DateTime.Now.Month,
                    DateTime.Now.Day);

                SunTimes.Text = "Sunrise: " + result.SunriseHour + ":" + result.SunriseMinute + " " +
                                "Sunset: " + result.SunsetHour + ":" + result.SunsetMinute;

                ViewModel.LightTime = (result.SunriseHour * 60) + result.SunriseMinute;
                ViewModel.DarkTime = (result.SunsetHour * 60) + result.SunsetMinute;

                SyncButton.IsEnabled = true;
                SyncLoader.IsActive = false;
                SyncLoader.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                SyncButton.IsEnabled = true;
                SyncLoader.IsActive = false;
                System.Diagnostics.Debug.WriteLine("Location error: " + ex.Message);
            }
        }

        private void ScheduleMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var combo = (ComboBox)sender;
            var selectedTag = (combo.SelectedItem as ComboBoxItem)?.Tag?.ToString();

            if (selectedTag == "FixedHours")
            {
                ViewModel.LightTime = 360;
                ViewModel.DarkTime = 1080;
            }
            else if (selectedTag == "SunsetToSunrise")
            {
                SunTimes.Text = "Please Sync to update Sunrise/Sunset times";
            }
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsEnabled")
            {
                if (ViewModel.IsEnabled != _generalSettingsRepository.SettingsConfig.Enabled.DarkMode)
                {
                    _generalSettingsRepository.SettingsConfig.Enabled.DarkMode = ViewModel.IsEnabled;

                    var generalSettingsMessage = new OutGoingGeneralSettings(_generalSettingsRepository.SettingsConfig).ToString();
                    Logger.LogInfo($"Saved general settings from DarkMode page.");

                    _sendConfigMsg?.Invoke(generalSettingsMessage);
                }
            }
            else
            {
                if (ViewModel.ModuleSettings != null)
                {
                    SndDarkModeSettings currentSettings = new(_moduleSettingsRepository.SettingsConfig);
                    SndModuleSettings<SndDarkModeSettings> csIpcMessage = new(currentSettings);

                    SndDarkModeSettings outSettings = new(ViewModel.ModuleSettings);
                    SndModuleSettings<SndDarkModeSettings> outIpcMessage = new(outSettings);

                    string csMessage = csIpcMessage.ToJsonString();
                    string outMessage = outIpcMessage.ToJsonString();

                    if (!csMessage.Equals(outMessage, StringComparison.Ordinal))
                    {
                        Logger.LogInfo($"Saved DarkMode settings from DarkMode page.");

                        _sendConfigMsg?.Invoke(outMessage);
                    }
                }
            }
        }

        private void LoadSettings(ISettingsRepository<GeneralSettings> generalSettingsRepository, ISettingsRepository<DarkModeSettings> moduleSettingsRepository)
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

        private void UpdateViewModelSettings(DarkModeSettings darkSettings, GeneralSettings generalSettings)
        {
            if (darkSettings != null)
            {
                if (generalSettings != null)
                {
                    ViewModel.IsEnabled = generalSettings.Enabled.DarkMode;
                    ViewModel.ModuleSettings = (DarkModeSettings)darkSettings.Clone();

                    UpdateEnabledState(generalSettings.Enabled.DarkMode);
                }
                else
                {
                    throw new ArgumentNullException(nameof(generalSettings));
                }
            }
            else
            {
                throw new ArgumentNullException(nameof(darkSettings));
            }
        }

        private void Settings_Changed(object sender, FileSystemEventArgs e)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                _moduleSettingsRepository.ReloadSettings();
                LoadSettings(_generalSettingsRepository, _moduleSettingsRepository);
            });
        }

        private void UpdateEnabledState(bool recommendedState)
        {
            var enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredDarkModeEnabledValue();

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
    }
}
