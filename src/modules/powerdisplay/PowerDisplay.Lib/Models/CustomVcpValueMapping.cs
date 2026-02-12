// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using PowerDisplay.Common.Utils;

namespace PowerDisplay.Common.Models
{
    /// <summary>
    /// Represents a custom name mapping for a VCP code value.
    /// Used to override the default VCP value names with user-defined names.
    /// This class is shared between PowerDisplay app and Settings UI.
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

        /// <summary>
        /// Gets or sets the target monitor display name (for UI display only, not serialized).
        /// </summary>
        [JsonIgnore]
        public string TargetMonitorName { get; set; } = string.Empty;

        /// <summary>
        /// Gets the display name for the VCP code (for UI display).
        /// Uses VcpNames.GetCodeName() to get the standard MCCS VCP code name.
        /// Note: For localized display in Settings UI, use VcpCodeToDisplayNameConverter instead.
        /// </summary>
        [JsonIgnore]
        public string VcpCodeDisplayName => VcpNames.GetCodeName(VcpCode);

        /// <summary>
        /// Gets the display name for the VCP value (using built-in mapping).
        /// </summary>
        [JsonIgnore]
        public string ValueDisplayName => VcpNames.GetFormattedValueName(VcpCode, Value);

        /// <summary>
        /// Gets a summary string for display in the UI list.
        /// Format: "OriginalValue → CustomName" or "OriginalValue → CustomName (MonitorName)"
        /// </summary>
        [JsonIgnore]
        public string DisplaySummary
        {
            get
            {
                var baseSummary = $"{VcpNames.GetValueName(VcpCode, Value) ?? $"0x{Value:X2}"} → {CustomName}";
                if (!ApplyToAll && !string.IsNullOrEmpty(TargetMonitorName))
                {
                    return $"{baseSummary} ({TargetMonitorName})";
                }

                return baseSummary;
            }
        }
    }
}
