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
                settings.Properties.open_powerlauncher.Alt = true;
                settings.Properties.open_powerlauncher.Code = (int)Windows.System.VirtualKey.Space;
                settings.Properties.maximum_number_of_results = 4;
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
                return settings.Properties.search_result_preference;
            }

            set
            {
                if (settings.Properties.search_result_preference != value)
                {
                    settings.Properties.search_result_preference = value;
                    UpdateSettings();
                }
            }
        }

        public string SearchTypePreference
        {
            get
            {
                return settings.Properties.search_type_preference;
            }

            set
            {
                if (settings.Properties.search_type_preference != value)
                {
                    settings.Properties.search_type_preference = value;
                    UpdateSettings();
                }
            }
        }

        public int MaximumNumberOfResults
        {
            get
            {
                return settings.Properties.maximum_number_of_results;
            }

            set
            {
                if (settings.Properties.maximum_number_of_results != value)
                {
                    settings.Properties.maximum_number_of_results = value;
                    UpdateSettings();
                }
            }
        }

        public HotkeySettings OpenPowerLauncher
        {
            get
            {
                return settings.Properties.open_powerlauncher;
            }

            set
            {
                if (settings.Properties.open_powerlauncher != value)
                {
                    settings.Properties.open_powerlauncher = value;
                    UpdateSettings();
                }
            }
        }

        public HotkeySettings OpenFileLocation
        {
            get
            {
                return settings.Properties.open_file_location;
            }

            set
            {
                if (settings.Properties.open_file_location != value)
                {
                    settings.Properties.open_file_location = value;
                    UpdateSettings();
                }
            }
        }

        public HotkeySettings CopyPathLocation
        {
            get
            {
                return settings.Properties.copy_path_location;
            }

            set
            {
                if (settings.Properties.copy_path_location != value)
                {
                    settings.Properties.copy_path_location = value;
                    UpdateSettings();
                }
            }
        }
        
        public bool OverrideWinRKey
        {
            get
            {
                return settings.Properties.override_win_r_key;
            }

            set
            {
                if (settings.Properties.override_win_r_key != value)
                {
                    settings.Properties.override_win_r_key = value;
                    UpdateSettings();
                }
            }
        }

        public bool OverrideWinSKey
        {
            get
            {
                return settings.Properties.override_win_s_key;
            }

            set
            {
                if (settings.Properties.override_win_s_key != value)
                {
                    settings.Properties.override_win_s_key = value;
                    UpdateSettings();
                }
            }
        }

        public bool IgnoreHotkeysInFullScreen
        {
            get
            {
                return settings.Properties.ignore_hotkeys_in_fullscreen;
            }

            set
            {
                if (settings.Properties.ignore_hotkeys_in_fullscreen != value)
                {
                    settings.Properties.ignore_hotkeys_in_fullscreen = value;
                    UpdateSettings();
                }
            }
        }

        public bool ClearInputOnLaunch
        {
	        get
	        {
		        return settings.Properties.clear_input_on_launch;
	        }

	        set
	        {
		        if (settings.Properties.clear_input_on_launch != value)
		        {
			        settings.Properties.clear_input_on_launch = value;
			        UpdateSettings();
		        }
	        }
        }

        public bool DisableDriveDetectionWarning
        {
            get
            {
                return settings.Properties.disable_drive_detection_warning;
            }

            set
            {
                if (settings.Properties.disable_drive_detection_warning != value)
                {
                    settings.Properties.disable_drive_detection_warning = value;
                    UpdateSettings();
                }
            }
        }
    }
}
