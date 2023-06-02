// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using ManagedCommon;
using Microsoft.UI.Xaml;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;
using WinUIEx;

namespace Peek.UI.Extensions
{
    public static class WindowExtensions
    {
        public static double GetMonitorScale(this Window window)
        {
            var hwnd = new HWND(window.GetWindowHandle());
            return hwnd.GetMonitorScale();
        }

        public static void BringToForeground(this Window window)
        {
            // Ability to be able to set the Window to the Foreground is very limited. A current workaround is simulating mouse input before bringing to the foreground.
            var windowHandle = window.GetWindowHandle();
            Windows.Win32.UI.Input.KeyboardAndMouse.INPUT input = new Windows.Win32.UI.Input.KeyboardAndMouse.INPUT { type = Windows.Win32.UI.Input.KeyboardAndMouse.INPUT_TYPE.INPUT_MOUSE, Anonymous = { } };
            Windows.Win32.UI.Input.KeyboardAndMouse.INPUT[] inputs = new Windows.Win32.UI.Input.KeyboardAndMouse.INPUT[] { input };
            _ = PInvoke.SendInput(inputs.AsSpan<Windows.Win32.UI.Input.KeyboardAndMouse.INPUT>(), Marshal.SizeOf(typeof(Windows.Win32.UI.Input.KeyboardAndMouse.INPUT)));
            if (PInvoke.SetForegroundWindow(new HWND(windowHandle)) == 0)
            {
                Logger.LogWarning("Couldn't set the Peek window as the foreground window.");
            }
        }

        internal static void CenterOnMonitor(this Window window, HWND hwndDesktop, double? width = null, double? height = null)
        {
            var hwndToCenter = new HWND(window.GetWindowHandle());
            var monitor = PInvoke.MonitorFromWindow(hwndDesktop, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
            MONITORINFO info = default(MONITORINFO);
            info.cbSize = 40;
            PInvoke.GetMonitorInfo(monitor, ref info);
            var dpi = PInvoke.GetDpiForWindow(new HWND(hwndDesktop));
            PInvoke.GetWindowRect(new HWND(hwndToCenter), out RECT windowRect);
            var scalingFactor = dpi / 96d;
            var w = width.HasValue ? (int)(width * scalingFactor) : windowRect.right - windowRect.left;
            var h = height.HasValue ? (int)(height * scalingFactor) : windowRect.bottom - windowRect.top;
            var cx = (info.rcMonitor.left + info.rcMonitor.right) / 2;
            var cy = (info.rcMonitor.bottom + info.rcMonitor.top) / 2;
            var left = cx - (w / 2);
            var top = cy - (h / 2);
            SetWindowPosOrThrow(new HWND(hwndToCenter), default(HWND), left, top, w, h, SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW);
        }

        private static void SetWindowPosOrThrow(HWND hWnd, HWND hWndInsertAfter, int x, int y, int cx, int cy, SET_WINDOW_POS_FLAGS uFlags)
        {
            bool result = PInvoke.SetWindowPos(hWnd, hWndInsertAfter, x, y, cx, cy, uFlags);
            if (!result)
            {
                Marshal.ThrowExceptionForHR(Marshal.GetLastWin32Error());
            }
        }
    }
}
