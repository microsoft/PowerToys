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
        public HotkeySettings DefaultSwitchMonitorHotkey => new HotkeySettings(true, true, false, false, 0x4B);

        [CmdConfigureIgnore]
        public HotkeySettings DefaultActivationShortcut => new HotkeySettings(true, false, false, true, 0x4B);

        [JsonPropertyName("enable_keystrokeoverlay")]
        public BoolProperty IsEnabled { get; set; }

        [JsonPropertyName("activation_shortcut")]
        public HotkeySettings ActivationShortcut { get; set; }

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
        public StringProperty TextColor { get; set; }

        [JsonPropertyName("background_color")]
        public StringProperty BackgroundColor { get; set; }

        public KeystrokeOverlayProperties()
        {
            IsEnabled = new BoolProperty(true);
            SwitchMonitorHotkey = DefaultSwitchMonitorHotkey;
            ActivationShortcut = DefaultActivationShortcut;
            IsDraggableOverlayEnabled = new BoolProperty(true);
            DisplayMode = new IntProperty(0);
            OverlayTimeout = new IntProperty(3000);
            TextSize = new IntProperty(24);
            TextColor = new StringProperty("#FFFFFF");
            BackgroundColor = new StringProperty("#000000");
        }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
