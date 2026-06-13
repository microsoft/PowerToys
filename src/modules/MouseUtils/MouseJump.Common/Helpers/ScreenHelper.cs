// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

using MouseJump.Common.Interop;
using MouseJump.Models.Display;
using MouseJump.Models.Drawing;

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MouseJump.Common.Helpers;

public static class ScreenHelper
{
    /// <summary>
    /// Duplicates functionality available in System.Windows.Forms.SystemInformation
    /// to reduce the dependency on WinForms
    /// </summary>
    private static RectangleInfo GetVirtualScreen()
    {
        return new(
            PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_XVIRTUALSCREEN),
            PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_YVIRTUALSCREEN),
            PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXVIRTUALSCREEN),
            PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYVIRTUALSCREEN));
    }

    public static IEnumerable<ScreenInfo> GetAllScreens()
    {
        // enumerate the monitors attached to the system
        var hMonitors = new List<HMONITOR>();
        unsafe
        {
            var callback = new MONITORENUMPROC(
                (hMonitor, hdcMonitor, lprcMonitor, dwData) =>
                {
                    hMonitors.Add(hMonitor);
                    return true;
                });
            var result = PInvoke.EnumDisplayMonitors(HDC.Null, null, callback, (LPARAM)0);
            if (result == 0)
            {
                throw new InvalidOperationException("failed to enumerate monitors");
            }

            // prevent callback from being collected during the enumeration
            GC.KeepAlive(callback);
        }

        // get detailed info about each monitor
        var monitorInfo = new MONITORINFO
        {
            cbSize = (uint)Marshal.SizeOf<MONITORINFO>(),
        };
        foreach (var hMonitor in hMonitors)
        {
            var result = PInvoke.GetMonitorInfo(hMonitor, ref monitorInfo);
            ResultHandler.ThrowIfZero(
                result,
                getLastError: true,
                memberName: nameof(PInvoke.GetMonitorInfo));

            yield return new ScreenInfo(
                handle: hMonitor,
                primary: (monitorInfo.dwFlags & PInvoke.MONITORINFOF_PRIMARY) != 0,
                displayArea: new RectangleInfo(
                    monitorInfo.rcMonitor.left,
                    monitorInfo.rcMonitor.top,
                    monitorInfo.rcMonitor.right - monitorInfo.rcMonitor.left,
                    monitorInfo.rcMonitor.bottom - monitorInfo.rcMonitor.top),
                workingArea: new RectangleInfo(
                    monitorInfo.rcWork.left,
                    monitorInfo.rcWork.top,
                    monitorInfo.rcWork.right - monitorInfo.rcWork.left,
                    monitorInfo.rcWork.bottom - monitorInfo.rcWork.top));
        }
    }

    public static ScreenInfo GetScreenFromPoint(
        List<ScreenInfo> screens,
        PointInfo pt)
    {
        // get the monitor handle from the point
        var hMonitor = PInvoke.MonitorFromPoint(
            new((int)pt.X, (int)pt.Y),
            MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        if (hMonitor.IsNull)
        {
            throw new InvalidOperationException($"no monitor found for point {pt}");
        }

        // find the screen with the given monitor handle
        var screen = screens
            .Single(item => item.Handle == hMonitor);
        return screen;
    }
}
