// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

using Settings.UI.Library.Attributes;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class LaserPointerProperties
    {
        // 40% transparent: alpha 0x99 is 60% opaque.
        public const string DefaultLaserColor = "#99FF2D2D";

        // -1 = None, 0 = Left, 1 = Right, 2 = Middle, 3 = X1 (back), 4 = X2 (forward).
        public const int ActivationButtonNone = -1;
        public const int ActivationButtonMiddle = 2;

        [CmdConfigureIgnore]
        public HotkeySettings DefaultActivationShortcut => new HotkeySettings(true, false, false, true, 0x4C);

        // The pen shortcut has no default. Leaving it unset is how pen support is turned
        // off: nothing else can arm the pen.
        [CmdConfigureIgnore]
        public HotkeySettings DefaultPenActivationShortcut => new HotkeySettings();

        [JsonPropertyName("activation_shortcut")]
        public HotkeySettings ActivationShortcut { get; set; }

        [JsonPropertyName("pen_activation_shortcut")]
        public HotkeySettings PenActivationShortcut { get; set; }

        [JsonPropertyName("activation_button")]
        public IntProperty ActivationButton { get; set; }

        // Draws whenever held, with no shortcut needed. Left and Right are deliberately
        // not offered: permanently swallowing either would make the machine unusable.
        [JsonPropertyName("always_on_button")]
        public IntProperty AlwaysOnButton { get; set; }

        [JsonPropertyName("suppress_activation_button")]
        public BoolProperty SuppressActivationButton { get; set; }

        [JsonPropertyName("auto_activate")]
        public BoolProperty AutoActivate { get; set; }

        // false: the trail is drawn only while the tip touches the screen.
        // true: the trail follows the pen as soon as it is in range.
        [JsonPropertyName("pen_render_when_close")]
        public BoolProperty PenRenderWhenClose { get; set; }

        [JsonPropertyName("glow_enabled")]
        public BoolProperty GlowEnabled { get; set; }

        [JsonPropertyName("laser_color")]
        public StringProperty LaserColor { get; set; }

        [JsonPropertyName("laser_size")]
        public IntProperty LaserSize { get; set; }

        [JsonPropertyName("decay_time_ms")]
        public IntProperty DecayTimeMs { get; set; }

        [JsonPropertyName("decay_length")]
        public IntProperty DecayLength { get; set; }

        [JsonPropertyName("streamline")]
        public IntProperty Streamline { get; set; }

        public LaserPointerProperties()
        {
            ActivationShortcut = DefaultActivationShortcut;
            PenActivationShortcut = DefaultPenActivationShortcut;
            ActivationButton = new IntProperty(ActivationButtonMiddle);
            AlwaysOnButton = new IntProperty(ActivationButtonNone);
            SuppressActivationButton = new BoolProperty(true);
            AutoActivate = new BoolProperty(false);
            PenRenderWhenClose = new BoolProperty(false);
            GlowEnabled = new BoolProperty(true);
            LaserColor = new StringProperty(DefaultLaserColor);
            LaserSize = new IntProperty(10);

            // Defaults mirror the excalidraw laser pointer: a 1 second per point decay
            // and a taper spread over the trailing 50 samples.
            DecayTimeMs = new IntProperty(1000);
            DecayLength = new IntProperty(50);
            Streamline = new IntProperty(75);
        }
    }
}
