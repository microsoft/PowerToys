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
        public const string DefaultLaserColor = "#CCFF2D2D";

        // -1 = None, 0 = Left, 1 = Right, 2 = Middle, 3 = X1 (back), 4 = X2 (forward).
        public const int ActivationButtonNone = -1;
        public const int ActivationButtonMiddle = 2;

        [CmdConfigureIgnore]
        public HotkeySettings DefaultActivationShortcut => new HotkeySettings(true, false, false, true, 0x4C);

        // Neither of these has a default. Leaving one unset is how that half of the
        // module stays switched off.
        [CmdConfigureIgnore]
        public HotkeySettings DefaultPenActivationShortcut => new HotkeySettings();

        // Ctrl+Shift+Win+W shares - it starts sharing, or moves the share to the next
        // window without ever dropping the shared surface. Ctrl+Shift+Win+Q quits. Two
        // one-way shortcuts rather than a toggle, so neither can do the opposite of what
        // was meant. Arguments are (win, ctrl, alt, shift, code).
        [CmdConfigureIgnore]
        public HotkeySettings DefaultPresenterActivationShortcut => new HotkeySettings(true, true, false, true, 0x57);

        [CmdConfigureIgnore]
        public HotkeySettings DefaultPresenterStopShortcut => new HotkeySettings(true, true, false, true, 0x51);

        [JsonPropertyName("activation_shortcut")]
        public HotkeySettings ActivationShortcut { get; set; }

        [JsonPropertyName("pen_activation_shortcut")]
        public HotkeySettings PenActivationShortcut { get; set; }

        // Starts sharing, or moves an existing share to whatever window the pointer is
        // over. Independent of the laser itself, so a share can stand for a whole
        // presentation. The json key is unchanged so existing bindings survive.
        [JsonPropertyName("presenter_activation_shortcut")]
        public HotkeySettings PresenterActivationShortcut { get; set; }

        [JsonPropertyName("presenter_stop_shortcut")]
        public HotkeySettings PresenterStopShortcut { get; set; }

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
            PresenterActivationShortcut = DefaultPresenterActivationShortcut;
            PresenterStopShortcut = DefaultPresenterStopShortcut;
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
