// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
﻿using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.Views;
using Windows.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class PowerLauncherViewModel : Observable
    {
        public PowerLauncherSettings settings;
        private const string POWERTOY_NAME = "PowerLauncher";


        public PowerLauncherViewModel()
        {
            if (SettingsUtils.SettingsExists(POWERTOY_NAME))
            {
                settings = SettingsUtils.GetSettings<PowerLauncherSettings>(POWERTOY_NAME);
            } else
            {
                settings = new PowerLauncherSettings();
            }
        }

        private void UpdateSettings([CallerMemberName] string propertyName = null)
        {
            // Notify UI of property change
            OnPropertyChanged(propertyName);

            // Save settings to file
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            SettingsUtils.SaveSettings(JsonSerializer.Serialize(settings, options), POWERTOY_NAME);

            // Propagate changes to Power Launcher through IPC
            var propertiesJson = JsonSerializer.Serialize(settings.properties);
            ShellPage.Default_SndMSG_Callback(
                string.Format("{{ \"{0}\": {1} }}", POWERTOY_NAME, JsonSerializer.Serialize(settings.properties)));
        }

        public bool EnablePowerLauncher
        {
            get { return settings.properties.enable_powerlauncher; }
            set 
            {
                if (settings.properties.enable_powerlauncher != value)
                {
                    settings.properties.enable_powerlauncher = value;
                    UpdateSettings();
                }
            }
        }

        public string SearchResultPreference
        {
            get { return settings.properties.search_result_preference;  }
            set 
            {
                if (settings.properties.search_result_preference != value)
                {
                    settings.properties.search_result_preference = value;
                    UpdateSettings();
                }
            }
        }

        public string SearchTypePreference
        {
            get { return settings.properties.search_type_preference; }
            set
            {
                if (settings.properties.search_type_preference != value)
                {
                    settings.properties.search_type_preference = value;
                    UpdateSettings();
                }
            }
        }

        public int MaximumNumberOfResults
        {
            get { return settings.properties.maximum_number_of_results; }
            set
            {
                if (settings.properties.maximum_number_of_results != value)
                {
                    settings.properties.maximum_number_of_results = value;
                    UpdateSettings();
                }
            }
        }

        public HotkeySettings OpenPowerLauncher
        {
            get { return settings.properties.open_powerlauncher; }
            set
            {
                if (settings.properties.open_powerlauncher != value)
                {
                    settings.properties.open_powerlauncher = value;
                    UpdateSettings();
                } 
            }
        }

        public HotkeySettings OpenFileLocation
        {
            get { return settings.properties.open_file_location; }
            set
            {
                if (settings.properties.open_file_location != value)
                {
                    settings.properties.open_file_location = value;
                    UpdateSettings();
                }
            }
        }

        public HotkeySettings CopyPathLocation
        {
            get { return settings.properties.copy_path_location; }
            set
            {
                if (settings.properties.copy_path_location != value)
                {
                    settings.properties.copy_path_location = value;
                    UpdateSettings();
                }
            }
        }

        public HotkeySettings OpenConsole
        {
            get { return settings.properties.open_console; }
            set
            {
                if (settings.properties.open_console != value)
                {
                    settings.properties.open_console = value;
                    UpdateSettings();
                }
            }
        }

        public bool OverrideWinRKey
        {
            get { return settings.properties.override_win_r_key; }
            set
            {
                if (settings.properties.override_win_r_key != value)
                {
                    settings.properties.override_win_r_key = value;
                    UpdateSettings();
                }
            }
        }

        public bool OverrideWinSKey
        {
            get { return settings.properties.override_win_s_key; }
            set
            {
                if (settings.properties.override_win_s_key != value)
                {
                    settings.properties.override_win_s_key = value;
                    UpdateSettings();
                }
            }
        }
    }
}