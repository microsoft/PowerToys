// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using ManagedCommon;
using Windows.Win32.Foundation;
using static PowerDisplay.Common.Drivers.NativeConstants;
using static PowerDisplay.Common.Drivers.PInvoke;

namespace PowerDisplay.Common.Drivers
{
    /// <summary>
    /// Win32 DisplayConfig API wrapper that enumerates all active display paths
    /// (QueryDisplayConfig + DisplayConfigGetDeviceInfo) and produces a neutral
    /// <see cref="MonitorDisplayInfo"/> inventory. This layer is independent of DDC/CI and
    /// WMI; <see cref="MonitorManager"/> routes the inventory to both downstream controllers.
    /// </summary>
    public static class DisplayConfigInventory
    {
        private const int MaxQueryAttempts = 3;

        /// <summary>
        /// Gets complete information for all monitors, keyed by device path.
        /// The device path is unique per target and supports mirror mode.
        /// </summary>
        /// <returns>Dictionary keyed by device path containing monitor information</returns>
        public static Dictionary<string, MonitorDisplayInfo> GetAllMonitorDisplayInfo()
        {
            TryGetAllMonitorDisplayInfo(out var monitorInfo);
            return monitorInfo;
        }

        /// <summary>
        /// Same as <see cref="GetAllMonitorDisplayInfo"/>, but returns false if the topology read
        /// failed or any active path could not be resolved, i.e. a display may be missing.
        /// </summary>
        public static unsafe bool TryGetAllMonitorDisplayInfo(out Dictionary<string, MonitorDisplayInfo> monitorInfo)
        {
            monitorInfo = new Dictionary<string, MonitorDisplayInfo>(StringComparer.OrdinalIgnoreCase);
            var complete = true;

            try
            {
                DisplayConfigPathInfo[] paths;
                uint pathCount;

                // The topology can change between sizing the buffers and querying them, which
                // fails with ERROR_INSUFFICIENT_BUFFER; re-size and retry.
                for (var attempt = 1; ; attempt++)
                {
                    // Get buffer sizes
                    var result = GetDisplayConfigBufferSizes(QdcOnlyActivePaths, out pathCount, out uint modeCount);
                    if (result != 0)
                    {
                        return false;
                    }

                    // Allocate buffers
                    paths = new DisplayConfigPathInfo[pathCount];
                    var modes = new DisplayConfigModeInfo[modeCount];

                    // Query display configuration using fixed pointer
                    fixed (DisplayConfigPathInfo* pathsPtr = paths)
                    {
                        fixed (DisplayConfigModeInfo* modesPtr = modes)
                        {
                            result = QueryDisplayConfig(QdcOnlyActivePaths, ref pathCount, pathsPtr, ref modeCount, modesPtr, IntPtr.Zero);
                        }
                    }

                    if (result == 0)
                    {
                        break;
                    }

                    if (result != ErrorInsufficientBuffer || attempt >= MaxQueryAttempts)
                    {
                        return false;
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
                        complete = false;
                        continue;
                    }

                    // Get target info (friendly name, device path)
                    var (friendlyName, devicePath) = GetTargetDeviceInfo(path.TargetInfo.AdapterId, path.TargetInfo.Id);

                    // Device path is the dictionary key; skip targets that don't have one.
                    if (string.IsNullOrEmpty(devicePath))
                    {
                        complete = false;
                        continue;
                    }

                    monitorInfo[devicePath] = new MonitorDisplayInfo
                    {
                        DevicePath = devicePath,
                        GdiDeviceName = gdiDeviceName,
                        FriendlyName = friendlyName ?? string.Empty,
                        MonitorNumber = i + 1, // 1-based, matches Windows Display Settings
                    };
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Logger.LogError($"DisplayConfigInventory: GetAllMonitorDisplayInfo exception: {ex.Message}");
                return false;
            }

            return complete;
        }

        /// <summary>
        /// Gets GDI device name for a source (e.g., "\\.\DISPLAY1").
        /// </summary>
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

                Logger.LogWarning(
                    $"DisplayConfigInventory: GetSourceGdiDeviceName failed (adapter.low=0x{adapterId.LowPart:X}, source={sourceId}, win32={result})");
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Logger.LogWarning(
                    $"DisplayConfigInventory: GetSourceGdiDeviceName exception (adapter.low=0x{adapterId.LowPart:X}, source={sourceId}): {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Gets friendly name and device path for a monitor target.
        /// </summary>
        private static unsafe (string? FriendlyName, string? DevicePath) GetTargetDeviceInfo(LUID adapterId, uint targetId)
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
                        deviceName.GetMonitorDevicePath());
                }

                Logger.LogWarning(
                    $"DisplayConfigInventory: GetTargetDeviceInfo failed (adapter.low=0x{adapterId.LowPart:X}, target={targetId}, win32={result})");
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Logger.LogWarning(
                    $"DisplayConfigInventory: GetTargetDeviceInfo exception (adapter.low=0x{adapterId.LowPart:X}, target={targetId}): {ex.Message}");
            }

            return (null, null);
        }
    }
}
