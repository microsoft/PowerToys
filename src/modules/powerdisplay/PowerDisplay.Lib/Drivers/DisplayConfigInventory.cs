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
    /// <see cref="MonitorDisplayInfo"/> inventory used by Phase 0 classification.
    /// This layer is independent of DDC/CI and WMI — both downstream controllers
    /// consume its output via <see cref="DisplayClassifier"/>.
    /// </summary>
    public static class DisplayConfigInventory
    {
        /// <summary>
        /// Gets complete information for all monitors, keyed by device path.
        /// The device path is unique per target and supports mirror mode.
        /// </summary>
        /// <returns>Dictionary keyed by device path containing monitor information</returns>
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
                Logger.LogError($"DisplayConfigInventory: GetAllMonitorDisplayInfo exception: {ex.Message}");
            }

            return monitorInfo;
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
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
            }

            return null;
        }

        /// <summary>
        /// Gets friendly name, device path, and output technology for a monitor target.
        /// </summary>
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
    }
}
