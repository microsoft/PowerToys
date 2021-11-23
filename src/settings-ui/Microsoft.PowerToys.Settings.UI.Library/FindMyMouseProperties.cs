// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class FindMyMouseProperties
    {
        [JsonPropertyName("do_not_activate_on_game_mode")]
        public BoolProperty DoNotActivateOnGameMode { get; set; }

        [JsonPropertyName("background_color")]
        public StringProperty BackgroundColor { get; set; }

        [JsonPropertyName("spotlight_color")]
        public StringProperty SpotlightColor { get; set; }

        [JsonPropertyName("overlay_opacity")]
        public IntProperty OverlayOpacity { get; set; }

        [JsonPropertyName("spotlight_radius")]
        public IntProperty SpotlightRadius { get; set; }

        [JsonPropertyName("animation_duration_ms")]
        public IntProperty AnimationDurationMs { get; set; }

        [JsonPropertyName("spotlight_initial_zoom")]
        public IntProperty SpotlightInitialZoom { get; set; }

        public FindMyMouseProperties()
        {
            DoNotActivateOnGameMode = new BoolProperty(true);
            BackgroundColor = new StringProperty("#000000");
            SpotlightColor = new StringProperty("#FFFFFF");
            OverlayOpacity = new IntProperty(50);
            SpotlightRadius = new IntProperty(100);
            AnimationDurationMs = new IntProperty(500);
            SpotlightInitialZoom = new IntProperty(9);
        }
    }
}
