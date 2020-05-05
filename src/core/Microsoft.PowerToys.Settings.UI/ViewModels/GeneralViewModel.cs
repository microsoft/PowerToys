// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.ViewModels.Commands;
using Microsoft.PowerToys.Settings.UI.Views;
using Windows.System;
using Windows.UI.Popups;
using Windows.UI.Xaml;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class GeneralViewModel : Observable
    {
        private GeneralSettings GeneralSettingsConfigs { get; set; }

        public ButtonClickCommand CheckFoUpdatesEventHandler { get; set; }

        public ButtonClickCommand RestartElevatedButtonEventHandler { get; set; }

        public GeneralViewModel()
        {
            this.CheckFoUpdatesEventHandler = new ButtonClickCommand(CheckForUpdates_Click);
            this.RestartElevatedButtonEventHandler = new ButtonClickCommand(Restart_Elevated);

            try
            {
                GeneralSettingsConfigs = SettingsUtils.GetSettings<GeneralSettings>(string.Empty);
            }
            catch
            {
                GeneralSettingsConfigs = new GeneralSettings();
                SettingsUtils.SaveSettings(GeneralSettingsConfigs.ToJsonString(), string.Empty);
            }

            switch (GeneralSettingsConfigs.Theme.ToLower())
            {
                case "light":
                    _isLightThemeRadioButtonChecked = true;
                    try
                    {
                        ShellPage.ShellHandler.RequestedTheme = ElementTheme.Light;
                    }
                    catch
                    {
                    }

                    break;
                case "dark":
                    _isDarkThemeRadioButtonChecked = true;
                    try
                    {
                        ShellPage.ShellHandler.RequestedTheme = ElementTheme.Dark;
                    }
                    catch
                    {
                    }

                    break;
                case "system":
                    _isSystemThemeRadioButtonChecked = true;
                    try
                    {
                        ShellPage.ShellHandler.RequestedTheme = ElementTheme.Default;
                    }
                    catch
                    {
                    }

                    break;
            }

            _startup = GeneralSettingsConfigs.Startup;
            _autoDownloadUpdates = GeneralSettingsConfigs.AutoDownloadUpdates;
            _isElevated = GeneralSettingsConfigs.IsElevated;
            _runElevated = GeneralSettingsConfigs.RunElevated;
        }

        private bool _packaged = false;
        private bool _startup = false;
        private bool _isElevated = false;
        private bool _runElevated = false;
        private bool _isDarkThemeRadioButtonChecked = false;
        private bool _isLightThemeRadioButtonChecked = false;
        private bool _isSystemThemeRadioButtonChecked = false;
        private bool _autoDownloadUpdates = false;

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

        public string AlwaysRunAsAdminText
        {
            get
            {
                if (IsElevated)
                {
                    return "Always run as administrator";
                }
                else
                {
                    return "Always run as administrator (Restart as administrator to change this)";
                }
            }

            set
            {
                OnPropertyChanged("AlwaysRunAsAdminText");
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
                    GeneralSettingsConfigs.Startup = value;
                    RaisePropertyChanged();
                }
            }
        }

        public string RunningAsAdminText
        {
            get
            {
                if (!IsElevated)
                {
                    return "Running as user.";
                }
                else
                {
                    return "Running as Adminstrator.";
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
                    OnPropertyChanged("AlwaysRunAsAdminText");
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
                    GeneralSettingsConfigs.RunElevated = value;
                    RaisePropertyChanged();
                }
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
                    GeneralSettingsConfigs.AutoDownloadUpdates = value;
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
                    GeneralSettingsConfigs.Theme = "dark";
                    _isDarkThemeRadioButtonChecked = value;
                    try
                    {
                        ShellPage.ShellHandler.RequestedTheme = ElementTheme.Dark;
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
                    GeneralSettingsConfigs.Theme = "light";
                    _isLightThemeRadioButtonChecked = value;
                    try
                    {
                        ShellPage.ShellHandler.RequestedTheme = ElementTheme.Light;
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
                    GeneralSettingsConfigs.Theme = "system";
                    _isSystemThemeRadioButtonChecked = value;
                    try
                    {
                        ShellPage.ShellHandler.RequestedTheme = ElementTheme.Default;
                    }
                    catch
                    {
                    }

                    RaisePropertyChanged();
                }
            }
        }

        public void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            // Notify UI of property change
            OnPropertyChanged(propertyName);

            OutGoingGeneralSettings outsettings = new OutGoingGeneralSettings(GeneralSettingsConfigs);

            ShellPage.DefaultSndMSGCallback(outsettings.ToString());
        }

        // callback function to launch the URL to check for updates.
        private async void CheckForUpdates_Click()
        {
            await Launcher.LaunchUriAsync(new Uri("https://github.com/microsoft/PowerToys/releases"));
        }

        public void Restart_Elevated()
        {
            GeneralSettings settings = SettingsUtils.GetSettings<GeneralSettings>(string.Empty);
            settings.CustomActionName = "restart_elevation";
            IsElevated = true;

            OutGoingGeneralSettings outsettings = new OutGoingGeneralSettings(settings);
            GeneralSettingsCustomAction customaction = new GeneralSettingsCustomAction(outsettings);

            if (ShellPage.DefaultSndMSGCallback != null)
            {
                ShellPage.DefaultSndMSGCallback(customaction.ToString());
            }
        }
    }
}
