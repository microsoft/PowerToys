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
        /// Get physical monitors for a logical monitor
        /// </summary>
        internal PHYSICAL_MONITOR[]? GetPhysicalMonitors(IntPtr hMonitor)
        {
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

                // Log each physical monitor
                for (int i = 0; i < numMonitors; i++)
                {
                    string desc = physicalMonitors[i].GetDescription() ?? string.Empty;
                    IntPtr handle = physicalMonitors[i].HPhysicalMonitor;
                    Logger.LogDebug($"GetPhysicalMonitors: [{i}] Handle=0x{handle:X}, Desc='{desc}'");

                    if (handle == IntPtr.Zero)
                    {
                        Logger.LogWarning($"GetPhysicalMonitors: Monitor [{i}] has NULL handle despite successful API call!");
                    }
                }

                return physicalMonitors;
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

                // Try to find matching monitor info
                foreach (var kvp in monitorDisplayInfo.Values)
                {
                    if (!string.IsNullOrEmpty(kvp.HardwareId))
                    {
                        hardwareId = kvp.HardwareId;

                        if (!string.IsNullOrEmpty(kvp.FriendlyName) && !kvp.FriendlyName.Contains("Generic"))
                        {
                            name = kvp.FriendlyName;
                        }

                        break;
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
                if (string.IsNullOrEmpty(name) || name.Contains("Generic") || name.Contains("PnP"))
                {
                    name = $"External Display {index + 1}";
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
                    MonitorNumber = ParseMonitorNumber(adapterName),
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
        /// Get current brightness using VCP code 0x10 only
        /// </summary>
        private BrightnessInfo GetCurrentBrightness(IntPtr handle)
        {
            // Try high-level API first
            if (DdcCiNative.TryGetMonitorBrightness(handle, out uint min, out uint current, out uint max))
            {
                return new BrightnessInfo((int)current, (int)min, (int)max);
            }

            // Try VCP code 0x10 (standard brightness)
            if (DdcCiNative.TryGetVCPFeature(handle, VcpCodeBrightness, out current, out max))
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
        /// Parse monitor number from device name (e.g., "\\.\DISPLAY1" -> 1)
        /// </summary>
        private int ParseMonitorNumber(string adapterName)
        {
            try
            {
                // Format is usually \\.\DISPLAYx
                if (string.IsNullOrEmpty(adapterName))
                {
                    return 0;
                }

                // Remove prefix
                var name = adapterName.Replace(@"\\.\", string.Empty, StringComparison.Ordinal);

                // Find "DISPLAY"
                int index = name.IndexOf("DISPLAY", StringComparison.OrdinalIgnoreCase);
                if (index >= 0)
                {
                    string numberPart = name.Substring(index + 7);

                    // Take only digits
                    string numberStr = new string(numberPart.TakeWhile(char.IsDigit).ToArray());

                    if (int.TryParse(numberStr, out int number))
                    {
                        return number;
                    }
                }
            }
            catch
            {
                // Ignore parsing errors
            }

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
