// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>
/// Multi-monitor enumeration via Win32 (<c>EnumDisplayMonitors</c> / <c>GetMonitorInfo</c>).
/// winappcli exposes no display topology, so this stays native — useful for multi-monitor
/// utilities (FancyZones, Mouse Utilities, Mouse Without Borders).
/// </summary>
public static class MonitorInfo
{
    /// <summary>One physical display, in virtual-screen pixel coordinates.</summary>
    public sealed record Monitor(
        string DeviceName,
        int Left,
        int Top,
        int Right,
        int Bottom,
        int WorkLeft,
        int WorkTop,
        int WorkRight,
        int WorkBottom,
        bool IsPrimary)
    {
        /// <summary>Full monitor width in pixels.</summary>
        public int Width => Right - Left;

        /// <summary>Full monitor height in pixels.</summary>
        public int Height => Bottom - Top;

        /// <summary>Usable work-area width in pixels.</summary>
        public int WorkWidth => WorkRight - WorkLeft;

        /// <summary>Usable work-area height in pixels.</summary>
        public int WorkHeight => WorkBottom - WorkTop;
    }

    private const uint MonitorInfoPrimary = 0x1;
    private const uint MonitorDefaultToNearest = 0x2;

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint dwFlags);

    /// <summary>All connected displays, in enumeration order.</summary>
    public static IReadOnlyList<Monitor> GetAll()
    {
        var list = new List<Monitor>();

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, EnumCallback, IntPtr.Zero);
        return list;

        bool EnumCallback(IntPtr hMonitor, IntPtr hdc, ref RECT lprcMonitor, IntPtr dwData)
        {
            var mi = new MONITORINFOEX { CbSize = Marshal.SizeOf<MONITORINFOEX>() };
            if (GetMonitorInfo(hMonitor, ref mi))
            {
                list.Add(CreateMonitor(mi));
            }

            return true;
        }
    }

    /// <summary>The primary display, or null if none reported.</summary>
    public static Monitor? GetPrimary() => GetAll().FirstOrDefault(m => m.IsPrimary);

    /// <summary>Number of connected displays.</summary>
    public static int Count => GetAll().Count;

    /// <summary>
    /// The monitor containing most of <paramref name="hWnd"/>, or the nearest monitor when the
    /// window is currently outside every monitor.
    /// </summary>
    /// <remarks>
    /// Monitor and work-area coordinates match physical DWM bounds only from a per-monitor-DPI-aware
    /// test host. Every UITestAutomation.Next test executable should embed the framework's
    /// PerMonitorV2 application manifest.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="hWnd"/> is invalid, was destroyed during the lookup, or its monitor
    /// information could not be read.
    /// </exception>
    public static Monitor GetFromWindow(IntPtr hWnd) =>
        GetFromWindow(hWnd, IsWindow, MonitorFromWindow, GetMonitor);

    internal static Monitor GetFromWindow(
        IntPtr hWnd,
        Func<IntPtr, bool> isWindow,
        Func<IntPtr, uint, IntPtr> monitorFromWindow,
        Func<IntPtr, Monitor> getMonitor)
    {
        ArgumentNullException.ThrowIfNull(isWindow);
        ArgumentNullException.ThrowIfNull(monitorFromWindow);
        ArgumentNullException.ThrowIfNull(getMonitor);

        if (hWnd == IntPtr.Zero || !isWindow(hWnd))
        {
            throw new InvalidOperationException($"Cannot query a monitor for invalid or destroyed HWND {FormatHandle(hWnd)}.");
        }

        var hMonitor = monitorFromWindow(hWnd, MonitorDefaultToNearest);
        if (hMonitor == IntPtr.Zero)
        {
            throw new InvalidOperationException($"MonitorFromWindow failed for HWND {FormatHandle(hWnd)}.");
        }

        var monitor = getMonitor(hMonitor);
        if (!isWindow(hWnd))
        {
            throw new InvalidOperationException($"HWND {FormatHandle(hWnd)} was destroyed during its monitor lookup.");
        }

        return monitor;
    }

    private static Monitor GetMonitor(IntPtr hMonitor)
    {
        var mi = new MONITORINFOEX { CbSize = Marshal.SizeOf<MONITORINFOEX>() };
        if (!GetMonitorInfo(hMonitor, ref mi))
        {
            throw new InvalidOperationException(
                $"GetMonitorInfo failed for HMONITOR {FormatHandle(hMonitor)} with Win32 error {Marshal.GetLastWin32Error()}.");
        }

        return CreateMonitor(mi);
    }

    private static Monitor CreateMonitor(MONITORINFOEX mi) =>
        new(
            mi.SzDevice,
            mi.RcMonitor.Left,
            mi.RcMonitor.Top,
            mi.RcMonitor.Right,
            mi.RcMonitor.Bottom,
            mi.RcWork.Left,
            mi.RcWork.Top,
            mi.RcWork.Right,
            mi.RcWork.Bottom,
            (mi.DwFlags & MonitorInfoPrimary) != 0);

    private static string FormatHandle(IntPtr handle) => $"0x{handle.ToInt64():X}";

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MONITORINFOEX
    {
        public int CbSize;
        public RECT RcMonitor;
        public RECT RcWork;
        public uint DwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string SzDevice;
    }
}
