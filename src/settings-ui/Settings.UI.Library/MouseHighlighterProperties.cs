// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class MouseHighlighterProperties
    {
        [JsonPropertyName("activation_shortcut")]
        public HotkeySettings ActivationShortcut { get; set; }

        [JsonPropertyName("left_button_click_color")]
        public StringProperty LeftButtonClickColor { get; set; }

        [JsonPropertyName("right_button_click_color")]
        public StringProperty RightButtonClickColor { get; set; }

        [JsonPropertyName("highlight_opacity")]
        public IntProperty HighlightOpacity { get; set; }

        [JsonPropertyName("highlight_radius")]
        public IntProperty HighlightRadius { get; set; }

        [JsonPropertyName("highlight_fade_delay_ms")]
        public IntProperty HighlightFadeDelayMs { get; set; }

        [JsonPropertyName("highlight_fade_duration_ms")]
        public IntProperty HighlightFadeDurationMs { get; set; }

        public MouseHighlighterProperties()
        {
            ActivationShortcut = new HotkeySettings(true, false, false, true, 0x48);
            LeftButtonClickColor = new StringProperty("#FFFF00");
            RightButtonClickColor = new StringProperty("#0000FF");
            HighlightOpacity = new IntProperty(160);
            HighlightRadius = new IntProperty(20);
            HighlightFadeDelayMs = new IntProperty(500);
            HighlightFadeDurationMs = new IntProperty(250);
        }
    }
}
