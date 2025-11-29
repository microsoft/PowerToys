// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace KeystrokeOverlayUI.Models
{
    public class SettingsRoot
    {
        [JsonPropertyName("enable_draggable_overlay")]
        public BoolProperty IsDraggable { get; set; } = new BoolProperty { Value = true };

        [JsonPropertyName("overlay_timeout")]
        public IntProperty OverlayTimeout { get; set; } = new IntProperty { Value = 3000 };

        [JsonPropertyName("text_size")]
        public IntProperty TextSize { get; set; } = new IntProperty { Value = 24 };

        [JsonPropertyName("text_opacity")]
        public IntProperty TextOpacity { get; set; } = new IntProperty { Value = 100 };

        [JsonPropertyName("background_opacity")]
        public IntProperty BackgroundOpacity { get; set; } = new IntProperty { Value = 50 };

        [JsonPropertyName("text_color")]
        public StringProperty TextColor { get; set; } = new StringProperty { Value = "#FFFFFF" };

        [JsonPropertyName("background_color")]
        public StringProperty BackgroundColor { get; set; } = new StringProperty { Value = "#000000" };
    }
}
