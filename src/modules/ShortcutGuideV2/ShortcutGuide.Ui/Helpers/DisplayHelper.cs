// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Windows.Foundation;
using WinUIEx;

namespace ShortcutGuide.Helpers
{
    public static partial class DisplayHelper
    {
        private enum MonitorFromWindowDwFlags : int
        {
            MONITOR_DEFAULTTONEAREST = 2,
        }

        public static Rect GetWorkAreaForDisplayWithWindow(nint hwnd)
        {
            foundMonitorIndex = -1;
            monitorIndex = 0;
            var monitor = NativeMethods.MonitorFromWindow(hwnd, (int)MonitorFromWindowDwFlags.MONITOR_DEFAULTTONEAREST);
            NativeMethods.EnumDisplayMonitors(nint.Zero, nint.Zero, MonitorEnumProc, new NativeMethods.LPARAM(monitor));
            return MonitorInfo.GetDisplayMonitors()[foundMonitorIndex].RectWork;
        }

        private static int foundMonitorIndex = -1;
        private static int monitorIndex;

        private static bool MonitorEnumProc(nint hMonitor, nint hdcMonitor, ref NativeMethods.RECT lprcMonitor, nint dwData)
        {
            nint targetMonitor = dwData;

            if (hMonitor == targetMonitor)
            {
                foundMonitorIndex = monitorIndex;
                return false;
            }

            monitorIndex++;
            return true;
        }
    }
}
