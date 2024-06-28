// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using MouseJumpUI.Common.Models.Drawing;
using MouseJumpUI.Common.NativeMethods;
using static MouseJumpUI.Common.NativeMethods.Core;
using static MouseJumpUI.Common.NativeMethods.User32;

namespace MouseJumpUI.Common.Helpers;

internal static class ScreenHelper
{
    /// <summary>
    /// Duplicates functionality available in System.Windows.Forms.SystemInformation
    /// to reduce the dependency on WinForms
    /// </summary>
    public static RectangleInfo GetVirtualScreen()
    {
        return new(
            User32.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_XVIRTUALSCREEN),
            User32.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_YVIRTUALSCREEN),
            User32.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXVIRTUALSCREEN),
            User32.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYVIRTUALSCREEN));
    }

    internal static IEnumerable<ScreenInfo> GetAllScreens()
    {
        // enumerate the monitors attached to the system
        var hMonitors = new List<HMONITOR>();
        var callback = new User32.MONITORENUMPROC(
            (hMonitor, hdcMonitor, lprcMonitor, dwData) =>
            {
                hMonitors.Add(hMonitor);
                return true;
            });
        var result = User32.EnumDisplayMonitors(HDC.Null, LPCRECT.Null, callback, LPARAM.Null);
        if (!result)
        {
            throw new Win32Exception(
                result.Value,
                $"{nameof(User32.EnumDisplayMonitors)} failed with return code {result.Value}");
        }

        // get detailed info about each monitor
        foreach (var hMonitor in hMonitors)
        {
            var monitorInfoPtr = new LPMONITORINFO(
                new MONITORINFO((DWORD)MONITORINFO.Size, RECT.Empty, RECT.Empty, 0));
            result = User32.GetMonitorInfoW(hMonitor, monitorInfoPtr);
            if (!result)
            {
                throw new Win32Exception(
                    result.Value,
                    $"{nameof(User32.GetMonitorInfoW)} failed with return code {result.Value}");
            }

            var monitorInfo = monitorInfoPtr.ToStructure();
            monitorInfoPtr.Free();

            yield return new ScreenInfo(
                handle: hMonitor,
                primary: monitorInfo.dwFlags.HasFlag(User32.MONITOR_INFO_FLAGS.MONITORINFOF_PRIMARY),
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

    internal static ScreenInfo GetScreenFromPoint(
        List<ScreenInfo> screens,
        PointInfo pt)
    {
        // get the monitor handle from the point
        var hMonitor = User32.MonitorFromPoint(
            new((int)pt.X, (int)pt.Y),
            User32.MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
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
