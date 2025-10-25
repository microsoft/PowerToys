// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

using Settings.UI.Library.Attributes;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class KeystrokeOverlayProperties
    {
        [CmdConfigureIgnore]
        public HotkeySettings DefaultOpenKeystrokeOverlay => new HotkeySettings(true, false, false, true, 0xBF);

        [JsonPropertyName("enable_keystrokeoverlay")]
        public BoolProperty IsEnabled { get; set; }

        [JsonPropertyName("switch_monitor_hotkey")]
        public HotkeySettings SwitchMonitorHotkey { get; set; }

        [JsonPropertyName("enable_draggable_overlay")]
        public BoolProperty IsDraggableOverlayEnabled { get; set; }

        [JsonPropertyName("display_mode")]
        public IntProperty DisplayMode { get; set; }

        [JsonPropertyName("overlay_timeout")]
        public IntProperty OverlayTimeout { get; set; }

        [JsonPropertyName("text_size")]
        public IntProperty TextSize { get; set; }

        [JsonPropertyName("text_color")]
        public IntProperty TextColor { get; set; }

        [JsonPropertyName("text_opacity")]
        public IntProperty TextOpacity { get; set; }

        [JsonPropertyName("background_color")]
        public IntProperty BackgroundColor { get; set; }

        [JsonPropertyName("background_opacity")]
        public IntProperty BackgroundOpacity { get; set; }

        public KeystrokeOverlayProperties()
        {
            IsEnabled = new BoolProperty(true);
            SwitchMonitorHotkey = new HotkeySettings(false, true, false, false, '0'); // Ctrl+0
            IsDraggableOverlayEnabled = new BoolProperty(true);
            DisplayMode = new IntProperty(0);
            OverlayTimeout = new IntProperty(3000);
            TextSize = new IntProperty(24);
            TextColor = new IntProperty(16777215); // White
            TextOpacity = new IntProperty(100);
            BackgroundColor = new IntProperty(0); // Black
            BackgroundOpacity = new IntProperty(50);
        }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
