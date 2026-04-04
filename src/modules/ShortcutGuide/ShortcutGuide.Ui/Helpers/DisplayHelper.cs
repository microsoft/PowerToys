// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation;
using WinUIEx;

namespace ShortcutGuide.Helpers
{
    public static class DisplayHelper
    {
        /// <summary>
        /// Returns the display work area for the monitor that contains the specified window.
        /// </summary>
        /// <param name="hwnd">The window handle</param>
        /// <returns>A <see cref="Rect"/> element containing the display area</returns>
        public static Rect GetWorkAreaForDisplayWithWindow(nint hwnd)
        {
            _foundMonitorIndex = -1;
            _monitorIndex = 0;
            var monitor = NativeMethods.MonitorFromWindow(hwnd, (int)NativeMethods.MonitorFromWindowDwFlags.MONITOR_DEFAULTTONEAREST);
            NativeMethods.EnumDisplayMonitors(nint.Zero, nint.Zero, MonitorEnumProc, new NativeMethods.LPARAM(monitor));
            return MonitorInfo.GetDisplayMonitors()[_foundMonitorIndex].RectWork;
        }

        /// <summary>
        /// The index of the monitor that contains the specified window. -1 indicates that no monitor was found (yet).
        /// </summary>
        private static int _foundMonitorIndex = -1;

        /// <summary>
        /// The index of the monitor in the enumeration. This is used to find the correct monitor in the list of monitors.
        /// </summary>
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
