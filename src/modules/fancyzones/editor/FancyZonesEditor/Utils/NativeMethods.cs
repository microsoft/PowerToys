// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace FancyZonesEditor.Utils
{
    internal sealed class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

        private const int GWL_EX_STYLE = -20;
        private const int WS_EX_APPWINDOW = 0x00040000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;

        private static readonly IntPtr DPI_AWARENESS_CONTEXT_UNAWARE = new IntPtr(-1);

        public static void SetWindowStyleToolWindow(Window hwnd)
        {
            var helper = new WindowInteropHelper(hwnd).Handle;
            _ = SetWindowLong(helper, GWL_EX_STYLE, (GetWindowLong(helper, GWL_EX_STYLE) | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW);
        }

        /// <summary>
        /// Positions a WPF window using DPI-unaware context to match the virtual coordinates
        /// from the FancyZones C++ backend (which uses a DPI-unaware thread).
        /// This fixes overlay positioning on mixed-DPI multi-monitor setups.
        /// </summary>
        public static void SetWindowPositionDpiUnaware(Window window, int x, int y, int width, int height)
        {
            var helper = new WindowInteropHelper(window).Handle;
            if (helper != IntPtr.Zero)
            {
                // Temporarily switch to DPI-unaware context to position window.
                // This matches how the C++ backend gets coordinates via dpiUnawareThread.
                IntPtr oldContext = SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_UNAWARE);
                try
                {
                    SetWindowPos(helper, IntPtr.Zero, x, y, width, height, SWP_NOZORDER | SWP_NOACTIVATE);
                }
                finally
                {
                    SetThreadDpiAwarenessContext(oldContext);
                }
            }
        }
    }
}
