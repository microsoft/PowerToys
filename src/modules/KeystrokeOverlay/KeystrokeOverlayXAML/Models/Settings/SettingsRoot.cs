// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace KeystrokeOverlayUI.Models
{
    public class SettingsRoot
    {
        [JsonPropertyName("IsDraggableOverlayEnabled")]
        public BoolProperty IsDraggable { get; set; } = new BoolProperty { Value = true };

        [JsonPropertyName("OverlayTimeout")]
        public IntProperty OverlayTimeout { get; set; } = new IntProperty { Value = 3000 };

        [JsonPropertyName("TextSize")]
        public IntProperty TextSize { get; set; } = new IntProperty { Value = 24 };

        [JsonPropertyName("TextOpacity")]
        public IntProperty TextOpacity { get; set; } = new IntProperty { Value = 100 };

        [JsonPropertyName("BackgroundOpacity")]
        public IntProperty BackgroundOpacity { get; set; } = new IntProperty { Value = 50 };

        [JsonPropertyName("TextColor")]
        public StringProperty TextColor { get; set; } = new StringProperty { Value = "#FFFFFF" };

        [JsonPropertyName("BackgroundColor")]
        public StringProperty BackgroundColor { get; set; } = new StringProperty { Value = "#000000" };
    }
}
