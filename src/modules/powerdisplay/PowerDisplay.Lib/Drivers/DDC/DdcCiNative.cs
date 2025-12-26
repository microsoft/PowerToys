// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Windows.Win32.Foundation;
using static PowerDisplay.Common.Drivers.NativeConstants;
using static PowerDisplay.Common.Drivers.PInvoke;

namespace PowerDisplay.Common.Drivers.DDC
{
    /// <summary>
    /// DDC/CI native API wrapper
    /// </summary>
    public static class DdcCiNative
    {
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
                    return DdcCiValidationResult.Invalid;
                }

                // Parse the capabilities string
                var parseResult = Utils.MccsCapabilitiesParser.Parse(capsString);
                var capabilities = parseResult.Capabilities;
                if (capabilities == null || capabilities.SupportedVcpCodes.Count == 0)
                {
                    return DdcCiValidationResult.Invalid;
                }

                // Check if brightness (VCP 0x10) is supported - determines DDC/CI validity
                bool supportsBrightness = capabilities.SupportsVcpCode(NativeConstants.VcpCodeBrightness);
                return new DdcCiValidationResult(supportsBrightness, capsString, capabilities);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                return DdcCiValidationResult.Invalid;
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
                return null;
            }
        }

        /// <summary>
        /// Gets GDI device name for a source (e.g., "\\.\DISPLAY1").
        /// </summary>
        /// <param name="adapterId">Adapter ID</param>
        /// <param name="sourceId">Source ID</param>
        /// <returns>GDI device name, or null if retrieval fails</returns>
        private static unsafe string? GetSourceGdiDeviceName(LUID adapterId, uint sourceId)
        {
            try
            {
                var sourceName = new DISPLAYCONFIG_SOURCE_DEVICE_NAME
                {
                    Header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                    {
                        Type = DisplayconfigDeviceInfoGetSourceName,
                        Size = (uint)sizeof(DISPLAYCONFIG_SOURCE_DEVICE_NAME),
                        AdapterId = adapterId,
                        Id = sourceId,
                    },
                };

                var result = DisplayConfigGetDeviceInfo(&sourceName);
                if (result == 0)
                {
                    return sourceName.GetViewGdiDeviceName();
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
            }

            return null;
        }

        /// <summary>
        /// Gets friendly name, hardware ID, and device path for a monitor target.
        /// </summary>
        /// <param name="adapterId">Adapter ID</param>
        /// <param name="targetId">Target ID</param>
        /// <returns>Tuple of (friendlyName, hardwareId, devicePath), any may be null if retrieval fails</returns>
        private static unsafe (string? FriendlyName, string? HardwareId, string? DevicePath) GetTargetDeviceInfo(LUID adapterId, uint targetId)
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

                var result = DisplayConfigGetDeviceInfo(&deviceName);
                if (result == 0)
                {
                    // Extract friendly name
                    var friendlyName = deviceName.GetMonitorFriendlyDeviceName();

                    // Extract device path (unique per target, used as key)
                    var devicePath = deviceName.GetMonitorDevicePath();

                    // Extract hardware ID from EDID data
                    var manufacturerId = deviceName.EdidManufactureId;
                    var manufactureCode = ConvertManufactureIdToString(manufacturerId);
                    var productCode = deviceName.EdidProductCodeId.ToString("X4", System.Globalization.CultureInfo.InvariantCulture);
                    var hardwareId = $"{manufactureCode}{productCode}";

                    return (friendlyName, hardwareId, devicePath);
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
            }

            return (null, null, null);
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
        /// Gets complete information for all monitors, keyed by GDI device name (e.g., "\\.\DISPLAY1").
        /// This allows reliable matching with GetMonitorInfo results.
        /// </summary>
        /// <returns>Dictionary keyed by GDI device name containing monitor information</returns>
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

                    // Get GDI device name from source info (e.g., "\\.\DISPLAY1")
                    var gdiDeviceName = GetSourceGdiDeviceName(path.SourceInfo.AdapterId, path.SourceInfo.Id);
                    if (string.IsNullOrEmpty(gdiDeviceName))
                    {
                        continue;
                    }

                    // Get target info (friendly name, hardware ID, device path)
                    var (friendlyName, hardwareId, devicePath) = GetTargetDeviceInfo(path.TargetInfo.AdapterId, path.TargetInfo.Id);

                    // Use device path as key - unique per target, supports mirror mode
                    if (string.IsNullOrEmpty(devicePath))
                    {
                        continue;
                    }

                    monitorInfo[devicePath] = new MonitorDisplayInfo
                    {
                        DevicePath = devicePath,
                        GdiDeviceName = gdiDeviceName,
                        FriendlyName = friendlyName ?? string.Empty,
                        HardwareId = hardwareId ?? string.Empty,
                        AdapterId = path.TargetInfo.AdapterId,
                        TargetId = path.TargetInfo.Id,
                        MonitorNumber = i + 1, // 1-based, matches Windows Display Settings
                    };
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
            }

            return monitorInfo;
        }
    }
}
