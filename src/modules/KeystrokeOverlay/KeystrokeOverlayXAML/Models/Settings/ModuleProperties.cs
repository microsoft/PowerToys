// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace KeystrokeOverlayUI.Models
{
    public class ModuleProperties
    {
        [JsonPropertyName("enable_draggable_overlay")]
        public BoolProperty IsDraggable { get; set; } = new() { Value = true };

        [JsonPropertyName("switch_monitor_hotkey")]
        public HotkeyProperty SwitchMonitorHotkey { get; set; } = new();

        [JsonPropertyName("display_mode")]
        public IntProperty DisplayMode { get; set; } = new() { Value = 0 };

        [JsonPropertyName("overlay_timeout")]
        public IntProperty OverlayTimeout { get; set; } = new() { Value = 3000 };

        [JsonPropertyName("text_size")]
        public IntProperty TextSize { get; set; } = new() { Value = 24 };

        [JsonPropertyName("text_color")]
        public StringProperty TextColor { get; set; } = new() { Value = "#FF000000" };

        [JsonPropertyName("background_color")]
        public StringProperty BackgroundColor { get; set; } = new() { Value = "#00000000" };
    }
}
