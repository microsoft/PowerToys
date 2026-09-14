// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ScreenTranslator.Core.Translation;

namespace ScreenTranslator.Helpers;

public static class MonitorHelper
{
    public static IReadOnlyList<ScreenInfo> GetAllScreens()
    {
        List<ScreenInfo> screens = new();

        OSInterop.EnumDisplayMonitors(
            IntPtr.Zero,
            IntPtr.Zero,
            (IntPtr hMonitor, IntPtr hdcMonitor, ref OSInterop.RECT lprcMonitor, IntPtr dwData) =>
            {
                OSInterop.MONITORINFOEX mi = default;
                mi.cbSize = Marshal.SizeOf(typeof(OSInterop.MONITORINFOEX));
                if (OSInterop.GetMonitorInfo(hMonitor, ref mi))
                {
                    PhysicalRect bounds = new(mi.rcMonitor.Left, mi.rcMonitor.Top, mi.rcMonitor.Width, mi.rcMonitor.Height);
                    PhysicalRect work = new(mi.rcWork.Left, mi.rcWork.Top, mi.rcWork.Width, mi.rcWork.Height);
                    bool isPrimary = (mi.dwFlags & 1) != 0;

                    double dpiX = 1.0;
                    double dpiY = 1.0;
                    try
                    {
                        if (OSInterop.GetDpiForMonitor(hMonitor, OSInterop.DpiType.Effective, out uint rawDpiX, out uint rawDpiY) == IntPtr.Zero)
                        {
                            dpiX = rawDpiX / 96.0;
                            dpiY = rawDpiY / 96.0;
                        }
                    }
                    catch
                    {
                        dpiX = 1.0;
                        dpiY = 1.0;
                    }

                    screens.Add(new ScreenInfo(bounds, work, dpiX, dpiY, isPrimary, hMonitor));
                }

                return true;
            },
            IntPtr.Zero);

        if (screens.Count == 0)
        {
            screens.Add(new ScreenInfo(
                new PhysicalRect(0, 0, OSInterop.GetSystemMetrics(OSInterop.SM_CXSCREEN), OSInterop.GetSystemMetrics(OSInterop.SM_CYSCREEN)),
                new PhysicalRect(0, 0, OSInterop.GetSystemMetrics(OSInterop.SM_CXSCREEN), OSInterop.GetSystemMetrics(OSInterop.SM_CYSCREEN)),
                1.0,
                1.0,
                true,
                IntPtr.Zero));
        }

        return screens;
    }
}
