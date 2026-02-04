// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace PowerDisplay.Common.Models
{
    /// <summary>
    /// Represents a custom name mapping for a VCP code value.
    /// Used to override the default VCP value names with user-defined names.
    /// </summary>
    public class CustomVcpValueMapping
    {
        /// <summary>
        /// Gets or sets the VCP code (e.g., 0x14 for color temperature, 0x60 for input source).
        /// </summary>
        [JsonPropertyName("vcpCode")]
        public byte VcpCode { get; set; }

        /// <summary>
        /// Gets or sets the VCP value to map (e.g., 0x11 for HDMI-1).
        /// </summary>
        [JsonPropertyName("value")]
        public int Value { get; set; }

        /// <summary>
        /// Gets or sets the custom name to display instead of the default name.
        /// </summary>
        [JsonPropertyName("customName")]
        public string CustomName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether this mapping applies to all monitors.
        /// When true, the mapping is applied globally. When false, only applies to TargetMonitorId.
        /// </summary>
        [JsonPropertyName("applyToAll")]
        public bool ApplyToAll { get; set; } = true;

        /// <summary>
        /// Gets or sets the target monitor ID when ApplyToAll is false.
        /// This is the monitor's unique identifier.
        /// </summary>
        [JsonPropertyName("targetMonitorId")]
        public string TargetMonitorId { get; set; } = string.Empty;
    }
}
