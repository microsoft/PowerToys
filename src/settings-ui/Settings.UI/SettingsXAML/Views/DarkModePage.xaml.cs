// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Dispatching;
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
            ViewModel = new DarkModeViewModel(darkSettings);
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

            InitializeComponent();
        }

        /* private void ModeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (TimeModeRadio.IsChecked == true)
            {
                // Set UseLocation to false (use specific times)
                ViewModel.ModuleSettings.Properties.UseLocation.Value = false;
            }
            else if (GeoModeRadio.IsChecked == true)
            {
                // Set UseLocation to true (use geolocation)
                ViewModel.ModuleSettings.Properties.UseLocation.Value = true;
            }

            // Refresh the view so dependent fields update (if applicable)
            ViewModel.NotifyPropertyChanged(nameof(ViewModel.IsUseLocationEnabled));
        } */

        private void GetLocation_Click(object sender, RoutedEventArgs e)
        {
            return;
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
            ViewModel.IsEnabled = recommendedState;
        }
    }
}
