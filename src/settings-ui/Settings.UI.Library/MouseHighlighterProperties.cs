// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

using Settings.UI.Library.Attributes;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class MouseHighlighterProperties
    {
        [CmdConfigureIgnore]
        public HotkeySettings DefaultActivationShortcut => new HotkeySettings(true, false, false, true, 0x48);

        [JsonPropertyName("activation_shortcut")]
        public HotkeySettings ActivationShortcut { get; set; }

        [JsonPropertyName("left_button_click_color")]
        public StringProperty LeftButtonClickColor { get; set; }

        [JsonPropertyName("right_button_click_color")]
        public StringProperty RightButtonClickColor { get; set; }

        [JsonPropertyName("highlight_opacity")]
        [CmdConfigureIgnore]
        public IntProperty HighlightOpacity { get; set; }

        [JsonPropertyName("always_color")]
        public StringProperty AlwaysColor { get; set; }

        [JsonPropertyName("highlight_radius")]
        public IntProperty HighlightRadius { get; set; }

        [JsonPropertyName("highlight_fade_delay_ms")]
        public IntProperty HighlightFadeDelayMs { get; set; }

        [JsonPropertyName("highlight_fade_duration_ms")]
        public IntProperty HighlightFadeDurationMs { get; set; }

        [JsonPropertyName("auto_activate")]
        public BoolProperty AutoActivate { get; set; }

        [JsonPropertyName("spotlight_mode")]
        public BoolProperty SpotlightMode { get; set; }

        [JsonPropertyName("ripple_mode")]
        public BoolProperty RippleMode { get; set; }

        [JsonPropertyName("ripple_size")]
        public IntProperty RippleSize { get; set; }

        [JsonPropertyName("ripple_intensity")]
        public DoubleProperty RippleIntensity { get; set; }

        [JsonPropertyName("ripple_duration_ms")]
        public IntProperty RippleDurationMs { get; set; }

        [JsonPropertyName("ripple_show_drag_trail")]
        public BoolProperty RippleShowDragTrail { get; set; }

        [JsonPropertyName("ripple_show_release_pulse")]
        public BoolProperty RippleShowReleasePulse { get; set; }

        public MouseHighlighterProperties()
        {
            ActivationShortcut = DefaultActivationShortcut;
            LeftButtonClickColor = new StringProperty("#a6FFFF00");
            RightButtonClickColor = new StringProperty("#a60000FF");
            AlwaysColor = new StringProperty("#00FF0000");
            HighlightOpacity = new IntProperty(166); // for migration from <=1.1 to 1.2
            HighlightRadius = new IntProperty(30);
            HighlightFadeDelayMs = new IntProperty(400);
            HighlightFadeDurationMs = new IntProperty(400);
            AutoActivate = new BoolProperty(false);
            SpotlightMode = new BoolProperty(false);
            RippleMode = new BoolProperty(true);
            RippleSize = new IntProperty(60);
            RippleIntensity = new DoubleProperty(0.7);
            RippleDurationMs = new IntProperty(480);
            RippleShowDragTrail = new BoolProperty(true);
            RippleShowReleasePulse = new BoolProperty(true);
        }
    }
}
