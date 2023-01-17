// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Windows.Foundation;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using WinUIEx;

namespace Peek.UI.Extensions
{
    public static class WindowExtensions
    {
        public static Size GetMonitorSize(this Window window)
        {
            var hwnd = new HWND(window.GetWindowHandle());
            var hwndDesktop = PInvoke.MonitorFromWindow(hwnd, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
            MONITORINFO info = default(MONITORINFO);
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

        public static void BringToForeground(this Window window)
        {
            var foregroundWindowHandle = PInvoke.GetForegroundWindow();

            uint targetProcessId = 0;
            uint windowThreadProcessId = 0;
            unsafe
            {
                windowThreadProcessId = PInvoke.GetWindowThreadProcessId(foregroundWindowHandle, &targetProcessId);
            }

            var windowHandle = window.GetWindowHandle();
            var currentThreadId = PInvoke.GetCurrentThreadId();
            PInvoke.AttachThreadInput(windowThreadProcessId, currentThreadId, true);
            PInvoke.BringWindowToTop(new HWND(windowHandle));
            PInvoke.ShowWindow(new HWND(windowHandle), Windows.Win32.UI.WindowsAndMessaging.SHOW_WINDOW_CMD.SW_SHOW);
            PInvoke.AttachThreadInput(windowThreadProcessId, currentThreadId, false);
        }
    }
}
