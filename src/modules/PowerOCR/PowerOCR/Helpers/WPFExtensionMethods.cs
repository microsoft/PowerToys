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
}
