// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class MousePointerCrosshairsProperties
    {
        [JsonPropertyName("activation_shortcut")]
        public HotkeySettings ActivationShortcut { get; set; }

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

        public MousePointerCrosshairsProperties()
        {
            ActivationShortcut = new HotkeySettings(false, true, true, false, 0x50); // Ctrl + Alt + P
            CrosshairsColor = new StringProperty("#FF0000");
            CrosshairsOpacity = new IntProperty(75);
            CrosshairsRadius = new IntProperty(20);
            CrosshairsThickness = new IntProperty(5);
            CrosshairsBorderColor = new StringProperty("#FFFFFF");
            CrosshairsBorderSize = new IntProperty(1);
        }
    }
}
