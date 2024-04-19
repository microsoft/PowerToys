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

        public MouseHighlighterProperties()
        {
            ActivationShortcut = DefaultActivationShortcut;
            LeftButtonClickColor = new StringProperty("#a6FFFF00");
            RightButtonClickColor = new StringProperty("#a60000FF");
            AlwaysColor = new StringProperty("#00FF0000");
            HighlightOpacity = new IntProperty(166); // for migration from <=1.1 to 1.2
            HighlightRadius = new IntProperty(20);
            HighlightFadeDelayMs = new IntProperty(500);
            HighlightFadeDurationMs = new IntProperty(250);
            AutoActivate = new BoolProperty(false);
        }
    }
}
