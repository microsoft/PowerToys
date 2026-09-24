// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Models
{
    /// <summary>
    /// Extracts the model identifier shared by monitors of the same model without
    /// retaining the per-instance part of a Windows monitor device path.
    /// </summary>
    public static class MonitorHardwareId
    {
        /// <summary>
        /// Extracts the EDID PnP identifier from a canonical Monitor.Id or a raw
        /// QueryDisplayConfig device path. Unrecognized forms return an empty string.
        /// </summary>
        public static string EdidIdFromMonitorId(string? monitorId)
        {
            if (string.IsNullOrEmpty(monitorId))
            {
                return string.Empty;
            }

            // Both forms place the EDID identifier between the first two '#' separators.
            var parts = monitorId.Split('#');
            if (parts.Length < 3 || string.IsNullOrEmpty(parts[1]))
            {
                return string.Empty;
            }

            return parts[1];
        }
    }
}
