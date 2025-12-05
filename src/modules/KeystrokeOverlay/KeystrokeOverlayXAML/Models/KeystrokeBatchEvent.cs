// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace KeystrokeOverlayUI.Models
{
    /// <summary>
    /// Single keystroke event emitted by the native Batcher.
    /// Matches native JSON fields: t, vk, text, mods, ts.
    /// </summary>
    public sealed class KeystrokeBatchEvent
    {
        [JsonPropertyName("t")]
        public string Type { get; set; }

        [JsonPropertyName("vk")]
        public int VirtualKey { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; }

        [JsonPropertyName("mods")]
        public string[] Modifiers { get; set; }

        [JsonPropertyName("ts")]
        public double Timestamp { get; set; }
    }
}
