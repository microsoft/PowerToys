// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using Microsoft.PowerToys.Settings.UI.Lib.Helpers;
using Microsoft.PowerToys.Settings.UI.Lib.Interface;
using Microsoft.PowerToys.Settings.UI.Lib.Utilities;
using Microsoft.PowerToys.Settings.UI.Lib.ViewModels.Commands;

namespace Microsoft.PowerToys.Settings.UI.Lib.ViewModels
{
    public class GeneralViewModel : Observable
    {
        private GeneralSettings GeneralSettingsConfig { get; set; }

        public ButtonClickCommand CheckFoUpdatesEventHandler { get; set; }

        public ButtonClickCommand RestartElevatedButtonEventHandler { get; set; }

        public Func<string, int> UpdateUIThemeCallBack { get; }

        public Func<string, int> SendConfigMSG { get; }

        public Func<string, int> SendRestartAsAdminConfigMSG { get; }

        public Func<string, int> SendCheckForUpdatesConfigMSG { get; }

        public string RunningAsUserDefaultText { get; set; }

        public string RunningAsAdminDefaultText { get; set; }

        private string _settingsConfigFileFolder = string.Empty;

        public GeneralViewModel(ISettingsRepository<GeneralSettings> settingsRepository, string runAsAdminText, string runAsUserText, bool isElevated, bool isAdmin, Func<string, int> updateTheme, Func<string, int> ipcMSGCallBackFunc, Func<string, int> ipcMSGRestartAsAdminMSGCallBackFunc, Func<string, int> ipcMSGCheckForUpdatesCallBackFunc, string configFileSubfolder = "")
        {
            CheckFoUpdatesEventHandler = new ButtonClickCommand(CheckForUpdates_Click);
            RestartElevatedButtonEventHandler = new ButtonClickCommand(Restart_Elevated);

            // To obtain the general settings configuration of PowerToys if it exists, else to create a new file and return the default configurations.
            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            // set the callback functions value to hangle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;
            SendCheckForUpdatesConfigMSG = ipcMSGCheckForUpdatesCallBackFunc;
            SendRestartAsAdminConfigMSG = ipcMSGRestartAsAdminMSGCallBackFunc;

            // set the callback function value to update the UI theme.
            UpdateUIThemeCallBack = updateTheme;
            UpdateUIThemeCallBack(GeneralSettingsConfig.Theme.ToLower());

            // Update Settings file folder:
            _settingsConfigFileFolder = configFileSubfolder;

            switch (GeneralSettingsConfig.Theme.ToLower())
            {
                case "light":
                    _isLightThemeRadioButtonChecked = true;
                    break;
                case "dark":
                    _isDarkThemeRadioButtonChecked = true;
                    break;
                case "system":
                    _isSystemThemeRadioButtonChecked = true;
                    break;
            }

            _startup = GeneralSettingsConfig.Startup;
            _autoDownloadUpdates = GeneralSettingsConfig.AutoDownloadUpdates;
            _isElevated = isElevated;
            _runElevated = GeneralSettingsConfig.RunElevated;

            RunningAsUserDefaultText = runAsUserText;
            RunningAsAdminDefaultText = runAsAdminText;

            _isAdmin = isAdmin;
        }

        private bool _packaged = false;
        private bool _startup = false;
        private bool _isElevated = false;
        private bool _runElevated = false;
        private bool _isAdmin = false;
        private bool _isDarkThemeRadioButtonChecked = false;
        private bool _isLightThemeRadioButtonChecked = false;
        private bool _isSystemThemeRadioButtonChecked = false;
        private bool _autoDownloadUpdates = false;

        private string _latestAvailableVersion = string.Empty;

        // Gets or sets a value indicating whether packaged.
        public bool Packaged
        {
            get
            {
                return _packaged;
            }

            set
            {
                if (_packaged != value)
                {
                    _packaged = value;
                    RaisePropertyChanged();
                }
            }
        }

        // Gets or sets a value indicating whether run powertoys on start-up.
        public bool Startup
        {
            get
            {
                return _startup;
            }

            set
            {
                if (_startup != value)
                {
                    _startup = value;
                    GeneralSettingsConfig.Startup = value;
                    RaisePropertyChanged();
                }
            }
        }

        public string RunningAsText
        {
            get
            {
                if (!IsElevated)
                {
                    return RunningAsUserDefaultText;
                }
                else
                {
                    return RunningAsAdminDefaultText;
                }
            }

            set
            {
                OnPropertyChanged("RunningAsAdminText");
            }
        }

