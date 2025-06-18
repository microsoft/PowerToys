// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation;
using WinUIEx;

namespace ShortcutGuide.Helpers
{
    public static class DisplayHelper
    {
        private enum MonitorFromWindowDwFlags
        {
            MONITOR_DEFAULTTONEAREST = 2,
        }

        public static Rect GetWorkAreaForDisplayWithWindow(nint hwnd)
        {
            _foundMonitorIndex = -1;
            _monitorIndex = 0;
            var monitor = NativeMethods.MonitorFromWindow(hwnd, (int)MonitorFromWindowDwFlags.MONITOR_DEFAULTTONEAREST);
            NativeMethods.EnumDisplayMonitors(nint.Zero, nint.Zero, MonitorEnumProc, new NativeMethods.LPARAM(monitor));
            return MonitorInfo.GetDisplayMonitors()[_foundMonitorIndex].RectWork;
        }

        private static int _foundMonitorIndex = -1;
        private static int _monitorIndex;

        private static bool MonitorEnumProc(nint hMonitor, nint hdcMonitor, ref NativeMethods.RECT lprcMonitor, nint dwData)
        {
            nint targetMonitor = dwData;

            if (hMonitor == targetMonitor)
            {
                _foundMonitorIndex = _monitorIndex;
                return false;
            }

            _monitorIndex++;
            return true;
        }
    }
}
