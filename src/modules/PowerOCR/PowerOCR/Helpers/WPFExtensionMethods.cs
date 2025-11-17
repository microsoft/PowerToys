// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace PowerOCR;

public static class WPFExtensionMethods
{
    public static Point GetAbsolutePosition(this Window w)
    {
        if (w.WindowState != WindowState.Maximized)
        {
            return new Point(w.Left, w.Top);
        }

        Int32Rect r;
        bool multiMonitorSupported = OSInterop.GetSystemMetrics(OSInterop.SM_CMONITORS) != 0;
        if (!multiMonitorSupported)
        {
            OSInterop.RECT rc = default(OSInterop.RECT);
            OSInterop.SystemParametersInfo(48, 0, ref rc, 0);
            r = new Int32Rect(rc.Left, rc.Top, rc.Width, rc.Height);
        }
        else
        {
            WindowInteropHelper helper = new WindowInteropHelper(w);
            IntPtr hmonitor = OSInterop.MonitorFromWindow(new HandleRef(null, helper.EnsureHandle()), 2);
            OSInterop.MONITORINFOEX info = new OSInterop.MONITORINFOEX();
            OSInterop.GetMonitorInfo(new HandleRef(null, hmonitor), info);
            r = new Int32Rect(info.RcMonitor.Left, info.RcMonitor.Top, info.RcMonitor.Width, info.RcMonitor.Height);
        }

        return new Point(r.X, r.Y);
    }

    public static DpiScale GetDpi(this System.Windows.Forms.Screen screen)
    {
        var point = new System.Drawing.Point(screen.Bounds.Left + 1, screen.Bounds.Top + 1);
        var mon = MonitorFromPoint(point, 2/*MONITOR_DEFAULTTONEAREST*/);
        GetDpiForMonitor(mon, DpiType.Effective, out uint dpiX, out uint dpiY);
        return new DpiScale(dpiX / 96.0, dpiY / 96.0);
    }

    // https://msdn.microsoft.com/library/windows/desktop/dd145062(v=vs.85).aspx
    [DllImport("User32.dll")]
    private static extern IntPtr MonitorFromPoint([In] System.Drawing.Point pt, [In] uint dwFlags);

    // https://msdn.microsoft.com/library/windows/desktop/dn280510(v=vs.85).aspx
    [DllImport("Shcore.dll")]
    private static extern IntPtr GetDpiForMonitor([In] IntPtr hmonitor, [In] DpiType dpiType, [Out] out uint dpiX, [Out] out uint dpiY);

    // https://msdn.microsoft.com/library/windows/desktop/dn280511(v=vs.85).aspx
    public enum DpiType
    {
        Effective = 0,
        Angular = 1,
        Raw = 2,
    }
}
