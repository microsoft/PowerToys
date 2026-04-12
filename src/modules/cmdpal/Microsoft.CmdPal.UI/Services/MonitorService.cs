// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels.Models;

namespace Microsoft.CmdPal.UI.Services;

/// <summary>
/// Implementation of <see cref="IMonitorService"/> using Win32 monitor enumeration APIs.
/// </summary>
public sealed class MonitorService : IMonitorService
{
    private const int PrimaryFlag = 0x00000001;

    private readonly object _lock = new();
    private List<MonitorInfo>? _cachedMonitors;

    private delegate bool MonitorEnumProc(
        IntPtr hMonitor,
        IntPtr hdcMonitor,
        IntPtr lprcMonitor,
        IntPtr dwData);

    /// <inheritdoc/>
    public event EventHandler? MonitorsChanged;

    /// <inheritdoc/>
    public IReadOnlyList<MonitorInfo> GetMonitors()
    {
        lock (_lock)
        {
            _cachedMonitors = EnumerateMonitors();
            return _cachedMonitors;
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
        }

        MonitorsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static List<MonitorInfo> EnumerateMonitors()
    {
        var monitors = new List<MonitorInfo>();

        EnumDisplayMonitors(
            IntPtr.Zero,
            IntPtr.Zero,
            (hMonitor, hdcMonitor, lprcMonitor, dwData) =>
            {
                var info = default(NativeMonitorInfoEx);
                info.Size = Marshal.SizeOf<NativeMonitorInfoEx>();
                if (GetMonitorInfo(hMonitor, ref info))
                {
                    _ = GetDpiForMonitor(hMonitor, 0, out var dpiX, out _);

                    var isPrimary = (info.Flags & PrimaryFlag) != 0;
                    var deviceName = info.DeviceName;
                    var displayName = FormatDisplayName(deviceName, isPrimary);

                    monitors.Add(new MonitorInfo
                    {
                        DeviceId = deviceName,
                        DisplayName = displayName,
                        Bounds = new ScreenRect(
                            info.Monitor.Left,
                            info.Monitor.Top,
                            info.Monitor.Right,
                            info.Monitor.Bottom),
                        WorkArea = new ScreenRect(
                            info.Work.Left,
                            info.Work.Top,
                            info.Work.Right,
                            info.Work.Bottom),
                        Dpi = dpiX,
                        IsPrimary = isPrimary,
                    });
                }

                return true;
            },
            IntPtr.Zero);

        return monitors;
    }

    private static string FormatDisplayName(string deviceName, bool isPrimary)
    {
        // Convert "\\.\DISPLAY1" → "Display 1"
        var name = deviceName;
        if (name.StartsWith(@"\\.\DISPLAY", StringComparison.OrdinalIgnoreCase))
        {
            var number = name.Substring(@"\\.\DISPLAY".Length);
            name = $"Display {number}";
        }

        if (isPrimary)
        {
            name += " (Primary)";
        }

        return name;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(
        IntPtr hdc,
        IntPtr lprcClip,
        MonitorEnumProc lpfnEnum,
        IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref NativeMonitorInfoEx lpmi);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(
        IntPtr hMonitor,
        int dpiType,
        out uint dpiX,
        out uint dpiY);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeMonitorInfoEx
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
    }
}
