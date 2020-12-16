// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Settings.UI.Library.ViewModels.Commands;

namespace Microsoft.PowerToys.Settings.UI.Library.ViewModels
{
    public class GeneralViewModel : Observable
    {
        private GeneralSettings GeneralSettingsConfig { get; set; }

        public ButtonClickCommand CheckForUpdatesEventHandler { get; set; }

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
            CheckForUpdatesEventHandler = new ButtonClickCommand(CheckForUpdatesClick);
            RestartElevatedButtonEventHandler = new ButtonClickCommand(RestartElevated);

            // To obtain the general settings configuration of PowerToys if it exists, else to create a new file and return the default configurations.
            if (settingsRepository == null)
            {
                throw new ArgumentNullException(nameof(settingsRepository));
            }

            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            // set the callback functions value to hangle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;
            SendCheckForUpdatesConfigMSG = ipcMSGCheckForUpdatesCallBackFunc;
            SendRestartAsAdminConfigMSG = ipcMSGRestartAsAdminMSGCallBackFunc;

            // set the callback function value to update the UI theme.
            UpdateUIThemeCallBack = updateTheme;

            UpdateUIThemeCallBack(GeneralSettingsConfig.Theme);

            // Update Settings file folder:
            _settingsConfigFileFolder = configFileSubfolder;

            // Using Invariant here as these are internal strings and fxcop
            // expects strings to be normalized to uppercase. While the theme names
            // are represented in lowercase everywhere else, we'll use uppercase
            // normalization for switch statements
            switch (GeneralSettingsConfig.Theme.ToUpperInvariant())
            {
                case "LIGHT":
                    _isLightThemeRadioButtonChecked = true;
                    break;
                case "DARK":
                    _isDarkThemeRadioButtonChecked = true;
                    break;
                case "SYSTEM":
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

        private bool _packaged;
        private bool _startup;
        private bool _isElevated;
        private bool _runElevated;
        private bool _isAdmin;
        private bool _isDarkThemeRadioButtonChecked;
        private bool _isLightThemeRadioButtonChecked;
        private bool _isSystemThemeRadioButtonChecked;
        private bool _autoDownloadUpdates;

        private string _latestAvailableVersion = string.Empty;
        private string _updateCheckedDate = string.Empty;

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
                    NotifyPropertyChanged();
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
                    NotifyPropertyChanged();
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
                    OnPropertyChanged(nameof(IsElevated));
                    OnPropertyChanged(nameof(IsAdminButtonEnabled));
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
                OnPropertyChanged(nameof(IsAdminButtonEnabled));
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
                    NotifyPropertyChanged();
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
                    NotifyPropertyChanged();
                }
            }
        }

        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "This may throw if the XAML page is not initialized in tests (https://github.com/microsoft/PowerToys/pull/2676)")]
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
                    catch (Exception e)
                    {
                        Logger.LogError("Exception encountered when changing Settings theme", e);
                    }

                    NotifyPropertyChanged();
                }
            }
        }

        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "This may throw if the XAML page is not initialized in tests (https://github.com/microsoft/PowerToys/pull/2676)")]
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
                    catch (Exception e)
                    {
                        Logger.LogError("Exception encountered when changing Settings theme", e);
                    }

                    NotifyPropertyChanged();
                }
            }
        }

        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "This may throw if the XAML page is not initialized in tests (https://github.com/microsoft/PowerToys/pull/2676)")]
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
                    catch (Exception e)
                    {
                        Logger.LogError("Exception encountered when changing Settings theme", e);
                    }

                    NotifyPropertyChanged();
                }
            }
        }

        // FxCop suggests marking this member static, but it is accessed through
        // an instance in autogenerated files (GeneralPage.g.cs) and will break
        // the file if modified
#pragma warning disable CA1822 // Mark members as static
        public string PowerToysVersion
#pragma warning restore CA1822 // Mark members as static
        {
            get
            {
                return Helper.GetProductVersion();
            }
        }

        public string UpdateCheckedDate
        {
            get
            {
                RequestUpdateCheckedDate();
                return _updateCheckedDate;
            }

            set
            {
                if (_updateCheckedDate != value)
                {
                    _updateCheckedDate = value;
                    NotifyPropertyChanged();
                }
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
                    NotifyPropertyChanged();
                }
            }
        }

        public void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
        {
            // Notify UI of property change
            OnPropertyChanged(propertyName);
            OutGoingGeneralSettings outsettings = new OutGoingGeneralSettings(GeneralSettingsConfig);

            SendConfigMSG(outsettings.ToString());
        }

        // callback function to launch the URL to check for updates.
        private void CheckForUpdatesClick()
        {
            GeneralSettingsConfig.CustomActionName = "check_for_updates";

            OutGoingGeneralSettings outsettings = new OutGoingGeneralSettings(GeneralSettingsConfig);
            GeneralSettingsCustomAction customaction = new GeneralSettingsCustomAction(outsettings);

            SendCheckForUpdatesConfigMSG(customaction.ToString());
            RequestUpdateCheckedDate();
        }

        private void RequestUpdateCheckedDate()
        {
            GeneralSettingsConfig.CustomActionName = "request_update_state_date";

            OutGoingGeneralSettings outsettings = new OutGoingGeneralSettings(GeneralSettingsConfig);
            GeneralSettingsCustomAction customaction = new GeneralSettingsCustomAction(outsettings);

            SendCheckForUpdatesConfigMSG(customaction.ToString());
        }

        public void RestartElevated()
        {
            GeneralSettingsConfig.CustomActionName = "restart_elevation";

            OutGoingGeneralSettings outsettings = new OutGoingGeneralSettings(GeneralSettingsConfig);
            GeneralSettingsCustomAction customaction = new GeneralSettingsCustomAction(outsettings);

            SendRestartAsAdminConfigMSG(customaction.ToString());
        }
    }
}
