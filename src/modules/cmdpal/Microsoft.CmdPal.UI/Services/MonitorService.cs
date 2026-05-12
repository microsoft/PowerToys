// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Windows.Win32;
using Windows.Win32.Devices.Display;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;

namespace Microsoft.CmdPal.UI.Services;

/// <summary>
/// Implementation of <see cref="IMonitorService"/> using Win32 monitor enumeration APIs.
/// </summary>
public sealed class MonitorService : IMonitorService
{
    private const uint PrimaryFlag = 0x00000001;

    private readonly object _lock = new();
    private List<MonitorInfo>? _cachedMonitors;
    private IReadOnlyList<MonitorInfo>? _cachedSnapshot;

    /// <inheritdoc/>
    public event EventHandler? MonitorsChanged;

    /// <inheritdoc/>
    public IReadOnlyList<MonitorInfo> GetMonitors()
    {
        lock (_lock)
        {
            if (_cachedSnapshot is not null)
            {
                return _cachedSnapshot;
            }

            _cachedMonitors = EnumerateMonitors();
            _cachedSnapshot = _cachedMonitors.AsReadOnly();
            return _cachedSnapshot;
        }
    }

    /// <inheritdoc/>
    public MonitorInfo? GetMonitorByDeviceId(string deviceId)
    {
        var monitors = GetMonitors();
        foreach (var monitor in monitors)
        {
            if (string.Equals(monitor.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
            {
                return monitor;
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public MonitorInfo? GetPrimaryMonitor()
    {
        var monitors = GetMonitors();
        foreach (var monitor in monitors)
        {
            if (monitor.IsPrimary)
            {
                return monitor;
            }
        }

        return null;
    }

    /// <summary>
    /// Call this when a display settings change message is received (e.g. WM_DISPLAYCHANGE)
    /// to invalidate the cached monitor list and raise <see cref="MonitorsChanged"/>.
    /// </summary>
    public void NotifyMonitorsChanged()
    {
        lock (_lock)
        {
            _cachedMonitors = null;
            _cachedSnapshot = null;
        }

        Logger.LogDebug("Display topology changed, invalidating monitor cache");
        MonitorsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static unsafe List<MonitorInfo> EnumerateMonitors()
    {
        var monitors = new List<MonitorInfo>();
        var friendlyNames = BuildFriendlyNameMap();

        PInvoke.EnumDisplayMonitors(
            HDC.Null,
            (RECT*)null,
            (HMONITOR hMonitor, HDC hdcMonitor, RECT* lprcMonitor, LPARAM dwData) =>
            {
                var infoEx = default(MONITORINFOEXW);
                infoEx.monitorInfo.cbSize = (uint)sizeof(MONITORINFOEXW);
                if (PInvoke.GetMonitorInfo(hMonitor, (MONITORINFO*)&infoEx))
                {
                    var hr = PInvoke.GetDpiForMonitor(
                        hMonitor,
                        MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI,
                        out var dpiX,
                        out _);
                    if (hr.Failed || dpiX == 0)
                    {
                        dpiX = 96;
                    }

                    var isPrimary = (infoEx.monitorInfo.dwFlags & PrimaryFlag) != 0;
                    var deviceName = new string(infoEx.szDevice.AsSpan()).TrimEnd('\0');
                    var displayName = FormatDisplayName(deviceName, isPrimary, friendlyNames);
                    var rcMonitor = infoEx.monitorInfo.rcMonitor;
                    var rcWork = infoEx.monitorInfo.rcWork;

                    monitors.Add(new MonitorInfo
                    {
                        DeviceId = deviceName,
                        DisplayName = displayName,
                        Bounds = new ScreenRect(
                            rcMonitor.left,
                            rcMonitor.top,
                            rcMonitor.right,
                            rcMonitor.bottom),
                        WorkArea = new ScreenRect(
                            rcWork.left,
                            rcWork.top,
                            rcWork.right,
                            rcWork.bottom),
                        Dpi = dpiX,
                        IsPrimary = isPrimary,
                    });
                }

                return true;
            },
            0);

        return monitors;
    }

    /// <summary>
    /// Builds a map from GDI device name (e.g. <c>\\.\DISPLAY1</c>) to the hardware
    /// friendly name (e.g. <c>DELL U2723QE</c>) using the Display Configuration APIs.
    /// Returns an empty dictionary on failure so callers can fall back gracefully.
    /// </summary>
    private static unsafe Dictionary<string, string> BuildFriendlyNameMap()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var result = PInvoke.GetDisplayConfigBufferSizes(
                QUERY_DISPLAY_CONFIG_FLAGS.QDC_ONLY_ACTIVE_PATHS,
                out var pathCount,
                out var modeCount);
            if (result != WIN32_ERROR.NO_ERROR)
            {
                return map;
            }

            var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];

            fixed (DISPLAYCONFIG_PATH_INFO* pathsPtr = paths)
            {
                fixed (DISPLAYCONFIG_MODE_INFO* modesPtr = modes)
                {
                    result = PInvoke.QueryDisplayConfig(
                        QUERY_DISPLAY_CONFIG_FLAGS.QDC_ONLY_ACTIVE_PATHS,
                        ref pathCount,
                        pathsPtr,
                        ref modeCount,
                        modesPtr,
                        null);
                    if (result != WIN32_ERROR.NO_ERROR)
                    {
                        return map;
                    }
                }
            }

            for (var i = 0; i < pathCount; i++)
            {
                var path = paths[i];

                // Get the GDI device name from the source info
                var sourceName = default(DISPLAYCONFIG_SOURCE_DEVICE_NAME);
                sourceName.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
                sourceName.header.size = (uint)sizeof(DISPLAYCONFIG_SOURCE_DEVICE_NAME);
                sourceName.header.adapterId = path.sourceInfo.adapterId;
                sourceName.header.id = path.sourceInfo.id;

                if (PInvoke.DisplayConfigGetDeviceInfo(ref sourceName.header) != 0)
                {
                    continue;
                }

                var gdiName = new string(sourceName.viewGdiDeviceName.AsSpan()).TrimEnd('\0');
                if (string.IsNullOrEmpty(gdiName))
                {
                    continue;
                }

                // Get the friendly name from the target info
                var targetName = default(DISPLAYCONFIG_TARGET_DEVICE_NAME);
                targetName.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME;
                targetName.header.size = (uint)sizeof(DISPLAYCONFIG_TARGET_DEVICE_NAME);
                targetName.header.adapterId = path.targetInfo.adapterId;
                targetName.header.id = path.targetInfo.id;

                if (PInvoke.DisplayConfigGetDeviceInfo(ref targetName.header) != 0)
                {
                    continue;
                }

                var friendly = new string(targetName.monitorFriendlyDeviceName.AsSpan()).TrimEnd('\0');
                if (!string.IsNullOrEmpty(friendly))
                {
                    map.TryAdd(gdiName, friendly);
                }
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Logger.LogError($"BuildFriendlyNameMap failed: {ex.Message}");
        }

        return map;
    }

    private static string FormatDisplayName(string deviceName, bool isPrimary, Dictionary<string, string> friendlyNames)
    {
        string name;

        if (friendlyNames.TryGetValue(deviceName, out var friendly))
        {
            name = friendly;
        }
        else if (deviceName.StartsWith(@"\\.\DISPLAY", StringComparison.OrdinalIgnoreCase))
        {
            // Fallback: convert "\\.\DISPLAY1" → "Display 1"
            var number = deviceName.Substring(@"\\.\DISPLAY".Length);
            name = $"Display {number}";
        }
        else
        {
            name = deviceName;
        }

        if (isPrimary)
        {
            name += " (Primary)";
        }

        return name;
    }
}
