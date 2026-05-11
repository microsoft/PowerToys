// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ManagedCommon;
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
                Logger.LogWarning("DDC: Monitor ignored - null physical monitor handle");
                return DdcCiValidationResult.Invalid;
            }

            var handleHex = $"0x{hPhysicalMonitor:X}";

            try
            {
                // Try to get capabilities string (slow I2C operation)
                var capsString = TryGetCapabilitiesString(hPhysicalMonitor);
                if (string.IsNullOrEmpty(capsString))
                {
                    Logger.LogWarning($"DDC: Monitor ignored (handle={handleHex}) - empty capabilities string from DDC/CI");
                    return DdcCiValidationResult.Invalid;
                }

                Logger.LogInfo($"DDC: Capabilities raw (handle={handleHex}, length={capsString.Length}): {capsString}");

                // Parse the capabilities string
                var parseResult = Utils.MccsCapabilitiesParser.Parse(capsString);
                var capabilities = parseResult.Capabilities;

                if (capabilities == null || capabilities.SupportedVcpCodes.Count == 0)
                {
                    Logger.LogWarning($"DDC: Monitor ignored (handle={handleHex}) - parsed capabilities have no VCP codes (parseErrors={parseResult.Errors.Count})");
                    return DdcCiValidationResult.Invalid;
                }

                // Check if brightness (VCP 0x10) is supported - determines DDC/CI validity
                bool supportsBrightness = capabilities.SupportsVcpCode(NativeConstants.VcpCodeBrightness);
                bool supportsContrast = capabilities.SupportsVcpCode(NativeConstants.VcpCodeContrast);
                bool supportsColorTemperature = capabilities.SupportsVcpCode(NativeConstants.VcpCodeSelectColorPreset);
                bool supportsVolume = capabilities.SupportsVcpCode(NativeConstants.VcpCodeVolume);

                Logger.LogInfo(
                    $"DDC: Capabilities parsed (handle={handleHex}) - " +
                    $"Brightness={supportsBrightness} Contrast={supportsContrast} " +
                    $"ColorTemperature={supportsColorTemperature} Volume={supportsVolume}");

                if (!supportsBrightness)
                {
                    Logger.LogWarning($"DDC: Monitor ignored (handle={handleHex}) - brightness (VCP 0x10) not advertised in capabilities");
                }

                return new DdcCiValidationResult(supportsBrightness, capsString, capabilities);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Logger.LogError($"DDC: Monitor ignored (handle={handleHex}) - exception during FetchCapabilities: {ex.Message}");
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
                    Logger.LogWarning($"DDC: GetCapabilitiesStringLength failed (handle=0x{hPhysicalMonitor:X}, length={length})");
                    return null;
                }

                // Allocate buffer and get capabilities string
                var buffer = Marshal.AllocHGlobal((int)length);
                try
                {
                    if (!CapabilitiesRequestAndCapabilitiesReply(hPhysicalMonitor, buffer, length))
                    {
                        Logger.LogWarning($"DDC: CapabilitiesRequestAndCapabilitiesReply failed (handle=0x{hPhysicalMonitor:X})");
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
                Logger.LogError($"DDC: TryGetCapabilitiesString exception (handle=0x{hPhysicalMonitor:X}): {ex.Message}");
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
                var sourceName = new DisplayConfigSourceDeviceName
                {
                    Header = new DisplayConfigDeviceInfoHeader
                    {
                        Type = DisplayconfigDeviceInfoGetSourceName,
                        Size = (uint)sizeof(DisplayConfigSourceDeviceName),
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
        /// Gets friendly name, device path, and output technology for a monitor target.
        /// </summary>
        /// <param name="adapterId">Adapter ID</param>
        /// <param name="targetId">Target ID</param>
        /// <returns>Tuple of (friendlyName, devicePath, outputTechnology), values may be null/0 if retrieval fails</returns>
        private static unsafe (string? FriendlyName, string? DevicePath, uint OutputTechnology) GetTargetDeviceInfo(LUID adapterId, uint targetId)
        {
            try
            {
                var deviceName = new DisplayConfigTargetDeviceName
                {
                    Header = new DisplayConfigDeviceInfoHeader
                    {
                        Type = DisplayconfigDeviceInfoGetTargetName,
                        Size = (uint)sizeof(DisplayConfigTargetDeviceName),
                        AdapterId = adapterId,
                        Id = targetId,
                    },
                };

                var result = DisplayConfigGetDeviceInfo(&deviceName);
                if (result == 0)
                {
                    return (
                        deviceName.GetMonitorFriendlyDeviceName(),
                        deviceName.GetMonitorDevicePath(),
                        deviceName.OutputTechnology);
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
            }

            return (null, null, 0u);
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
                var paths = new DisplayConfigPathInfo[pathCount];
                var modes = new DisplayConfigModeInfo[modeCount];

                // Query display configuration using fixed pointer
                fixed (DisplayConfigPathInfo* pathsPtr = paths)
                {
                    fixed (DisplayConfigModeInfo* modesPtr = modes)
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

                    // Get target info (friendly name, device path, output technology)
                    var (friendlyName, devicePath, outputTechnology) = GetTargetDeviceInfo(path.TargetInfo.AdapterId, path.TargetInfo.Id);

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
                        AdapterId = path.TargetInfo.AdapterId,
                        TargetId = path.TargetInfo.Id,
                        MonitorNumber = i + 1, // 1-based, matches Windows Display Settings
                        OutputTechnology = outputTechnology,
                        IsInternal = DisplayClassifier.IsInternal(outputTechnology),
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
