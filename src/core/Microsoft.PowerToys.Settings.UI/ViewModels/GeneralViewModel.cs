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
                    ShellPage.ShellHandler.RequestedTheme = ElementTheme.Light;
                    break;
                case "dark":
                    _isDarkThemeRadioButtonChecked = true;
                    ShellPage.ShellHandler.RequestedTheme = ElementTheme.Dark;
                    break;
                case "system":
                    _isSystemThemeRadioButtonChecked = true;
                    ShellPage.ShellHandler.RequestedTheme = ElementTheme.Default;
                    break;
            }

            _startup = GeneralSettingsConfigs.Startup;
        }

        private bool _packaged = false;
        private bool _startup = false;
        private bool _isElevated = false;
        private bool _runElevated = false;
        private bool _isDarkThemeRadioButtonChecked = false;
        private bool _isLightThemeRadioButtonChecked = false;
        private bool _isSystemThemeRadioButtonChecked = false;

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
                    RaisePropertyChanged();
                }
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
                    RaisePropertyChanged();
                }
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
                    ShellPage.ShellHandler.RequestedTheme = ElementTheme.Dark;
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
                    ShellPage.ShellHandler.RequestedTheme = ElementTheme.Light;
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
                    ShellPage.ShellHandler.RequestedTheme = ElementTheme.Default;
                    RaisePropertyChanged();
                }
            }
        }

        public void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            // Notify UI of property change
            OnPropertyChanged(propertyName);

            SettingsUtils.SaveSettings(GeneralSettingsConfigs.ToJsonString(), string.Empty);

            OutGoingGeneralSettings outsettings = new OutGoingGeneralSettings(GeneralSettingsConfigs);

            ShellPage.DefaultSndMSGCallback(outsettings.ToString());
        }

        // callback function to launch the URL to check for updates.
        private async void CheckForUpdates_Click()
        {
            await Launcher.LaunchUriAsync(new Uri("https://github.com/microsoft/PowerToys/releases"));
        }

        private void Restart_Elevated()
        {
            GeneralSettings settings = SettingsUtils.GetSettings<GeneralSettings>(string.Empty);
            settings.RunElevated = true;
            OutGoingGeneralSettings outsettings = new OutGoingGeneralSettings(settings);

            if (ShellPage.DefaultSndMSGCallback != null)
            {
                ShellPage.DefaultSndMSGCallback(outsettings.ToString());
            }
        }
    }
}
