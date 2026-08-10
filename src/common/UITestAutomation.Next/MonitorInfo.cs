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
    }

    private const uint MONITORINFOF_PRIMARY = 0x1;

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

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
                list.Add(new Monitor(
                    mi.SzDevice,
                    mi.RcMonitor.Left,
                    mi.RcMonitor.Top,
                    mi.RcMonitor.Right,
                    mi.RcMonitor.Bottom,
                    mi.RcWork.Left,
                    mi.RcWork.Top,
                    mi.RcWork.Right,
                    mi.RcWork.Bottom,
                    (mi.DwFlags & MONITORINFOF_PRIMARY) != 0));
            }

            return true;
        }
    }

    /// <summary>The primary display, or null if none reported.</summary>
    public static Monitor? GetPrimary() => GetAll().FirstOrDefault(m => m.IsPrimary);

    /// <summary>Number of connected displays.</summary>
    public static int Count => GetAll().Count;

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
