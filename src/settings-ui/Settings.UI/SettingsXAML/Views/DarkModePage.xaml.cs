// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Settings.UI.Library;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class DarkModePage : Page
    {
        private readonly string _appName = "DarkMode";
        private readonly SettingsUtils _settingsUtils;

        private readonly SettingsRepository<GeneralSettings> _generalSettingsRepository;
        private readonly SettingsRepository<DarkModeSettings> _moduleSettingsRepository;

        private DarkModeViewModel ViewModel { get; set; }

        public DarkModePage()
        {
            _settingsUtils = new SettingsUtils();

            ViewModel = new DarkModeViewModel();

            _generalSettingsRepository = SettingsRepository<GeneralSettings>.GetInstance(_settingsUtils);
            _moduleSettingsRepository = SettingsRepository<DarkModeSettings>.GetInstance(_settingsUtils);

            // We load the view model settings first.
            LoadSettings(_generalSettingsRepository, _moduleSettingsRepository);

            DataContext = ViewModel;

            var settingsPath = _settingsUtils.GetSettingsFilePath(_appName);

            InitializeComponent();
        }

        private void ModeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (TimeModeRadio.IsChecked == true)
            {
                // Set UseLocation to false (use specific times)
                ViewModel.ModuleSettings.Properties.UseLocation = false;
            }
            else if (GeoModeRadio.IsChecked == true)
            {
                // Set UseLocation to true (use geolocation)
                ViewModel.ModuleSettings.Properties.UseLocation = true;
            }

            // Refresh the view so dependent fields update (if applicable)
            ViewModel.NotifyPropertyChanged(nameof(ViewModel.IsUseLocationEnabled));
        }

        private void GetLocation_Click(object sender, RoutedEventArgs e)
        {
            return;
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

        private void UpdateEnabledState(bool recommendedState)
        {
            ViewModel.IsEnabled = recommendedState;
        }
    }
}
