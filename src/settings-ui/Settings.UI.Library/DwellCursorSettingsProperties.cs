// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class DwellCursorSettingsProperties
    {
        [JsonPropertyName("activation_shortcut")]
        public HotkeySettings ActivationShortcut { get; set; } = DefaultActivationShortcut;

        public static HotkeySettings DefaultActivationShortcut => new HotkeySettings(true, false, true, false, 0x44); // Win + Alt + D

        [JsonPropertyName("delay_time_ms")]
        public IntProperty DelayTimeMs { get; set; } = new IntProperty() { Value = 1000 }; // 0.5s-10s
    }
}
