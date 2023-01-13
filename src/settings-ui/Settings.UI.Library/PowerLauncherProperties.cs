// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using ManagedCommon;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class PowerLauncherProperties
    {
        [JsonPropertyName("search_result_preference")]
        public string SearchResultPreference { get; set; }

        [JsonPropertyName("search_type_preference")]
        public string SearchTypePreference { get; set; }

        [JsonPropertyName("maximum_number_of_results")]
        public int MaximumNumberOfResults { get; set; }

        [JsonPropertyName("open_powerlauncher")]
        public HotkeySettings OpenPowerLauncher { get; set; }

        [JsonPropertyName("open_file_location")]
        public HotkeySettings OpenFileLocation { get; set; }

        [JsonPropertyName("copy_path_location")]
        public HotkeySettings CopyPathLocation { get; set; }

        [JsonPropertyName("open_console")]
        public HotkeySettings OpenConsole { get; set; }

        [JsonPropertyName("override_win_r_key")]
        public bool OverrideWinkeyR { get; set; }

        [JsonPropertyName("override_win_s_key")]
        public bool OverrideWinkeyS { get; set; }

        [JsonPropertyName("ignore_hotkeys_in_fullscreen")]
        public bool IgnoreHotkeysInFullscreen { get; set; }

        [JsonPropertyName("clear_input_on_launch")]
        public bool ClearInputOnLaunch { get; set; }

        [JsonPropertyName("tab_selects_context_buttons")]
        public bool TabSelectsContextButtons { get; set; }

        [JsonPropertyName("theme")]
        public Theme Theme { get; set; }

        [JsonPropertyName("startupPosition")]
        public StartupPosition Position { get; set; }

        [JsonPropertyName("use_centralized_keyboard_hook")]
        public bool UseCentralizedKeyboardHook { get; set; }

        [JsonPropertyName("search_query_results_with_delay")]
        public bool SearchQueryResultsWithDelay { get; set; }

        [JsonPropertyName("search_input_delay")]
        public int SearchInputDelay { get; set; }

        [JsonPropertyName("search_input_delay_fast")]
        public int SearchInputDelayFast { get; set; }

        [JsonPropertyName("search_clicked_item_weight")]
        public int SearchClickedItemWeight { get; set; }

        [JsonPropertyName("search_query_tuning_enabled")]
        public bool SearchQueryTuningEnabled { get; set; }

        [JsonPropertyName("search_wait_for_slow_results")]
        public bool SearchWaitForSlowResults { get; set; }

        public PowerLauncherProperties()
        {
            OpenPowerLauncher = new HotkeySettings(false, false, true, false, 32);
            OpenFileLocation = new HotkeySettings();
            CopyPathLocation = new HotkeySettings();
            OpenConsole = new HotkeySettings();
            SearchResultPreference = "most_recently_used";
            SearchTypePreference = "application_name";
            IgnoreHotkeysInFullscreen = false;
            ClearInputOnLaunch = false;
            TabSelectsContextButtons = true;
            MaximumNumberOfResults = 4;
            Theme = Theme.System;
            Position = StartupPosition.Cursor;
            UseCentralizedKeyboardHook = false;
            SearchQueryResultsWithDelay = true;
            SearchInputDelay = 150;
            SearchClickedItemWeight = 5;
            SearchQueryTuningEnabled = false;
            SearchWaitForSlowResults = false;
        }
    }
}
