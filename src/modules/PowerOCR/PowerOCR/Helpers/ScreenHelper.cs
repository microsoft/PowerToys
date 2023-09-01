// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using PowerOCR.Models.Drawing;
using PowerOCR.NativeMethods;
using static PowerOCR.NativeMethods.Core;

namespace PowerOCR.Helpers;

internal static class ScreenHelper
{
    public static IEnumerable<ScreenInfo> GetAllScreens()
    {
        // enumerate the monitors attached to the system
        var hMonitors = new List<HMONITOR>();
        var result = User32.EnumDisplayMonitors(
            HDC.Null,
            LPCRECT.Null,
            (unnamedParam1, unnamedParam2, unnamedParam3, unnamedParam4) =>
            {
                hMonitors.Add(unnamedParam1);
                return true;
            },
            LPARAM.Null);
        if (!result)
        {
            throw new Win32Exception(
                $"{nameof(User32.EnumDisplayMonitors)} failed with return code {result.Value}");
        }

        // get detailed info about each monitor
        foreach (var hMonitor in hMonitors)
        {
            var monitorInfoPtr = new User32.LPMONITORINFO(
                new User32.MONITORINFO((uint)User32.MONITORINFO.Size, RECT.Empty, RECT.Empty, 0));
            result = User32.GetMonitorInfoW(hMonitor, monitorInfoPtr);
            if (!result)
            {
                throw new Win32Exception(
                    $"{nameof(User32.GetMonitorInfoW)} failed with return code {result.Value}");
            }

            var monitorInfo = monitorInfoPtr.ToStructure();
            monitorInfoPtr.Free();

            yield return new ScreenInfo(
                handle: hMonitor,
                primary: monitorInfo.dwFlags.HasFlag(User32.MONITOR_INFO_FLAGS.MONITORINFOF_PRIMARY),
                displayArea: new Rectangle(
                    monitorInfo.rcMonitor.left,
                    monitorInfo.rcMonitor.top,
                    monitorInfo.rcMonitor.right - monitorInfo.rcMonitor.left,
                    monitorInfo.rcMonitor.bottom - monitorInfo.rcMonitor.top),
                workingArea: new Rectangle(
                    monitorInfo.rcWork.left,
                    monitorInfo.rcWork.top,
                    monitorInfo.rcWork.right - monitorInfo.rcWork.left,
                    monitorInfo.rcWork.bottom - monitorInfo.rcWork.top));
        }
    }
}
