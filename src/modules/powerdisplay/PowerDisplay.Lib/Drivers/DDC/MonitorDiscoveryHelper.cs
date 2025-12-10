// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using ManagedCommon;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Utils;
using static PowerDisplay.Common.Drivers.NativeConstants;
using static PowerDisplay.Common.Drivers.PInvoke;

using MONITORINFOEX = PowerDisplay.Common.Drivers.MonitorInfoEx;
using PHYSICAL_MONITOR = PowerDisplay.Common.Drivers.PhysicalMonitor;

namespace PowerDisplay.Common.Drivers.DDC
{
    /// <summary>
    /// Helper class for discovering and creating monitor objects
    /// </summary>
    public class MonitorDiscoveryHelper
    {
        public MonitorDiscoveryHelper()
        {
        }

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
                Logger.LogDebug($"GetPhysicalMonitors: hMonitor=0x{hMonitor:X}");

                if (!GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out uint numMonitors))
                {
                    Logger.LogWarning($"GetPhysicalMonitors: GetNumberOfPhysicalMonitorsFromHMONITOR failed for 0x{hMonitor:X}");
                    return null;
                }

                Logger.LogDebug($"GetPhysicalMonitors: numMonitors={numMonitors}");
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

                Logger.LogDebug($"GetPhysicalMonitors: GetPhysicalMonitorsFromHMONITOR returned {apiResult}");

                if (!apiResult)
                {
                    Logger.LogWarning($"GetPhysicalMonitors: GetPhysicalMonitorsFromHMONITOR failed");
                    return null;
                }

                // Filter out NULL handles and log each physical monitor
                var validMonitors = new List<PHYSICAL_MONITOR>();
                for (int i = 0; i < numMonitors; i++)
                {
                    string desc = physicalMonitors[i].GetDescription() ?? string.Empty;
                    IntPtr handle = physicalMonitors[i].HPhysicalMonitor;

                    if (handle == IntPtr.Zero)
                    {
                        Logger.LogWarning($"GetPhysicalMonitors: Monitor [{i}] has NULL handle, filtering out");
                        hasNullHandles = true;
                        continue;
                    }

                    Logger.LogDebug($"GetPhysicalMonitors: [{i}] Handle=0x{handle:X}, Desc='{desc}'");
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
        /// </summary>
        /// <param name="physicalMonitor">Physical monitor structure with handle and description</param>
        /// <param name="monitorInfo">Display info from QueryDisplayConfig (HardwareId, FriendlyName, MonitorNumber)</param>
        internal Monitor? CreateMonitorFromPhysical(
            PHYSICAL_MONITOR physicalMonitor,
            MonitorDisplayInfo monitorInfo)
        {
            try
            {
                // Get hardware ID and friendly name directly from MonitorDisplayInfo
                string edidId = monitorInfo.HardwareId ?? string.Empty;
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

                // Get current brightness
                var brightnessInfo = GetCurrentBrightness(physicalMonitor.HPhysicalMonitor);

                var monitor = new Monitor
                {
                    Id = monitorId,
                    Name = name.Trim(),
                    CurrentBrightness = brightnessInfo.IsValid ? brightnessInfo.ToPercentage() : 50,
                    MinBrightness = 0,
                    MaxBrightness = 100,
                    IsAvailable = true,
                    Handle = physicalMonitor.HPhysicalMonitor,
                    Capabilities = MonitorCapabilities.DdcCi,
                    ConnectionType = "External",
                    CommunicationMethod = "DDC/CI",
                    Manufacturer = ExtractManufacturer(name),
                    CapabilitiesStatus = "unknown",
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

        /// <summary>
        /// Get current brightness using VCP code 0x10
        /// </summary>
        private VcpFeatureValue GetCurrentBrightness(IntPtr handle)
        {
            if (GetVCPFeatureAndVCPFeatureReply(handle, VcpCodeBrightness, IntPtr.Zero, out uint current, out uint max))
            {
                return new VcpFeatureValue((int)current, 0, (int)max);
            }

            return VcpFeatureValue.Invalid;
        }

        /// <summary>
        /// Extract manufacturer from name
        /// </summary>
        private string ExtractManufacturer(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "Unknown";
            }

            // Common manufacturer prefixes
            var manufacturers = new[] { "DELL", "HP", "LG", "Samsung", "ASUS", "Acer", "BenQ", "AOC", "ViewSonic" };
            var upperName = name.ToUpperInvariant();

            foreach (var manufacturer in manufacturers)
            {
                if (upperName.Contains(manufacturer))
                {
                    return manufacturer;
                }
            }

            // Return first word as manufacturer
            var firstWord = name.Split(' ')[0];
            return firstWord.Length > 2 ? firstWord : "Unknown";
        }
    }
}
