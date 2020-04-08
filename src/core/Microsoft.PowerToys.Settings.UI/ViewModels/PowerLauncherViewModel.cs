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
        private const string POWERTOYNAME = "PowerLauncher";
        private PowerLauncherSettings settings;

        public PowerLauncherViewModel()
        {
            if (SettingsUtils.SettingsExists(POWERTOYNAME))
            {
                this.settings = SettingsUtils.GetSettings<PowerLauncherSettings>(POWERTOYNAME);
            }
            else
            {
                this.settings = new PowerLauncherSettings();
            }
        }

        private void UpdateSettings([CallerMemberName] string propertyName = null)
        {
            // Notify UI of property change
            this.OnPropertyChanged(propertyName);

            // Save settings to file
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
            };
            SettingsUtils.SaveSettings(JsonSerializer.Serialize(this.settings, options), POWERTOYNAME);

            // Propagate changes to Power Launcher through IPC
            var propertiesJson = JsonSerializer.Serialize(this.settings.properties);
            ShellPage.DefaultSndMSGCallback(
                string.Format("{{ \"{0}\": {1} }}", POWERTOYNAME, JsonSerializer.Serialize(this.settings.properties)));
        }

        public bool EnablePowerLauncher
        {
            get
            {
                return this.settings.properties.enable_powerlauncher;
            }

            set
            {
                if (this.settings.properties.enable_powerlauncher != value)
                {
                    this.settings.properties.enable_powerlauncher = value;
                    this.UpdateSettings();
                }
            }
        }

        public string SearchResultPreference
        {
            get
            {
                return this.settings.properties.search_result_preference;
            }

            set
            {
                if (this.settings.properties.search_result_preference != value)
                {
                    this.settings.properties.search_result_preference = value;
                    this.UpdateSettings();
                }
            }
        }

        public string SearchTypePreference
        {
            get
            {
                return this.settings.properties.search_type_preference;
            }

            set
            {
                if (this.settings.properties.search_type_preference != value)
                {
                    this.settings.properties.search_type_preference = value;
                    this.UpdateSettings();
                }
            }
        }

        public int MaximumNumberOfResults
        {
            get
            {
                return this.settings.properties.maximum_number_of_results;
            }

            set
            {
                if (this.settings.properties.maximum_number_of_results != value)
                {
                    this.settings.properties.maximum_number_of_results = value;
                    this.UpdateSettings();
                }
            }
        }

        public HotkeySettings OpenPowerLauncher
        {
            get
            {
                return this.settings.properties.open_powerlauncher;
            }

            set
            {
                if (this.settings.properties.open_powerlauncher != value)
                {
                    this.settings.properties.open_powerlauncher = value;
                    this.UpdateSettings();
                }
            }
        }

        public HotkeySettings OpenFileLocation
        {
            get
            {
                return this.settings.properties.open_file_location;
            }

            set
            {
                if (this.settings.properties.open_file_location != value)
                {
                    this.settings.properties.open_file_location = value;
                    this.UpdateSettings();
                }
            }
        }

        public HotkeySettings CopyPathLocation
        {
            get
            {
                return this.settings.properties.copy_path_location;
            }

            set
            {
                if (this.settings.properties.copy_path_location != value)
                {
                    this.settings.properties.copy_path_location = value;
                    this.UpdateSettings();
                }
            }
        }

        public HotkeySettings OpenConsole
        {
            get
            {
                return this.settings.properties.open_console;
            }

            set
            {
                if (this.settings.properties.open_console != value)
                {
                    this.settings.properties.open_console = value;
                    this.UpdateSettings();
                }
            }
        }

        public bool OverrideWinRKey
        {
            get
            {
                return this.settings.properties.override_win_r_key;
            }

            set
            {
                if (this.settings.properties.override_win_r_key != value)
                {
                    this.settings.properties.override_win_r_key = value;
                    this.UpdateSettings();
                }
            }
        }

        public bool OverrideWinSKey
        {
            get
            {
                return this.settings.properties.override_win_s_key;
            }

            set
            {
                if (this.settings.properties.override_win_s_key != value)
                {
                    this.settings.properties.override_win_s_key = value;
                    this.UpdateSettings();
                }
            }
        }
    }
}
