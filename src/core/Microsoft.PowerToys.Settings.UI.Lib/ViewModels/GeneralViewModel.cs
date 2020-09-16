// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.PowerToys.Settings.UI.Lib.Helpers;
using Microsoft.PowerToys.Settings.UI.Lib.Interface;
using Microsoft.PowerToys.Settings.UI.Lib.Utilities;
using Microsoft.PowerToys.Settings.UI.Lib.ViewModels.Commands;

namespace Microsoft.PowerToys.Settings.UI.Lib.ViewModels
{
    public class GeneralViewModel : Observable
    {
        private IGeneralSettingsCache<GeneralSettings> GeneralSettingsCache { get; set; }

        public ButtonClickCommand CheckFoUpdatesEventHandler { get; set; }

        public ButtonClickCommand RestartElevatedButtonEventHandler { get; set; }

        public Func<string, int> UpdateUIThemeCallBack { get; }

        public Func<string, int> SendConfigMSG { get; }

        public Func<string, int> SendRestartAsAdminConfigMSG { get; }

        public Func<string, int> SendCheckForUpdatesConfigMSG { get; }

        public string RunningAsUserDefaultText { get; set; }

        public string RunningAsAdminDefaultText { get; set; }

        private string _settingsConfigFileFolder = string.Empty;

        public GeneralViewModel(IGeneralSettingsCache<GeneralSettings> generalSettingsCache, string runAsAdminText, string runAsUserText, bool isElevated, bool isAdmin, Func<string, int> updateTheme, Func<string, int> ipcMSGCallBackFunc, Func<string, int> ipcMSGRestartAsAdminMSGCallBackFunc, Func<string, int> ipcMSGCheckForUpdatesCallBackFunc, string configFileSubfolder = "")
        {
            CheckFoUpdatesEventHandler = new ButtonClickCommand(CheckForUpdates_Click);
            RestartElevatedButtonEventHandler = new ButtonClickCommand(Restart_Elevated);
            GeneralSettingsCache = generalSettingsCache;

            if (GeneralSettingsCache.GeneralSettings == null)
            {
                try
                {
                    GeneralSettingsCache.GeneralSettings = SettingsUtils.GetSettings<GeneralSettings>(string.Empty);

                    if (Helper.CompareVersions(GeneralSettingsCache.GeneralSettings.PowertoysVersion, Helper.GetProductVersion()) < 0)
                    {
                        // Update settings
                        GeneralSettingsCache.GeneralSettings.PowertoysVersion = Helper.GetProductVersion();
                        SettingsUtils.SaveSettings(GeneralSettingsCache.GeneralSettings.ToJsonString(), string.Empty);
                    }
                }
                catch (FormatException e)
                {
                    // If there is an issue with the version number format, don't migrate settings.
                    Debug.WriteLine(e.Message);
                }
                catch
                {
                    SettingsUtils.SaveSettings(GeneralSettingsCache.GeneralSettings.ToJsonString(), string.Empty);
                }
            }

            // set the callback functions value to hangle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;
            SendCheckForUpdatesConfigMSG = ipcMSGCheckForUpdatesCallBackFunc;
            SendRestartAsAdminConfigMSG = ipcMSGRestartAsAdminMSGCallBackFunc;

            // set the callback function value to update the UI theme.
            UpdateUIThemeCallBack = updateTheme;
            UpdateUIThemeCallBack(GeneralSettingsCache.GeneralSettings.Theme.ToLower());

            // Update Settings file folder:
            _settingsConfigFileFolder = configFileSubfolder;

            switch (GeneralSettingsCache.GeneralSettings.Theme.ToLower())
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

            _startup = GeneralSettingsCache.GeneralSettings.Startup;
            _autoDownloadUpdates = GeneralSettingsCache.GeneralSettings.AutoDownloadUpdates;
            _isElevated = isElevated;
            _runElevated = GeneralSettingsCache.GeneralSettings.RunElevated;

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
                    GeneralSettingsCache.GeneralSettings.Startup = value;
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
                    GeneralSettingsCache.GeneralSettings.RunElevated = value;
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
                    GeneralSettingsCache.GeneralSettings.AutoDownloadUpdates = value;
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
                    GeneralSettingsCache.GeneralSettings.Theme = "dark";
                    _isDarkThemeRadioButtonChecked = value;
                    try
                    {
                        UpdateUIThemeCallBack(GeneralSettingsCache.GeneralSettings.Theme);
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
                    GeneralSettingsCache.GeneralSettings.Theme = "light";
                    _isLightThemeRadioButtonChecked = value;
                    try
                    {
                        UpdateUIThemeCallBack(GeneralSettingsCache.GeneralSettings.Theme);
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
                    GeneralSettingsCache.GeneralSettings.Theme = "system";
                    _isSystemThemeRadioButtonChecked = value;
                    try
                    {
                        UpdateUIThemeCallBack(GeneralSettingsCache.GeneralSettings.Theme);
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
            OutGoingGeneralSettings outsettings = new OutGoingGeneralSettings(GeneralSettingsCache.GeneralSettings);

            SendConfigMSG(outsettings.ToString());
        }

        // callback function to launch the URL to check for updates.
        private void CheckForUpdates_Click()
        {
            GeneralSettings settings = SettingsUtils.GetSettings<GeneralSettings>(_settingsConfigFileFolder);
            settings.CustomActionName = "check_for_updates";

            OutGoingGeneralSettings outsettings = new OutGoingGeneralSettings(settings);
            GeneralSettingsCustomAction customaction = new GeneralSettingsCustomAction(outsettings);

            SendCheckForUpdatesConfigMSG(customaction.ToString());
        }

        public void Restart_Elevated()
        {
            GeneralSettings settings = SettingsUtils.GetSettings<GeneralSettings>(_settingsConfigFileFolder);
            settings.CustomActionName = "restart_elevation";

            OutGoingGeneralSettings outsettings = new OutGoingGeneralSettings(settings);
            GeneralSettingsCustomAction customaction = new GeneralSettingsCustomAction(outsettings);

            SendRestartAsAdminConfigMSG(customaction.ToString());
        }
    }
}
