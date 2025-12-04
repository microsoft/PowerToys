// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ManagedCommon;
using static PowerDisplay.Common.Drivers.NativeConstants;
using static PowerDisplay.Common.Drivers.NativeDelegates;
using static PowerDisplay.Common.Drivers.PInvoke;

// Type aliases for Windows API naming conventions compatibility
using DISPLAY_DEVICE = PowerDisplay.Common.Drivers.DisplayDevice;
using DISPLAYCONFIG_DEVICE_INFO_HEADER = PowerDisplay.Common.Drivers.DISPLAYCONFIG_DEVICE_INFO_HEADER;
using DISPLAYCONFIG_MODE_INFO = PowerDisplay.Common.Drivers.DISPLAYCONFIG_MODE_INFO;
using DISPLAYCONFIG_PATH_INFO = PowerDisplay.Common.Drivers.DISPLAYCONFIG_PATH_INFO;
using DISPLAYCONFIG_TARGET_DEVICE_NAME = PowerDisplay.Common.Drivers.DISPLAYCONFIG_TARGET_DEVICE_NAME;
using LUID = PowerDisplay.Common.Drivers.Luid;
using MONITORINFOEX = PowerDisplay.Common.Drivers.MonitorInfoEx;
using PHYSICAL_MONITOR = PowerDisplay.Common.Drivers.PhysicalMonitor;
using RECT = PowerDisplay.Common.Drivers.Rect;

#pragma warning disable SA1649 // File name should match first type name - Multiple related types for DDC/CI
#pragma warning disable SA1402 // File may only contain a single type - Related DDC/CI types grouped together

namespace PowerDisplay.Common.Drivers.DDC
{
    /// <summary>
    /// Display device information class
    /// </summary>
    public class DisplayDeviceInfo
    {
        public string DeviceName { get; set; } = string.Empty;

        public string AdapterName { get; set; } = string.Empty;

        public string DeviceID { get; set; } = string.Empty;

        public string DeviceKey { get; set; } = string.Empty;
    }

    /// <summary>
    /// DDC/CI validation result containing both validation status and cached capabilities data.
    /// This allows reusing capabilities data retrieved during validation, avoiding duplicate I2C calls.
    /// </summary>
    public struct DdcCiValidationResult
    {
        /// <summary>
        /// Gets a value indicating whether the monitor has a valid DDC/CI connection with brightness support.
        /// </summary>
        public bool IsValid { get; }

        /// <summary>
        /// Gets the raw capabilities string retrieved during validation.
        /// Null if retrieval failed.
        /// </summary>
        public string? CapabilitiesString { get; }

        /// <summary>
        /// Gets the parsed VCP capabilities info retrieved during validation.
        /// Null if parsing failed.
        /// </summary>
        public Models.VcpCapabilities? VcpCapabilitiesInfo { get; }

        /// <summary>
        /// Gets a value indicating whether capabilities retrieval was attempted.
        /// True means the result is from an actual attempt (success or failure).
        /// </summary>
        public bool WasAttempted { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DdcCiValidationResult"/> struct.
        /// </summary>
        public DdcCiValidationResult(bool isValid, string? capabilitiesString = null, Models.VcpCapabilities? vcpCapabilitiesInfo = null, bool wasAttempted = true)
        {
            IsValid = isValid;
            CapabilitiesString = capabilitiesString;
            VcpCapabilitiesInfo = vcpCapabilitiesInfo;
            WasAttempted = wasAttempted;
        }

        /// <summary>
        /// Gets an invalid validation result with no cached data.
        /// </summary>
        public static DdcCiValidationResult Invalid => new(false, null, null, true);

        /// <summary>
        /// Gets a result indicating validation was not attempted yet.
        /// </summary>
        public static DdcCiValidationResult NotAttempted => new(false, null, null, false);
    }

    /// <summary>
    /// DDC/CI native API wrapper
    /// </summary>
    public static class DdcCiNative
    {
        // Display Configuration constants
        public const uint QdcAllPaths = 0x00000001;

        public const uint QdcOnlyActivePaths = 0x00000002;

        public const uint DisplayconfigDeviceInfoGetTargetName = 2;

        // Helper Methods

