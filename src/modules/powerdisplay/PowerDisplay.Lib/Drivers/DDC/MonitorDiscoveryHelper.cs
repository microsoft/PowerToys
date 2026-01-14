// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using ManagedCommon;
using PowerDisplay.Common.Models;
using static PowerDisplay.Common.Drivers.NativeConstants;
using static PowerDisplay.Common.Drivers.PInvoke;
using PHYSICAL_MONITOR = PowerDisplay.Common.Drivers.PhysicalMonitor;

namespace PowerDisplay.Common.Drivers.DDC
{
    /// <summary>
    /// Helper class for discovering and creating monitor objects
    /// </summary>
    public class MonitorDiscoveryHelper
    {
        /// <summary>
        /// Get physical monitors for a logical monitor.
        /// Filters out any monitors with NULL handles (Windows API bug workaround).
        /// </summary>
        /// <param name="hMonitor">Handle to the logical monitor</param>
        /// <param name="hasNullHandles">Output: true if any NULL handles were filtered out</param>
        /// <returns>Array of valid physical monitors, or null if API call failed</returns>
        internal PHYSICAL_MONITOR[]? GetPhysicalMonitors(IntPtr hMonitor, out bool hasNullHandles)
        {
            hasNullHandles = false;

            try
            {
                if (!GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out uint numMonitors))
                {
                    Logger.LogWarning($"GetPhysicalMonitors: GetNumberOfPhysicalMonitorsFromHMONITOR failed for 0x{hMonitor:X}");
                    return null;
                }

                if (numMonitors == 0)
                {
                    Logger.LogWarning($"GetPhysicalMonitors: numMonitors is 0");
                    return null;
                }

                var physicalMonitors = new PHYSICAL_MONITOR[numMonitors];
                bool apiResult;
                unsafe
                {
                    fixed (PHYSICAL_MONITOR* ptr = physicalMonitors)
                    {
                        apiResult = GetPhysicalMonitorsFromHMONITOR(hMonitor, numMonitors, ptr);
                    }
                }

                if (!apiResult)
                {
                    Logger.LogWarning($"GetPhysicalMonitors: GetPhysicalMonitorsFromHMONITOR failed");
                    return null;
                }

                // Filter out NULL handles and log each physical monitor
                var validMonitors = new List<PHYSICAL_MONITOR>();
                for (int i = 0; i < numMonitors; i++)
                {
                    IntPtr handle = physicalMonitors[i].HPhysicalMonitor;

                    if (handle == IntPtr.Zero)
                    {
                        Logger.LogWarning($"GetPhysicalMonitors: Monitor [{i}] has NULL handle, filtering out");
                        hasNullHandles = true;
                        continue;
                    }

                    validMonitors.Add(physicalMonitors[i]);
                }

                return validMonitors.Count > 0 ? validMonitors.ToArray() : null;
            }
            catch (Exception ex)
            {
                Logger.LogError($"GetPhysicalMonitors: Exception: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Create Monitor object from physical monitor and display info.
        /// Uses MonitorDisplayInfo directly from QueryDisplayConfig for stable identification.
        /// Note: Brightness is not initialized here - MonitorManager handles brightness initialization
        /// after discovery to avoid slow I2C operations during the discovery phase.
        /// </summary>
        /// <param name="physicalMonitor">Physical monitor structure with handle and description</param>
        /// <param name="monitorInfo">Display info from QueryDisplayConfig (EdidId, FriendlyName, MonitorNumber)</param>
        internal Monitor? CreateMonitorFromPhysical(
            PHYSICAL_MONITOR physicalMonitor,
            MonitorDisplayInfo monitorInfo)
        {
            try
            {
                // Get EDID ID and friendly name directly from MonitorDisplayInfo
                string edidId = monitorInfo.EdidId ?? string.Empty;
                string name = physicalMonitor.GetDescription() ?? string.Empty;

                // Use FriendlyName from QueryDisplayConfig if available and not generic
                if (!string.IsNullOrEmpty(monitorInfo.FriendlyName) &&
                    !monitorInfo.FriendlyName.Contains("Generic"))
                {
                    name = monitorInfo.FriendlyName;
                }

                // Generate unique monitor Id: "DDC_{EdidId}_{MonitorNumber}"
                string monitorId = !string.IsNullOrEmpty(edidId)
                    ? $"DDC_{edidId}_{monitorInfo.MonitorNumber}"
                    : $"DDC_Unknown_{monitorInfo.MonitorNumber}";

                // If still no good name, use default value
                if (string.IsNullOrEmpty(name) || name.Contains("Generic") || name.Contains("PnP"))
                {
                    name = "External Display";
                }

                var monitor = new Monitor
                {
                    Id = monitorId,
                    Name = name.Trim(),
                    CurrentBrightness = 50, // Default value, will be updated by MonitorManager after discovery
                    MinBrightness = 0,
                    MaxBrightness = 100,
                    IsAvailable = true,
                    Handle = physicalMonitor.HPhysicalMonitor,
                    Capabilities = MonitorCapabilities.DdcCi,
                    CommunicationMethod = "DDC/CI",
                    MonitorNumber = monitorInfo.MonitorNumber,
                    GdiDeviceName = monitorInfo.GdiDeviceName ?? string.Empty,
                    Orientation = DmdoDefault, // Orientation will be set separately if needed
                };

                // Note: Feature detection (brightness, contrast, color temp, volume) is now done
                // in MonitorManager after capabilities string is retrieved and parsed.
                // This ensures we rely on capabilities data rather than trial-and-error probing.
                return monitor;
            }
            catch (Exception ex)
            {
                Logger.LogError($"DDC: CreateMonitorFromPhysical exception: {ex.Message}");
                return null;
            }
        }
    }
}
