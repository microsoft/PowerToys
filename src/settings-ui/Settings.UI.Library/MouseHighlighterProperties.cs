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

        // Input Highlighter keystroke-overlay defaults.
        [CmdConfigureIgnore]
        public HotkeySettings DefaultKeystrokeSwitchMonitorHotkey => new HotkeySettings(true, true, false, false, 0xBF); // Win+Ctrl+/

        [CmdConfigureIgnore]
        public HotkeySettings DefaultKeystrokeSwitchDisplayModeHotkey => new HotkeySettings(true, true, false, false, 0x44); // Win+Ctrl+D

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

        // ---- Input Highlighter sub-toggles ----
        [JsonPropertyName("show_mouse")]
        public BoolProperty ShowMouse { get; set; }

        [JsonPropertyName("show_keystrokes")]
        public BoolProperty ShowKeystrokes { get; set; }

        // ---- Keystroke overlay properties ----
        [JsonPropertyName("keystroke_switch_monitor_hotkey")]
        public HotkeySettings KeystrokeSwitchMonitorHotkey { get; set; }

        [JsonPropertyName("keystroke_switch_display_mode_hotkey")]
        public HotkeySettings KeystrokeSwitchDisplayModeHotkey { get; set; }

        // 0 = Last5, 1 = SingleCharactersOnly, 2 = ShortcutsOnly, 3 = Stream
        [JsonPropertyName("keystroke_display_mode")]
        public IntProperty KeystrokeDisplayMode { get; set; }

        // 0 = TopLeft, 1 = TopCenter, 2 = TopRight, 3 = BottomLeft, 4 = BottomCenter, 5 = BottomRight
        [JsonPropertyName("keystroke_position")]
        public IntProperty KeystrokePosition { get; set; }

        [JsonPropertyName("keystroke_timeout_ms")]
        public IntProperty KeystrokeTimeoutMs { get; set; }

        [JsonPropertyName("keystroke_text_size")]
        public IntProperty KeystrokeTextSize { get; set; }

        [JsonPropertyName("keystroke_text_color")]
        public StringProperty KeystrokeTextColor { get; set; }

        [JsonPropertyName("keystroke_background_color")]
        public StringProperty KeystrokeBackgroundColor { get; set; }

        [JsonPropertyName("keystroke_stroke_color")]
        public StringProperty KeystrokeStrokeColor { get; set; }

        [JsonPropertyName("keystroke_stroke_thickness")]
        public IntProperty KeystrokeStrokeThickness { get; set; }

        [JsonPropertyName("keystroke_draggable")]
        public BoolProperty KeystrokeDraggable { get; set; }

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

            // Input Highlighter sub-toggles. Fresh installs get both halves enabled;
            // existing Mouse Highlighter users are migrated to mouse-only in
            // MouseHighlighterSettings.UpgradeSettingsConfiguration to avoid surprise.
            ShowMouse = new BoolProperty(true);
            ShowKeystrokes = new BoolProperty(true);

            KeystrokeSwitchMonitorHotkey = DefaultKeystrokeSwitchMonitorHotkey;
            KeystrokeSwitchDisplayModeHotkey = DefaultKeystrokeSwitchDisplayModeHotkey;
            KeystrokeDisplayMode = new IntProperty(0); // Last5
            KeystrokePosition = new IntProperty(4); // BottomCenter
            KeystrokeTimeoutMs = new IntProperty(3000);
            KeystrokeTextSize = new IntProperty(24);
            KeystrokeTextColor = new StringProperty("#FFFFFFFF");
            KeystrokeBackgroundColor = new StringProperty("#80000000");
            KeystrokeStrokeColor = new StringProperty("#00FFFFFF");
            KeystrokeStrokeThickness = new IntProperty(0);
            KeystrokeDraggable = new BoolProperty(true);
        }
    }
}
