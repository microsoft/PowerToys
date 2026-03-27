// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerDisplay.Common.Utils;
using PowerDisplay.Models;

namespace PowerDisplay.Common.Extensions
{
    /// <summary>
    /// Extension methods for <see cref="CustomVcpValueMapping"/> that provide display-related properties.
    /// These depend on <see cref="VcpNames"/> and are therefore kept in PowerDisplay.Lib
    /// rather than in the shared PowerDisplay.Models project.
    /// </summary>
    public static class CustomVcpValueMappingExtensions
    {
        /// <summary>
        /// Gets the display name for the VCP code (e.g., "Select Color Preset").
        /// </summary>
        public static string GetVcpCodeDisplayName(this CustomVcpValueMapping mapping)
        {
            return VcpNames.GetCodeName(mapping.VcpCode);
        }

        /// <summary>
        /// Gets the formatted display name for the VCP value (e.g., "6500K (0x05)").
        /// </summary>
        public static string GetValueDisplayName(this CustomVcpValueMapping mapping)
        {
            return VcpNames.GetFormattedValueName(mapping.VcpCode, mapping.Value);
        }

        /// <summary>
        /// Gets a summary string for display in the UI list.
        /// Format: "OriginalValue → CustomName" or "OriginalValue → CustomName (MonitorName)"
        /// </summary>
        public static string GetDisplaySummary(this CustomVcpValueMapping mapping)
        {
            var baseSummary = $"{VcpNames.GetValueName(mapping.VcpCode, mapping.Value) ?? $"0x{mapping.Value:X2}"} \u2192 {mapping.CustomName}";
            if (!mapping.ApplyToAll && !string.IsNullOrEmpty(mapping.TargetMonitorName))
            {
                return $"{baseSummary} ({mapping.TargetMonitorName})";
            }

            return baseSummary;
        }
    }
}
