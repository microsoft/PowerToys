// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

using Settings.UI.Library.Attributes;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class CursorFocusProperties
    {
        [CmdConfigureIgnore]
        public HotkeySettings DefaultActivationShortcut => new HotkeySettings(true, false, true, false, 0x46); // Win + Alt + F

        [JsonPropertyName("activation_shortcut")]
        public HotkeySettings ActivationShortcut { get; set; }

        [JsonPropertyName("auto_activate")]
        public BoolProperty AutoActivate { get; set; }

        [JsonPropertyName("focus_change_delay_ms")]
        public IntProperty FocusChangeDelayMs { get; set; }

        [JsonPropertyName("target_position")]
        public IntProperty TargetPosition { get; set; }

        [JsonPropertyName("disable_on_fullscreen")]
        public BoolProperty DisableOnFullScreen { get; set; }

        [JsonPropertyName("disable_on_game_mode")]
        public BoolProperty DisableOnGameMode { get; set; }

        public CursorFocusProperties()
        {
            ActivationShortcut = DefaultActivationShortcut;
            AutoActivate = new BoolProperty(false);
            FocusChangeDelayMs = new IntProperty(200); // Default 200ms delay
            TargetPosition = new IntProperty(0); // 0=Center of window (default), 1=Center of title bar
            DisableOnFullScreen = new BoolProperty(false);
            DisableOnGameMode = new BoolProperty(false);
        }
    }
}
