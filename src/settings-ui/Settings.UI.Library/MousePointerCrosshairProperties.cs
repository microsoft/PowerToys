// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class MousePointerCrosshairProperties
    {
        [JsonPropertyName("activation_shortcut")]
        public HotkeySettings ActivationShortcut { get; set; }

        [JsonPropertyName("crosshair_color")]
        public StringProperty CrosshairColor { get; set; }

        [JsonPropertyName("crosshair_opacity")]
        public IntProperty CrosshairOpacity { get; set; }

        [JsonPropertyName("crosshair_radius")]
        public IntProperty CrosshairRadius { get; set; }

        [JsonPropertyName("crosshair_thickness")]
        public IntProperty CrosshairThickness { get; set; }

        [JsonPropertyName("crosshair_border_color")]
        public StringProperty CrosshairBorderColor { get; set; }

        [JsonPropertyName("crosshair_border_size")]
        public IntProperty CrosshairBorderSize { get; set; }

        public MousePointerCrosshairProperties()
        {
            ActivationShortcut = new HotkeySettings(false, true, true, false, 0x50); // Ctrl + Alt + P
            CrosshairColor = new StringProperty("#FF0000");
            CrosshairOpacity = new IntProperty(75);
            CrosshairRadius = new IntProperty(20);
            CrosshairThickness = new IntProperty(5);
            CrosshairBorderColor = new StringProperty("#FFFFFF");
            CrosshairBorderSize = new IntProperty(1);
        }
    }
}
