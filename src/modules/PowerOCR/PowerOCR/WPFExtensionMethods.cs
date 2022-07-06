using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

static class WPFExtensionMethods
{
    public static Point GetAbsolutePosition(this Window w)
    {
        if (w.WindowState != WindowState.Maximized)
            return new Point(w.Left, w.Top);

        Int32Rect r;
        bool MultiMonitorSupported = OSInterop.GetSystemMetrics(OSInterop.SM_CMONITORS) != 0;
        if (!MultiMonitorSupported)
        {
            OSInterop.RECT rc = new();
            OSInterop.SystemParametersInfo(48, 0, ref rc, 0);
            r = new Int32Rect(rc.left, rc.top, rc.width, rc.height);
        }
        else
        {
            WindowInteropHelper helper = new(w);
            IntPtr hmonitor = OSInterop.MonitorFromWindow(new HandleRef(null, helper.EnsureHandle()), 2);
            OSInterop.MONITORINFOEX info = new();
            OSInterop.GetMonitorInfo(new HandleRef(null, hmonitor), info);
            r = new Int32Rect(info.rcMonitor.left, info.rcMonitor.top, info.rcMonitor.width, info.rcMonitor.height);
        }
        return new Point(r.X, r.Y);
    }
}