// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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
        /// Generate a unique key for monitor matching based on Id.
        /// </summary>
        /// <param name="monitor">The monitor data to generate a key for.</param>
        /// <returns>A unique string key for the monitor.</returns>
        public static string GetMonitorKey(IMonitorData? monitor)
            => monitor?.Id ?? string.Empty;

        /// <summary>
        /// Check if two monitors are considered the same based on their Ids.
        /// </summary>
        /// <param name="monitor1">First monitor.</param>
        /// <param name="monitor2">Second monitor.</param>
        /// <returns>True if the monitors have the same Id.</returns>
        public static bool AreMonitorsSame(IMonitorData monitor1, IMonitorData monitor2)
        {
            if (monitor1 == null || monitor2 == null)
            {
                return false;
            }

            return !string.IsNullOrEmpty(monitor1.Id) && monitor1.Id == monitor2.Id;
        }
    }
}
