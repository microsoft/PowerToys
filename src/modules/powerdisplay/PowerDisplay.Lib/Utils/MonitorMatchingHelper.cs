// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerDisplay.Common.Interfaces;

namespace PowerDisplay.Common.Utils
{
    /// <summary>
    /// Helper class for monitor matching and identification.
    /// Provides consistent logic for matching monitors across different data sources.
    /// </summary>
    public static class MonitorMatchingHelper
    {
        /// <summary>
        /// Generate a unique key for monitor matching based on hardware ID and internal name.
        /// Uses HardwareId if available, otherwise falls back to Id (InternalName) or Name.
        /// </summary>
        /// <param name="monitor">The monitor data to generate a key for.</param>
        /// <returns>A unique string key for the monitor.</returns>
        public static string GetMonitorKey(IMonitorData monitor)
        {
            if (monitor == null)
            {
                return string.Empty;
            }

            // Use hardware ID if available (most stable identifier)
            if (!string.IsNullOrEmpty(monitor.HardwareId))
            {
                return monitor.HardwareId;
            }

            // Fall back to Id (InternalName in MonitorInfo)
            if (!string.IsNullOrEmpty(monitor.Id))
            {
                return monitor.Id;
            }

            // Last resort: use display name
            return monitor.Name ?? string.Empty;
        }

        /// <summary>
        /// Generate a unique key for monitor matching using explicit values.
        /// Useful when you don't have an IMonitorData object.
        /// </summary>
        /// <param name="hardwareId">The monitor's hardware ID.</param>
        /// <param name="internalName">The monitor's internal name (optional fallback).</param>
        /// <param name="name">The monitor's display name (optional fallback).</param>
        /// <returns>A unique string key for the monitor.</returns>
        public static string GetMonitorKey(string? hardwareId, string? internalName = null, string? name = null)
        {
            // Use hardware ID if available (most stable identifier)
            if (!string.IsNullOrEmpty(hardwareId))
            {
                return hardwareId;
            }

            // Fall back to internal name
            if (!string.IsNullOrEmpty(internalName))
            {
                return internalName;
            }

            // Last resort: use display name
            return name ?? string.Empty;
        }

        /// <summary>
        /// Check if two monitors are considered the same based on their keys.
        /// </summary>
        /// <param name="monitor1">First monitor.</param>
        /// <param name="monitor2">Second monitor.</param>
        /// <returns>True if the monitors have the same key.</returns>
        public static bool AreMonitorsSame(IMonitorData monitor1, IMonitorData monitor2)
        {
            if (monitor1 == null || monitor2 == null)
            {
                return false;
            }

            return GetMonitorKey(monitor1) == GetMonitorKey(monitor2);
        }
    }
}
