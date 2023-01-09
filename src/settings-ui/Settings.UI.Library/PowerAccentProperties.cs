// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Enumerations;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class PowerAccentProperties
    {
        [JsonPropertyName("activation_key")]
        public PowerAccentActivationKey ActivationKey { get; set; }

        [JsonPropertyName("toolbar_position")]
        public StringProperty ToolbarPosition { get; set; }

        [JsonPropertyName("input_time_ms")]
        public IntProperty InputTime { get; set; }

        [JsonPropertyName("selected_lang")]
        public StringProperty SelectedLang { get; set; }

        [JsonPropertyName("excluded_apps")]
        public StringProperty ExcludedApps { get; set; }

        [JsonPropertyName("show_description")]
        public bool ShowUnicodeDescription { get; set; }

        [JsonPropertyName("sort_by_usage_frequency")]
        public bool SortByUsageFrequency { get; set; }

        [JsonPropertyName("start_selection_from_the_left")]
        public bool StartSelectionFromTheLeft { get; set; }

        public PowerAccentProperties()
        {
            ActivationKey = PowerAccentActivationKey.Both;
            ToolbarPosition = "Top center";
            InputTime = new IntProperty(PowerAccentSettings.DefaultInputTimeMs);
            SelectedLang = "ALL";
            ExcludedApps = new StringProperty();
            ShowUnicodeDescription = false;
            SortByUsageFrequency = false;
            StartSelectionFromTheLeft = false;
        }
    }
}
