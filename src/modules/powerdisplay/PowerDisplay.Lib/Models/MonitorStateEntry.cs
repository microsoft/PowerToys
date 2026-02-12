// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json.Serialization;

namespace PowerDisplay.Common.Models
{
    /// <summary>
    /// Individual monitor state entry for JSON persistence.
    /// Stores the current state of a monitor's adjustable parameters.
    /// </summary>
    public sealed class MonitorStateEntry
    {
        /// <summary>
        /// Gets or sets the brightness level (0-100).
        /// </summary>
        [JsonPropertyName("brightness")]
        public int Brightness { get; set; }

        /// <summary>
        /// Gets or sets the color temperature VCP value.
        /// </summary>
        [JsonPropertyName("colorTemperature")]
        public int ColorTemperatureVcp { get; set; }

        /// <summary>
        /// Gets or sets the contrast level (0-100).
        /// </summary>
        [JsonPropertyName("contrast")]
        public int Contrast { get; set; }

        /// <summary>
        /// Gets or sets the volume level (0-100).
        /// </summary>
        [JsonPropertyName("volume")]
        public int Volume { get; set; }

        /// <summary>
        /// Gets or sets the raw capabilities string from DDC/CI.
        /// </summary>
        [JsonPropertyName("capabilitiesRaw")]
        public string? CapabilitiesRaw { get; set; }

        /// <summary>
        /// Gets or sets when this entry was last updated.
        /// </summary>
        [JsonPropertyName("lastUpdated")]
        public DateTime LastUpdated { get; set; }
    }
}
