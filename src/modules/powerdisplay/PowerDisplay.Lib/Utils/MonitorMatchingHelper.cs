// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using ManagedCommon;
using PowerDisplay.Common.Drivers.DDC;
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
        /// Get monitor number from WMI InstanceName by matching with EnumDisplayDevices.
        /// Returns 0 if matching fails.
        /// </summary>
        /// <param name="instanceName">WMI InstanceName (e.g., "DISPLAY\BOE0900\4&amp;10fd3ab1&amp;0&amp;UID265988_0")</param>
        /// <returns>Monitor number (1, 2, 3...) or 0 if not found</returns>
        public static int GetMonitorNumberFromWmiInstanceName(string instanceName)
        {
            // Fetch display devices and delegate to the overload
            var displayDevices = DdcCiNative.GetAllDisplayDevices();
            return GetMonitorNumberFromWmiInstanceName(instanceName, displayDevices);
        }

        /// <summary>
        /// Get monitor number from WMI InstanceName using pre-fetched display devices.
        /// Use this overload in loops to avoid repeated Win32 API calls.
        /// Returns 0 if matching fails.
        /// </summary>
        /// <param name="instanceName">WMI InstanceName (e.g., "DISPLAY\BOE0900\4&amp;10fd3ab1&amp;0&amp;UID265988_0")</param>
        /// <param name="displayDevices">Pre-fetched list of display devices from DdcCiNative.GetAllDisplayDevices()</param>
        /// <returns>Monitor number (1, 2, 3...) or 0 if not found</returns>
        public static int GetMonitorNumberFromWmiInstanceName(string instanceName, IReadOnlyList<DisplayDeviceInfo> displayDevices)
        {
            try
            {
                // Extract the device instance path for matching
                string? devicePath = ExtractDeviceInstancePath(instanceName);
                if (string.IsNullOrEmpty(devicePath))
                {
                    Logger.LogWarning($"GetMonitorNumberFromWmiInstanceName: Failed to extract device path from '{instanceName}'");
                    return 0;
                }

                if (displayDevices.Count == 0)
                {
                    Logger.LogWarning("GetMonitorNumberFromWmiInstanceName: No display devices found from EnumDisplayDevices");
                    return 0;
                }

                // Log all available devices for debugging
                Logger.LogDebug($"GetMonitorNumberFromWmiInstanceName: Searching for devicePath='{devicePath}' in {displayDevices.Count} devices");
                foreach (var d in displayDevices)
                {
                    Logger.LogDebug($"  - Adapter: {d.AdapterName}, DeviceKey: {d.DeviceKey}");
                }

                // Find matching device by checking if DeviceKey contains our device path
                foreach (var device in displayDevices)
                {
                    if (!string.IsNullOrEmpty(device.DeviceKey) &&
                        device.DeviceKey.Contains(devicePath, StringComparison.OrdinalIgnoreCase))
                    {
                        // Found match! Parse monitor number from AdapterName (e.g., "\\.\DISPLAY1" -> 1)
                        int monitorNumber = ParseDisplayNumber(device.AdapterName);
                        Logger.LogDebug($"GetMonitorNumberFromWmiInstanceName: Matched '{instanceName}' to DISPLAY{monitorNumber}");
                        return monitorNumber;
                    }
                }

                // No match found
                Logger.LogWarning($"GetMonitorNumberFromWmiInstanceName: No matching display device found for path '{devicePath}'");
            }
            catch (Exception ex)
            {
                Logger.LogError($"GetMonitorNumberFromWmiInstanceName: Exception while parsing '{instanceName}': {ex.Message}");
            }

            return 0;
        }

        /// <summary>
        /// Extract the device instance path from WMI InstanceName for matching.
        /// WMI InstanceName format: "DISPLAY\BOE0900\4&amp;10fd3ab1&amp;0&amp;UID265988_0"
        /// Returns: "4&amp;10fd3ab1&amp;0&amp;UID265988" (the unique device path portion)
        /// </summary>
        /// <param name="instanceName">WMI InstanceName</param>
        /// <returns>Device instance path for matching, or null if extraction fails</returns>
        public static string? ExtractDeviceInstancePath(string instanceName)
        {
            if (string.IsNullOrEmpty(instanceName))
            {
                Logger.LogDebug("ExtractDeviceInstancePath: instanceName is null or empty");
                return null;
            }

            // Find the last backslash to get the instance path portion
            // e.g., "DISPLAY\BOE0900\4&10fd3ab1&0&UID265988_0" -> "4&10fd3ab1&0&UID265988_0"
            int lastBackslash = instanceName.LastIndexOf('\\');
            if (lastBackslash < 0 || lastBackslash >= instanceName.Length - 1)
            {
                Logger.LogWarning($"ExtractDeviceInstancePath: Invalid format, no valid backslash found in '{instanceName}'");
                return null;
            }

            string instancePath = instanceName.Substring(lastBackslash + 1);

            // Remove trailing WMI instance index suffix (e.g., _0, _1, _12)
            // WMI appends "_N" where N is the instance index to device instance paths
            // See: https://learn.microsoft.com/en-us/windows/win32/wmicoreprov/wmimonitorid
            int lastUnderscore = instancePath.LastIndexOf('_');
            if (lastUnderscore > 0)
            {
                string suffix = instancePath[(lastUnderscore + 1)..];
                if (suffix.Length > 0 && suffix.All(char.IsDigit))
                {
                    instancePath = instancePath[..lastUnderscore];
                }
            }

            if (string.IsNullOrEmpty(instancePath))
            {
                Logger.LogWarning($"ExtractDeviceInstancePath: Extracted path is empty from '{instanceName}'");
                return null;
            }

            return instancePath;
        }

        /// <summary>
        /// Parse display number from adapter name (e.g., "\\.\DISPLAY1" -> 1)
        /// </summary>
        /// <param name="adapterName">Adapter name from EnumDisplayDevices</param>
        /// <returns>Display number or 0 if parsing fails</returns>
        public static int ParseDisplayNumber(string adapterName)
        {
            if (string.IsNullOrEmpty(adapterName))
            {
                return 0;
            }

            // Find "DISPLAY" and extract the number after it
            int index = adapterName.IndexOf("DISPLAY", StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                string numberPart = adapterName.Substring(index + 7);
                string numberStr = new string(numberPart.TakeWhile(char.IsDigit).ToArray());

                if (int.TryParse(numberStr, out int number))
                {
                    return number;
                }
            }

            return 0;
        }

        /// <summary>
        /// Generate a unique key for monitor matching based on hardware ID and internal name.
        /// Uses HardwareId if available; otherwise falls back to Id (InternalName) or Name.
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