        /// <summary>
        /// Safe wrapper for getting VCP feature value
        /// </summary>
        /// <param name="hPhysicalMonitor">Physical monitor handle</param>
        /// <param name="vcpCode">VCP code</param>
        /// <param name="currentValue">Current value</param>
        /// <param name="maxValue">Maximum value</param>
        /// <returns>True if successful</returns>
        public static bool TryGetVCPFeature(IntPtr hPhysicalMonitor, byte vcpCode, out uint currentValue, out uint maxValue)
        {
            currentValue = 0;
            maxValue = 0;

            if (hPhysicalMonitor == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                return GetVCPFeatureAndVCPFeatureReply(hPhysicalMonitor, vcpCode, IntPtr.Zero, out currentValue, out maxValue);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Logger.LogDebug($"TryGetVCPFeature failed for VCP code 0x{vcpCode:X2}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Safe wrapper for setting VCP feature value
        /// </summary>
        /// <param name="hPhysicalMonitor">Physical monitor handle</param>
        /// <param name="vcpCode">VCP code</param>
        /// <param name="value">New value</param>
        /// <returns>True if successful</returns>
        public static bool TrySetVCPFeature(IntPtr hPhysicalMonitor, byte vcpCode, uint value)
        {
            if (hPhysicalMonitor == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                return SetVCPFeature(hPhysicalMonitor, vcpCode, value);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Logger.LogDebug($"TrySetVCPFeature failed for VCP code 0x{vcpCode:X2}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Fetches VCP capabilities string from a monitor and returns a validation result.
        /// This is the slow I2C operation (~4 seconds per monitor) that should only be done once.
        /// The result is cached regardless of success or failure.
        /// </summary>
        /// <param name="hPhysicalMonitor">Physical monitor handle</param>
        /// <returns>Validation result with capabilities data (or failure status)</returns>
        public static DdcCiValidationResult FetchCapabilities(IntPtr hPhysicalMonitor)
        {
            if (hPhysicalMonitor == IntPtr.Zero)
            {
                return DdcCiValidationResult.Invalid;
            }

            try
            {
                // Try to get capabilities string (slow I2C operation)
                var capsString = TryGetCapabilitiesString(hPhysicalMonitor);
                if (string.IsNullOrEmpty(capsString))
                {
                    Logger.LogDebug($"FetchCapabilities: Failed to get capabilities string for handle 0x{hPhysicalMonitor:X}");
                    return DdcCiValidationResult.Invalid;
                }

                // Parse the capabilities string
                var parseResult = Utils.MccsCapabilitiesParser.Parse(capsString);
                var capabilities = parseResult.Capabilities;
                if (capabilities == null || capabilities.SupportedVcpCodes.Count == 0)
                {
                    Logger.LogDebug($"FetchCapabilities: Failed to parse capabilities string for handle 0x{hPhysicalMonitor:X}");
                    return DdcCiValidationResult.Invalid;
                }

                // Check if brightness (VCP 0x10) is supported - determines DDC/CI validity
                bool supportsBrightness = capabilities.SupportsVcpCode(NativeConstants.VcpCodeBrightness);

                Logger.LogDebug($"FetchCapabilities: Handle 0x{hPhysicalMonitor:X} - BrightnessSupport={supportsBrightness}, VcpCodes={capabilities.SupportedVcpCodes.Count}");
                return new DdcCiValidationResult(supportsBrightness, capsString, capabilities);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Logger.LogDebug($"FetchCapabilities: Exception for handle 0x{hPhysicalMonitor:X}: {ex.Message}");
                return DdcCiValidationResult.Invalid;
            }
        }

        /// <summary>
        /// Validates the DDC/CI connection by checking if the monitor returns a valid capabilities string
        /// that includes brightness control (VCP 0x10).
        /// NOTE: This method performs a slow I2C operation. Prefer using FetchCapabilities() during
        /// discovery phase and caching the result.
        /// </summary>
        /// <param name="hPhysicalMonitor">Physical monitor handle</param>
        /// <returns>Validation result containing status and cached capabilities data</returns>
        [System.Obsolete("Use FetchCapabilities() during discovery and cache results. This method is kept for backward compatibility.")]
        public static DdcCiValidationResult ValidateDdcCiConnection(IntPtr hPhysicalMonitor)
        {
            // Delegate to FetchCapabilities which does the same thing
            return FetchCapabilities(hPhysicalMonitor);
        }

        /// <summary>
        /// Quick connection check using a simple VCP read (brightness).
        /// This is much faster than full capabilities retrieval (~50ms vs ~4s).
        /// Use this for runtime connection validation when capabilities are already cached.
        /// </summary>
        /// <param name="hPhysicalMonitor">Physical monitor handle</param>
        /// <returns>True if the monitor responds to VCP queries</returns>
        public static bool QuickConnectionCheck(IntPtr hPhysicalMonitor)
        {
            if (hPhysicalMonitor == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                // Try a quick brightness read via VCP 0x10 to verify connection
                return TryGetVCPFeature(hPhysicalMonitor, NativeConstants.VcpCodeBrightness, out _, out _);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Try to get capabilities string from a physical monitor handle.
        /// </summary>
        /// <param name="hPhysicalMonitor">Physical monitor handle</param>
        /// <returns>Capabilities string, or null if failed</returns>
        private static string? TryGetCapabilitiesString(IntPtr hPhysicalMonitor)
        {
            if (hPhysicalMonitor == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                // Get capabilities string length
                if (!GetCapabilitiesStringLength(hPhysicalMonitor, out uint length) || length == 0)
                {
                    return null;
                }

                // Allocate buffer and get capabilities string
                var buffer = Marshal.AllocHGlobal((int)length);
                try
                {
                    if (!CapabilitiesRequestAndCapabilitiesReply(hPhysicalMonitor, buffer, length))
                    {
                        return null;
                    }

                    return Marshal.PtrToStringAnsi(buffer);
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Logger.LogDebug($"TryGetCapabilitiesString failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets the monitor friendly name
        /// </summary>
        /// <param name="adapterId">Adapter ID</param>
        /// <param name="targetId">Target ID</param>
        /// <returns>Monitor friendly name, or null if retrieval fails</returns>
        public static unsafe string? GetMonitorFriendlyName(LUID adapterId, uint targetId)
        {
            try
            {
                var deviceName = new DISPLAYCONFIG_TARGET_DEVICE_NAME
                {
                    Header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                    {
                        Type = DisplayconfigDeviceInfoGetTargetName,
                        Size = (uint)sizeof(DISPLAYCONFIG_TARGET_DEVICE_NAME),
                        AdapterId = adapterId,
                        Id = targetId,
                    },
                };

                var result = DisplayConfigGetDeviceInfo(ref deviceName);
                if (result == 0)
                {
                    return deviceName.GetMonitorFriendlyDeviceName();
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Logger.LogDebug($"GetMonitorFriendlyName failed: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Gets all monitor friendly names by enumerating display configurations
        /// </summary>
        /// <returns>Mapping of device path to friendly name</returns>
        public static unsafe Dictionary<string, string> GetAllMonitorFriendlyNames()
        {
            var friendlyNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                // Get buffer sizes
                var result = GetDisplayConfigBufferSizes(QdcOnlyActivePaths, out uint pathCount, out uint modeCount);
                if (result != 0)
                {
                    return friendlyNames;
                }

                // Allocate buffers
                var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
                var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];

                // Query display configuration using fixed pointer
                fixed (DISPLAYCONFIG_PATH_INFO* pathsPtr = paths)
                {
                    fixed (DISPLAYCONFIG_MODE_INFO* modesPtr = modes)
                    {
                        result = QueryDisplayConfig(QdcOnlyActivePaths, ref pathCount, pathsPtr, ref modeCount, modesPtr, IntPtr.Zero);
                        if (result != 0)
                        {
                            return friendlyNames;
                        }
                    }
                }

                // Get friendly name for each path
                for (int i = 0; i < pathCount; i++)
                {
                    var path = paths[i];
                    var friendlyName = GetMonitorFriendlyName(path.TargetInfo.AdapterId, path.TargetInfo.Id);

                    if (!string.IsNullOrEmpty(friendlyName))
                    {
                        // Use adapter and target ID as key
                        var key = $"{path.TargetInfo.AdapterId}_{path.TargetInfo.Id}";
                        friendlyNames[key] = friendlyName;
                    }
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Logger.LogDebug($"GetAllMonitorFriendlyNames failed: {ex.Message}");
            }

            return friendlyNames;
        }

        /// <summary>
        /// Gets the EDID hardware ID information for a monitor
        /// </summary>
        /// <param name="adapterId">Adapter ID</param>
        /// <param name="targetId">Target ID</param>
        /// <returns>Hardware ID string in format: manufacturer code + product code</returns>
        public static unsafe string? GetMonitorHardwareId(LUID adapterId, uint targetId)
        {
            try
            {
                var deviceName = new DISPLAYCONFIG_TARGET_DEVICE_NAME
                {
                    Header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                    {
                        Type = DisplayconfigDeviceInfoGetTargetName,
                        Size = (uint)sizeof(DISPLAYCONFIG_TARGET_DEVICE_NAME),
                        AdapterId = adapterId,
                        Id = targetId,
                    },
                };

                var result = DisplayConfigGetDeviceInfo(ref deviceName);
                if (result == 0)
                {
                    // Convert manufacturer ID to 3-character string
                    var manufacturerId = deviceName.EdidManufactureId;
                    var manufactureCode = ConvertManufactureIdToString(manufacturerId);

                    // Convert product ID to 4-digit hex string
                    var productCode = deviceName.EdidProductCodeId.ToString("X4", System.Globalization.CultureInfo.InvariantCulture);

                    var hardwareId = $"{manufactureCode}{productCode}";
                    Logger.LogDebug($"GetMonitorHardwareId - ManufacturerId: 0x{manufacturerId:X4}, Code: '{manufactureCode}', ProductCode: '{productCode}', Result: '{hardwareId}'");

                    return hardwareId;
                }
                else
                {
                    Logger.LogError($"GetMonitorHardwareId - DisplayConfigGetDeviceInfo failed with result: {result}");
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Logger.LogDebug($"GetMonitorHardwareId failed: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Converts manufacturer ID to 3-character manufacturer code
        /// </summary>
        /// <param name="manufacturerId">Manufacturer ID</param>
        /// <returns>3-character manufacturer code</returns>
        private static string ConvertManufactureIdToString(ushort manufacturerId)
        {
            // EDID manufacturer ID requires byte order swap first
            manufacturerId = (ushort)(((manufacturerId & 0xff00) >> 8) | ((manufacturerId & 0x00ff) << 8));

            // Extract 3 5-bit characters (each character is A-Z, where A=1, B=2, ..., Z=26)
            var char1 = (char)('A' - 1 + ((manufacturerId >> 0) & 0x1f));
            var char2 = (char)('A' - 1 + ((manufacturerId >> 5) & 0x1f));
            var char3 = (char)('A' - 1 + ((manufacturerId >> 10) & 0x1f));

            // Combine characters in correct order
            return $"{char3}{char2}{char1}";
        }

        /// <summary>
        /// Gets complete information for all monitors, including friendly name and hardware ID
        /// </summary>
        /// <returns>Dictionary containing monitor information</returns>
        public static unsafe Dictionary<string, MonitorDisplayInfo> GetAllMonitorDisplayInfo()
        {
            var monitorInfo = new Dictionary<string, MonitorDisplayInfo>(StringComparer.OrdinalIgnoreCase);

            try
            {
                // Get buffer sizes
                var result = GetDisplayConfigBufferSizes(QdcOnlyActivePaths, out uint pathCount, out uint modeCount);
                if (result != 0)
                {
                    return monitorInfo;
                }

                // Allocate buffers
                var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
                var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];

                // Query display configuration using fixed pointer
                fixed (DISPLAYCONFIG_PATH_INFO* pathsPtr = paths)
                {
                    fixed (DISPLAYCONFIG_MODE_INFO* modesPtr = modes)
                    {
                        result = QueryDisplayConfig(QdcOnlyActivePaths, ref pathCount, pathsPtr, ref modeCount, modesPtr, IntPtr.Zero);
                        if (result != 0)
                        {
                            return monitorInfo;
                        }
                    }
                }

                // Get information for each path
                // The path index corresponds to Windows Display Settings "Identify" number
                for (int i = 0; i < pathCount; i++)
                {
                    var path = paths[i];
                    var friendlyName = GetMonitorFriendlyName(path.TargetInfo.AdapterId, path.TargetInfo.Id);
                    var hardwareId = GetMonitorHardwareId(path.TargetInfo.AdapterId, path.TargetInfo.Id);

                    if (!string.IsNullOrEmpty(friendlyName) || !string.IsNullOrEmpty(hardwareId))
                    {
                        var key = $"{path.TargetInfo.AdapterId}_{path.TargetInfo.Id}";
                        monitorInfo[key] = new MonitorDisplayInfo
                        {
                            FriendlyName = friendlyName ?? string.Empty,
                            HardwareId = hardwareId ?? string.Empty,
                            AdapterId = path.TargetInfo.AdapterId,
                            TargetId = path.TargetInfo.Id,
                            MonitorNumber = i + 1, // 1-based, matches Windows Display Settings
                        };

                        Logger.LogDebug($"QueryDisplayConfig path[{i}]: HardwareId={hardwareId}, FriendlyName={friendlyName}, MonitorNumber={i + 1}");
                    }
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Logger.LogDebug($"GetAllMonitorDisplayInfo failed: {ex.Message}");
            }

            return monitorInfo;
        }

        /// <summary>
        /// Get all display device information using EnumDisplayDevices API
        /// </summary>
        /// <returns>List of display device information</returns>
        public static unsafe List<DisplayDeviceInfo> GetAllDisplayDevices()
        {
            var devices = new List<DisplayDeviceInfo>();

            try
            {
                // Enumerate all adapters
                uint adapterIndex = 0;
                var adapter = default(DISPLAY_DEVICE);
                adapter.Cb = (uint)sizeof(DisplayDevice);

                while (EnumDisplayDevices(null, adapterIndex, ref adapter, EddGetDeviceInterfaceName))
                {
                    // Skip mirroring drivers
                    if ((adapter.StateFlags & DisplayDeviceMirroringDriver) != 0)
                    {
                        adapterIndex++;
                        adapter = default(DISPLAY_DEVICE);
                        adapter.Cb = (uint)sizeof(DisplayDevice);
                        continue;
                    }

                    // Only process adapters attached to desktop
                    if ((adapter.StateFlags & DisplayDeviceAttachedToDesktop) != 0)
                    {
                        // Enumerate all monitors on this adapter
                        uint displayIndex = 0;
                        var display = default(DISPLAY_DEVICE);
                        display.Cb = (uint)sizeof(DisplayDevice);

                        string adapterDeviceName = adapter.GetDeviceName();
                        while (EnumDisplayDevices(adapterDeviceName, displayIndex, ref display, EddGetDeviceInterfaceName))
                        {
                            string displayDeviceID = display.GetDeviceID();

                            // Only process active monitors
                            if ((display.StateFlags & DisplayDeviceAttachedToDesktop) != 0 &&
                                !string.IsNullOrEmpty(displayDeviceID))
                            {
                                var deviceInfo = new DisplayDeviceInfo
                                {
                                    DeviceName = display.GetDeviceName(),
                                    AdapterName = adapterDeviceName,
                                    DeviceID = displayDeviceID,
                                };

                                // Extract DeviceKey: remove GUID part (#{...} and everything after)
                                // Example: \\?\DISPLAY#GSM5C6D#5&1234&0&UID#{GUID} -> \\?\DISPLAY#GSM5C6D#5&1234&0&UID
                                int guidIndex = deviceInfo.DeviceID.IndexOf("#{", StringComparison.Ordinal);
                                if (guidIndex >= 0)
                                {
                                    deviceInfo.DeviceKey = deviceInfo.DeviceID.Substring(0, guidIndex);
                                }
                                else
                                {
                                    deviceInfo.DeviceKey = deviceInfo.DeviceID;
                                }

                                devices.Add(deviceInfo);

                                Logger.LogDebug($"Found display device - Name: {deviceInfo.DeviceName}, Adapter: {deviceInfo.AdapterName}, DeviceKey: {deviceInfo.DeviceKey}");
                            }

                            displayIndex++;
                            display = default(DISPLAY_DEVICE);
                            display.Cb = (uint)sizeof(DisplayDevice);
                        }
                    }

                    adapterIndex++;
                    adapter = default(DISPLAY_DEVICE);
                    adapter.Cb = (uint)sizeof(DisplayDevice);
                }

                Logger.LogInfo($"GetAllDisplayDevices found {devices.Count} display devices");
            }
            catch (Exception ex)
            {
                Logger.LogError($"GetAllDisplayDevices exception: {ex.Message}");
            }

            return devices;
        }
    }

    /// <summary>
    /// Monitor display information structure
    /// </summary>
    public struct MonitorDisplayInfo
    {
        public string FriendlyName { get; set; }

        public string HardwareId { get; set; }

        public LUID AdapterId { get; set; }

        public uint TargetId { get; set; }

        /// <summary>
        /// Gets or sets the monitor number based on QueryDisplayConfig path index.
        /// This matches the number shown in Windows Display Settings "Identify" feature.
        /// 1-based index (paths[0] = 1, paths[1] = 2, etc.)
        /// </summary>
        public int MonitorNumber { get; set; }
    }
}
