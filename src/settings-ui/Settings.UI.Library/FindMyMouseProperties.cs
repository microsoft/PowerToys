// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

using Settings.UI.Library.Attributes;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class FindMyMouseProperties
    {
        [CmdConfigureIgnore]
        public HotkeySettings DefaultActivationShortcut => new HotkeySettings(true, false, false, true, 0x46);

        [JsonPropertyName("activation_method")]
        public IntProperty ActivationMethod { get; set; }

        [JsonPropertyName("include_win_key")]
        public BoolProperty IncludeWinKey { get; set; }

        [JsonPropertyName("activation_shortcut")]
        public HotkeySettings ActivationShortcut { get; set; }

        [JsonPropertyName("do_not_activate_on_game_mode")]
        public BoolProperty DoNotActivateOnGameMode { get; set; }

        [JsonPropertyName("background_color")]
        public StringProperty BackgroundColor { get; set; }

        [JsonPropertyName("spotlight_color")]
        public StringProperty SpotlightColor { get; set; }

        [JsonPropertyName("spotlight_radius")]
        public IntProperty SpotlightRadius { get; set; }

        [JsonPropertyName("animation_duration_ms")]
        public IntProperty AnimationDurationMs { get; set; }

        [JsonPropertyName("spotlight_initial_zoom")]
        public IntProperty SpotlightInitialZoom { get; set; }

        [JsonPropertyName("excluded_apps")]
        public StringProperty ExcludedApps { get; set; }

        [JsonPropertyName("shaking_minimum_distance")]
        public IntProperty ShakingMinimumDistance { get; set; }

        [JsonPropertyName("shaking_interval_ms")]
        public IntProperty ShakingIntervalMs { get; set; }

        [JsonPropertyName("shaking_factor")]
        public IntProperty ShakingFactor { get; set; }

        public FindMyMouseProperties()
        {
            ActivationMethod = new IntProperty(0);
            IncludeWinKey = new BoolProperty(false);
            ActivationShortcut = DefaultActivationShortcut;
            DoNotActivateOnGameMode = new BoolProperty(true);
            BackgroundColor = new StringProperty("#80000000"); // ARGB (#AARRGGBB)
            SpotlightColor = new StringProperty("#80FFFFFF");
            SpotlightRadius = new IntProperty(100);
            AnimationDurationMs = new IntProperty(500);
            SpotlightInitialZoom = new IntProperty(9);
            ExcludedApps = new StringProperty();
            ShakingMinimumDistance = new IntProperty(1000);
            ShakingIntervalMs = new IntProperty(1000);
            ShakingFactor = new IntProperty(400);
        }
    }
}
