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
            var monitor = NativeMethods.MonitorFromWindow(hwnd, (int)NativeMethods.MonitorFromWindowDwFlags.MONITOR_DEFAULTTONEAREST);

            int foundIndex = -1;
            int currentIndex = 0;

            NativeMethods.EnumDisplayMonitors(
                nint.Zero,
                nint.Zero,
                (nint hMonitor, nint hdcMonitor, ref NativeMethods.RECT lprcMonitor, nint dwData) =>
                {
                    if (hMonitor == dwData)
                    {
                        foundIndex = currentIndex;
                        return false;
                    }

                    currentIndex++;
                    return true;
                },
                monitor);

            var monitors = MonitorInfo.GetDisplayMonitors();
            if (foundIndex < 0 || foundIndex >= monitors.Count)
            {
                foundIndex = 0;
            }

            return monitors[foundIndex].RectWork;
        }
    }
}
