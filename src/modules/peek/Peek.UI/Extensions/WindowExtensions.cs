// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

        internal static void CenterOnMonitor(this Window window, HWND hwndDesktop, double? width = null, double? height = null)
        {
            var hwndToCenter = new HWND(window.GetWindowHandle());

            // If the window is maximized, restore to normal state before change its size
            var placement = default(WINDOWPLACEMENT);
            if (PInvoke_PeekUI.GetWindowPlacement(hwndToCenter, ref placement))
            {
                if (placement.showCmd == SHOW_WINDOW_CMD.SW_MAXIMIZE)
                {
                    placement.showCmd = SHOW_WINDOW_CMD.SW_SHOWNORMAL;
                    if (!PInvoke_PeekUI.SetWindowPlacement(hwndToCenter, in placement))
                    {
                        Logger.LogError($"SetWindowPlacement failed with error {Marshal.GetLastWin32Error()}");
                    }
                }
            }
            else
            {
                Logger.LogError($"GetWindowPlacement failed with error {Marshal.GetLastWin32Error()}");
            }

            var monitor = PInvoke_PeekUI.MonitorFromWindow(hwndDesktop, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
            MONITORINFO info = default(MONITORINFO);
            info.cbSize = 40;
            PInvoke_PeekUI.GetMonitorInfo(monitor, ref info);
            var dpi = PInvoke_PeekUI.GetDpiForWindow(new HWND((nint)hwndDesktop));
            PInvoke_PeekUI.GetWindowRect(hwndToCenter, out RECT windowRect);
            var scalingFactor = dpi / 96d;
            var w = width.HasValue ? (int)(width * scalingFactor) : windowRect.right - windowRect.left;
            var h = height.HasValue ? (int)(height * scalingFactor) : windowRect.bottom - windowRect.top;
            var cx = (info.rcMonitor.left + info.rcMonitor.right) / 2;
            var cy = (info.rcMonitor.bottom + info.rcMonitor.top) / 2;
            var left = cx - (w / 2);
            var top = cy - (h / 2);

            SetWindowPosOrThrow(hwndToCenter, default(HWND), left, top, w, h, SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
        }

        private static void SetWindowPosOrThrow(HWND hWnd, HWND hWndInsertAfter, int x, int y, int cx, int cy, SET_WINDOW_POS_FLAGS uFlags)
        {
            bool result = PInvoke_PeekUI.SetWindowPos(hWnd, hWndInsertAfter, x, y, cx, cy, uFlags);
            if (!result)
            {
                Marshal.ThrowExceptionForHR(Marshal.GetLastWin32Error());
            }
        }
    }
}
