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
        public static string GetMonitorKey(IMonitorData? monitor)
            => GetMonitorKey(monitor?.HardwareId, monitor?.Id, monitor?.Name);

        /// <summary>
        /// Generate a unique key for monitor matching using explicit values.
        /// Uses priority: HardwareId > InternalName > Name.
        /// </summary>
        /// <param name="hardwareId">The monitor's hardware ID.</param>
        /// <param name="internalName">The monitor's internal name (optional fallback).</param>
        /// <param name="name">The monitor's display name (optional fallback).</param>
        /// <returns>A unique string key for the monitor.</returns>
        public static string GetMonitorKey(string? hardwareId, string? internalName = null, string? name = null)
            => !string.IsNullOrEmpty(hardwareId) ? hardwareId
             : !string.IsNullOrEmpty(internalName) ? internalName
             : name ?? string.Empty;

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
