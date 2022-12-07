// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.UI.Extensions
{
    using Microsoft.UI.Xaml;
    using Windows.Foundation;
    using Windows.Win32;
    using Windows.Win32.Foundation;
    using Windows.Win32.Graphics.Gdi;
    using WinUIEx;

    public static class WindowExtensions
    {
        public static Size GetMonitorSize(this Window window)
        {
            var hwnd = new HWND(window.GetWindowHandle());
            var hwndDesktop = PInvoke.MonitorFromWindow(hwnd, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
            MONITORINFO info = new ();
            info.cbSize = 40;
            PInvoke.GetMonitorInfo(hwndDesktop, ref info);
            double monitorWidth = info.rcMonitor.left + info.rcMonitor.right;
            double monitorHeight = info.rcMonitor.bottom + info.rcMonitor.top;

            return new Size(monitorWidth, monitorHeight);
        }

        public static double GetMonitorScale(this Window window)
        {
            var hwnd = new HWND(window.GetWindowHandle());
            var dpi = PInvoke.GetDpiForWindow(new HWND(hwnd));
            double scalingFactor = dpi / 96d;

            return scalingFactor;
        }
    }
}
