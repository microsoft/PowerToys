// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ManagedCommon;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Utils;
using static PowerDisplay.Common.Drivers.NativeConstants;
using static PowerDisplay.Common.Drivers.PInvoke;

using MONITORINFOEX = PowerDisplay.Common.Drivers.MonitorInfoEx;
using PHYSICAL_MONITOR = PowerDisplay.Common.Drivers.PhysicalMonitor;
using RECT = PowerDisplay.Common.Drivers.Rect;

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
        /// Get monitor device ID
        /// </summary>
        public unsafe string GetMonitorDeviceId(IntPtr hMonitor)
        {
            try
            {
                var monitorInfo = new MONITORINFOEX { CbSize = (uint)sizeof(MonitorInfoEx) };
                if (GetMonitorInfo(hMonitor, ref monitorInfo))
                {
                    return monitorInfo.GetDeviceName() ?? string.Empty;
                }
            }
            catch
            {
                // Silent failure
            }

            return string.Empty;
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
        /// Create Monitor object from physical monitor
        /// </summary>
        internal Monitor? CreateMonitorFromPhysical(
            PHYSICAL_MONITOR physicalMonitor,
            string adapterName,
            int index,
            Dictionary<string, MonitorDisplayInfo> monitorDisplayInfo,
            DisplayDeviceInfo? displayDevice)
        {
            try
            {
                // Get hardware ID and friendly name from the display info
                string hardwareId = string.Empty;
                string name = physicalMonitor.GetDescription() ?? string.Empty;

                // Step 1: Extract HardwareId from displayDevice.DeviceID
                // DeviceID format: \\?\DISPLAY#GSM5C6D#5&1234&0&UID#{GUID}
                // We need to extract "GSM5C6D" (the second segment after DISPLAY#)
                string? extractedHardwareId = null;
                if (displayDevice != null && !string.IsNullOrEmpty(displayDevice.DeviceID))
                {
                    extractedHardwareId = ExtractHardwareIdFromDeviceId(displayDevice.DeviceID);
                }

                // Step 2: Find matching MonitorDisplayInfo by HardwareId
                MonitorDisplayInfo? matchedInfo = null;
                if (!string.IsNullOrEmpty(extractedHardwareId))
                {
                    foreach (var kvp in monitorDisplayInfo.Values)
                    {
                        // Match by HardwareId (e.g., "GSM5C6D" matches "GSM5C6D")
                        if (!string.IsNullOrEmpty(kvp.HardwareId) &&
                            kvp.HardwareId.Equals(extractedHardwareId, StringComparison.OrdinalIgnoreCase))
                        {
                            matchedInfo = kvp;
                            break;
                        }
                    }
                }

                // Step 3: Fallback to first match if no direct match found (for backward compatibility)
                if (matchedInfo == null)
                {
                    foreach (var kvp in monitorDisplayInfo.Values)
                    {
                        if (!string.IsNullOrEmpty(kvp.HardwareId))
                        {
                            matchedInfo = kvp;
                            break;
                        }
                    }
                }

                // Step 4: Use matched info
                if (matchedInfo.HasValue)
                {
                    hardwareId = matchedInfo.Value.HardwareId;
                    if (!string.IsNullOrEmpty(matchedInfo.Value.FriendlyName) &&
                        !matchedInfo.Value.FriendlyName.Contains("Generic"))
                    {
                        name = matchedInfo.Value.FriendlyName;
                    }
                }

                // Use stable device IDs from DisplayDeviceInfo
                string deviceKey;
                string monitorId;

                if (displayDevice != null && !string.IsNullOrEmpty(displayDevice.DeviceKey))
                {
                    // Use stable device key from EnumDisplayDevices
                    deviceKey = displayDevice.DeviceKey;
                    monitorId = $"DDC_{deviceKey.Replace(@"\\?\", string.Empty, StringComparison.Ordinal).Replace("#", "_", StringComparison.Ordinal).Replace("&", "_", StringComparison.Ordinal)}";
                }
                else
                {
                    // Fallback: create device ID without handle in the key
                    var baseDevice = adapterName.Replace(@"\\.\", string.Empty, StringComparison.Ordinal);
                    deviceKey = $"{baseDevice}_{index}";
                    monitorId = $"DDC_{deviceKey}";
                }

                // If still no good name, use default value
                // Note: Don't include index in the name - let DisplayName property handle numbering
                if (string.IsNullOrEmpty(name) || name.Contains("Generic") || name.Contains("PnP"))
                {
                    name = "External Display";
                }

                // Get current brightness
                var brightnessInfo = GetCurrentBrightness(physicalMonitor.HPhysicalMonitor);

                var monitor = new Monitor
                {
                    Id = monitorId,
                    HardwareId = hardwareId,
                    Name = name.Trim(),
                    CurrentBrightness = brightnessInfo.IsValid ? brightnessInfo.ToPercentage() : 50,
                    MinBrightness = 0,
                    MaxBrightness = 100,
                    IsAvailable = true,
                    Handle = physicalMonitor.HPhysicalMonitor,
                    DeviceKey = deviceKey,
                    Capabilities = MonitorCapabilities.DdcCi,
                    ConnectionType = "External",
                    CommunicationMethod = "DDC/CI",
                    Manufacturer = ExtractManufacturer(name),
                    CapabilitiesStatus = "unknown",
                    MonitorNumber = GetMonitorNumber(matchedInfo),
                    Orientation = GetMonitorOrientation(adapterName),
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
        /// Extract HardwareId from DeviceID string.
        /// DeviceID format: \\?\DISPLAY#GSM5C6D#5&amp;1234&amp;0&amp;UID#{GUID}
        /// Returns the second segment (e.g., "GSM5C6D") which is the manufacturer+product code.
        /// </summary>
        private static string? ExtractHardwareIdFromDeviceId(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId))
            {
                return null;
            }

            // Find "DISPLAY#" and extract the next segment before the next "#"
            const string displayPrefix = "DISPLAY#";
            int startIndex = deviceId.IndexOf(displayPrefix, StringComparison.OrdinalIgnoreCase);
            if (startIndex < 0)
            {
                return null;
            }

            startIndex += displayPrefix.Length;
            int endIndex = deviceId.IndexOf('#', startIndex);
            if (endIndex < 0)
            {
                return null;
            }

            var hardwareId = deviceId.Substring(startIndex, endIndex - startIndex);
            return string.IsNullOrEmpty(hardwareId) ? null : hardwareId;
        }

        /// <summary>
        /// Get current brightness using VCP code 0x10
        /// </summary>
        private BrightnessInfo GetCurrentBrightness(IntPtr handle)
        {
            if (DdcCiNative.TryGetVCPFeature(handle, VcpCodeBrightness, out uint current, out uint max))
            {
                return new BrightnessInfo((int)current, 0, (int)max);
            }

            return BrightnessInfo.Invalid;
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

        /// <summary>
        /// Get monitor number from MonitorDisplayInfo (QueryDisplayConfig path index).
        /// This matches the number shown in Windows Display Settings "Identify" feature.
        /// </summary>
        private int GetMonitorNumber(MonitorDisplayInfo? matchedInfo)
        {
            if (matchedInfo.HasValue && matchedInfo.Value.MonitorNumber > 0)
            {
                return matchedInfo.Value.MonitorNumber;
            }

            // No match found - return 0 (will not display number suffix)
            return 0;
        }

        /// <summary>
        /// Get monitor orientation using EnumDisplaySettings
        /// </summary>
        private unsafe int GetMonitorOrientation(string adapterName)
        {
            try
            {
                DevMode devMode = default;
                devMode.DmSize = (short)sizeof(DevMode);
                if (EnumDisplaySettings(adapterName, EnumCurrentSettings, &devMode))
                {
                    return devMode.DmDisplayOrientation;
                }
            }
            catch
            {
                // Ignore errors
            }

            return DmdoDefault;
        }
    }
}
