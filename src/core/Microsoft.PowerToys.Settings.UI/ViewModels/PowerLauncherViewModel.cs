// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System.Runtime.CompilerServices;
using System.Text.Json;

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.Views;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class PowerLauncherViewModel : Observable
    {
        private PowerLauncherSettings settings;
        private GeneralSettings generalSettings;

        public delegate void SendCallback(PowerLauncherSettings settings);

        private readonly SendCallback callback;

        public PowerLauncherViewModel()
        {
            callback = (PowerLauncherSettings settings) =>
            {
                // Propagate changes to Power Launcher through IPC
                ShellPage.DefaultSndMSGCallback(
                    string.Format("{{ \"powertoys\": {{ \"{0}\": {1} }} }}", PowerLauncherSettings.ModuleName, JsonSerializer.Serialize(settings)));
            };
            if (SettingsUtils.SettingsExists(PowerLauncherSettings.ModuleName))
            {
                settings = SettingsUtils.GetSettings<PowerLauncherSettings>(PowerLauncherSettings.ModuleName);
            }
            else
            {
                settings = new PowerLauncherSettings();
                settings.Properties.OpenPowerLauncher.Alt = true;
                settings.Properties.OpenPowerLauncher.Code = (int)Windows.System.VirtualKey.Space;
                settings.Properties.MaximumNumberOfResults = 4;
                callback(settings);
            }

            if (SettingsUtils.SettingsExists())
            {
                generalSettings = SettingsUtils.GetSettings<GeneralSettings>();
            }
            else
            {
                generalSettings = new GeneralSettings();
            }
        }

        public PowerLauncherViewModel(PowerLauncherSettings settings, SendCallback callback)
        {
            this.settings = settings;
            this.callback = callback;
        }

        private void UpdateSettings([CallerMemberName] string propertyName = null)
        {
            // Notify UI of property change
            OnPropertyChanged(propertyName);

            callback(settings);
        }

        public bool EnablePowerLauncher
        {
            get
            {
                return generalSettings.Enabled.PowerLauncher;
            }

            set
            {
                if (generalSettings.Enabled.PowerLauncher != value)
                {
                    generalSettings.Enabled.PowerLauncher = value;
                    OnPropertyChanged(nameof(EnablePowerLauncher));
                    OutGoingGeneralSettings outgoing = new OutGoingGeneralSettings(generalSettings);
                    ShellPage.DefaultSndMSGCallback(outgoing.ToString());
                }
            }
        }

        public string SearchResultPreference
        {
            get
            {
                return settings.Properties.SearchResultPreference;
            }

            set
            {
                if (settings.Properties.SearchResultPreference != value)
                {
                    settings.Properties.SearchResultPreference = value;
                    UpdateSettings();
                }
            }
        }

        public string SearchTypePreference
        {
            get
            {
                return settings.Properties.SearchTypePreference;
            }

            set
            {
                if (settings.Properties.SearchTypePreference != value)
                {
                    settings.Properties.SearchTypePreference = value;
                    UpdateSettings();
                }
            }
        }

        public int MaximumNumberOfResults
        {
            get
            {
                return settings.Properties.MaximumNumberOfResults;
            }

            set
            {
                if (settings.Properties.MaximumNumberOfResults != value)
                {
                    settings.Properties.MaximumNumberOfResults = value;
                    UpdateSettings();
                }
            }
        }

        public HotkeySettings OpenPowerLauncher
        {
            get
            {
                return settings.Properties.OpenPowerLauncher;
            }

            set
            {
                if (settings.Properties.OpenPowerLauncher != value)
                {
                    settings.Properties.OpenPowerLauncher = value;
                    UpdateSettings();
                }
            }
        }

        public HotkeySettings OpenFileLocation
        {
            get
            {
                return settings.Properties.OpenFileLocation;
            }

            set
            {
                if (settings.Properties.OpenFileLocation != value)
                {
                    settings.Properties.OpenFileLocation = value;
                    UpdateSettings();
                }
            }
        }

        public HotkeySettings OpenConsole
        {
            get
            {
                return settings.Properties.OpenConsole;
            }

            set
            {
                if (settings.Properties.OpenConsole != value)
                {
                    settings.Properties.OpenConsole = value;
                    UpdateSettings();
                }
            }
        }

        public HotkeySettings CopyPathLocation
        {
            get
            {
                return settings.Properties.CopyPathLocation;
            }

            set
            {
                if (settings.Properties.CopyPathLocation != value)
                {
                    settings.Properties.CopyPathLocation = value;
                    UpdateSettings();
                }
            }
        }

        public bool OverrideWinRKey
        {
            get
            {
                return settings.Properties.OverrideWinkeyR;
            }

            set
            {
                if (settings.Properties.OverrideWinkeyR != value)
                {
                    settings.Properties.OverrideWinkeyR = value;
                    UpdateSettings();
                }
            }
        }

        public bool OverrideWinSKey
        {
            get
            {
                return settings.Properties.OverrideWinkeyS;
            }

            set
            {
                if (settings.Properties.OverrideWinkeyS != value)
                {
                    settings.Properties.OverrideWinkeyS = value;
                    UpdateSettings();
                }
            }
        }

        public bool IgnoreHotkeysInFullScreen
        {
            get
            {
                return settings.Properties.IgnoreHotkeysInFullscreen;
            }

            set
            {
                if (settings.Properties.IgnoreHotkeysInFullscreen != value)
                {
                    settings.Properties.IgnoreHotkeysInFullscreen = value;
                    UpdateSettings();
                }
            }
        }

        public bool ClearInputOnLaunch
        {
            get
            {
                return settings.Properties.ClearInputOnLaunch;
            }

            set
            {
                if (settings.Properties.ClearInputOnLaunch != value)
                {
                    settings.Properties.ClearInputOnLaunch = value;
                    UpdateSettings();
                }
            }
        }

        public bool DisableDriveDetectionWarning
        {
            get
            {
                return settings.Properties.DisableDriveDetectionWarning;
            }

            set
            {
                if (settings.Properties.DisableDriveDetectionWarning != value)
                {
                    settings.Properties.DisableDriveDetectionWarning = value;
                    UpdateSettings();
                }
            }
        }
    }
}
