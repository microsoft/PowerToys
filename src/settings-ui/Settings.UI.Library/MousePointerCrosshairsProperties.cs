// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

using Settings.UI.Library.Attributes;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class MousePointerCrosshairsProperties
    {
        [CmdConfigureIgnore]
        public HotkeySettings DefaultActivationShortcut => new HotkeySettings(true, false, true, false, 0x50); // Win + Alt + P

        [CmdConfigureIgnore]
        public HotkeySettings DefaultGlidingCursorActivationShortcut => new HotkeySettings(true, false, true, false, 0xBE); // Win + Alt + .

        [JsonPropertyName("activation_shortcut")]
        public HotkeySettings ActivationShortcut { get; set; }

        [JsonPropertyName("gliding_cursor_activation_shortcut")]
        public HotkeySettings GlidingCursorActivationShortcut { get; set; }

        [JsonPropertyName("crosshairs_color")]
        public StringProperty CrosshairsColor { get; set; }

        [JsonPropertyName("crosshairs_opacity")]
        public IntProperty CrosshairsOpacity { get; set; }

        [JsonPropertyName("crosshairs_radius")]
        public IntProperty CrosshairsRadius { get; set; }

        [JsonPropertyName("crosshairs_thickness")]
        public IntProperty CrosshairsThickness { get; set; }

        [JsonPropertyName("crosshairs_border_color")]
        public StringProperty CrosshairsBorderColor { get; set; }

        [JsonPropertyName("crosshairs_border_size")]
        public IntProperty CrosshairsBorderSize { get; set; }

        [JsonPropertyName("crosshairs_orientation")]
        public IntProperty CrosshairsOrientation { get; set; }

        [JsonPropertyName("crosshairs_auto_hide")]
        public BoolProperty CrosshairsAutoHide { get; set; }

        [JsonPropertyName("crosshairs_is_fixed_length_enabled")]
        public BoolProperty CrosshairsIsFixedLengthEnabled { get; set; }

        [JsonPropertyName("crosshairs_fixed_length")]
        public IntProperty CrosshairsFixedLength { get; set; }

        [JsonPropertyName("auto_activate")]
        public BoolProperty AutoActivate { get; set; }

        [JsonPropertyName("gliding_travel_speed")]
        public IntProperty GlidingTravelSpeed { get; set; }

        [JsonPropertyName("gliding_delay_speed")]
        public IntProperty GlidingDelaySpeed { get; set; }

        public MousePointerCrosshairsProperties()
        {
            ActivationShortcut = DefaultActivationShortcut;
            GlidingCursorActivationShortcut = DefaultGlidingCursorActivationShortcut;
            CrosshairsColor = new StringProperty("#FF0000");
            CrosshairsOpacity = new IntProperty(75);
            CrosshairsRadius = new IntProperty(20);
            CrosshairsThickness = new IntProperty(5);
            CrosshairsBorderColor = new StringProperty("#FFFFFF");
            CrosshairsBorderSize = new IntProperty(1);
            CrosshairsOrientation = new IntProperty(0); // Default to both (0=Both, 1=Vertical, 2=Horizontal)
            CrosshairsAutoHide = new BoolProperty(false);
            CrosshairsIsFixedLengthEnabled = new BoolProperty(false);
            CrosshairsFixedLength = new IntProperty(1);
            AutoActivate = new BoolProperty(false);
            GlidingTravelSpeed = new IntProperty(25);
            GlidingDelaySpeed = new IntProperty(5);
        }
    }
}
