// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace KeystrokeOverlayUI.Models
{
    /// <summary>
    /// Root object wrapping an array of keystroke events.
    /// Matches native JSON: { "schema": 1, "events": [ ... ] }.
    /// </summary>
    public sealed class KeystrokeBatchRoot
    {
        [JsonPropertyName("schema")]
        public int Schema { get; set; }

        [JsonPropertyName("events")]
        public KeystrokeBatchEvent[] Events { get; set; }
    }
}