        // Gets or sets a value indicating whether the powertoy elevated.
        public bool IsElevated
        {
            get
            {
                return _isElevated;
            }

            set
            {
                if (_isElevated != value)
                {
                    _isElevated = value;
                    OnPropertyChanged("IsElevated");
                    OnPropertyChanged("IsAdminButtonEnabled");
                    OnPropertyChanged("RunningAsAdminText");
                }
            }
        }

        public bool IsAdminButtonEnabled
        {
            get
            {
                return !IsElevated;
            }

            set
            {
                OnPropertyChanged("IsAdminButtonEnabled");
            }
        }

        // Gets or sets a value indicating whether powertoys should run elevated.
        public bool RunElevated
        {
            get
            {
                return _runElevated;
            }

            set
            {
                if (_runElevated != value)
                {
                    _runElevated = value;
                    GeneralSettingsConfig.RunElevated = value;
                    RaisePropertyChanged();
                }
            }
        }

        // Gets a value indicating whether the user is part of administrators group.
        public bool IsAdmin
        {
            get
            {
                return _isAdmin;
            }
        }

        public bool AutoDownloadUpdates
        {
            get
            {
                return _autoDownloadUpdates;
            }

            set
            {
                if (_autoDownloadUpdates != value)
                {
                    _autoDownloadUpdates = value;
                    GeneralSettingsConfig.AutoDownloadUpdates = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool IsDarkThemeRadioButtonChecked
        {
            get
            {
                return _isDarkThemeRadioButtonChecked;
            }

            set
            {
                if (value == true)
                {
                    GeneralSettingsConfig.Theme = "dark";
                    _isDarkThemeRadioButtonChecked = value;
                    try
                    {
                        UpdateUIThemeCallBack(GeneralSettingsConfig.Theme);
                    }
                    catch
                    {
                    }

                    RaisePropertyChanged();
                }
            }
        }

        public bool IsLightThemeRadioButtonChecked
        {
            get
            {
                return _isLightThemeRadioButtonChecked;
            }

            set
            {
                if (value == true)
                {
                    GeneralSettingsConfig.Theme = "light";
                    _isLightThemeRadioButtonChecked = value;
                    try
                    {
                        UpdateUIThemeCallBack(GeneralSettingsConfig.Theme);
                    }
                    catch
                    {
                    }

                    RaisePropertyChanged();
                }
            }
        }

        public bool IsSystemThemeRadioButtonChecked
        {
            get
            {
                return _isSystemThemeRadioButtonChecked;
            }

            set
            {
                if (value == true)
                {
                    GeneralSettingsConfig.Theme = "system";
                    _isSystemThemeRadioButtonChecked = value;
                    try
                    {
                        UpdateUIThemeCallBack(GeneralSettingsConfig.Theme);
                    }
                    catch
                    {
                    }

                    RaisePropertyChanged();
                }
            }
        }

        public string PowerToysVersion
        {
            get
            {
                return Helper.GetProductVersion();
            }
        }

        // Temp string. Appears when a user clicks "Check for updates" button and shows latest version available on the Github.
        public string LatestAvailableVersion
        {
            get
            {
                return _latestAvailableVersion;
            }

            set
            {
                if (_latestAvailableVersion != value)
                {
                    _latestAvailableVersion = value;
                    RaisePropertyChanged();
                }
            }
        }

        public void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            // Notify UI of property change
            OnPropertyChanged(propertyName);
            OutGoingGeneralSettings outsettings = new OutGoingGeneralSettings(GeneralSettingsConfig);

            SendConfigMSG(outsettings.ToString());
        }

        // callback function to launch the URL to check for updates.
        private void CheckForUpdates_Click()
        {
            GeneralSettingsConfig.CustomActionName = "check_for_updates";

            OutGoingGeneralSettings outsettings = new OutGoingGeneralSettings(GeneralSettingsConfig);
            GeneralSettingsCustomAction customaction = new GeneralSettingsCustomAction(outsettings);

            SendCheckForUpdatesConfigMSG(customaction.ToString());
        }

        public void Restart_Elevated()
        {
            GeneralSettingsConfig.CustomActionName = "restart_elevation";

            OutGoingGeneralSettings outsettings = new OutGoingGeneralSettings(GeneralSettingsConfig);
            GeneralSettingsCustomAction customaction = new GeneralSettingsCustomAction(outsettings);

            SendRestartAsAdminConfigMSG(customaction.ToString());
        }
    }
}
