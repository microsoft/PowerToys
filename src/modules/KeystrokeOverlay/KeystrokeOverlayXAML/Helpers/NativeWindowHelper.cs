// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace KeystrokeOverlayUI.Helpers
{
    public static class NativeWindowHelper
    {
        // ---------------------------------------------------------
        // Public Structs
        // ---------------------------------------------------------
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        // ---------------------------------------------------------
        // Public Methods
        // ---------------------------------------------------------

        /// <summary>
        /// Retrieves the current cursor position in screen coordinates.
        /// </summary>
        public static POINT GetCursorPosition()
        {
            GetCursorPos(out POINT lpPoint);
            return lpPoint;
        }

        /// <summary>
        /// Sets the window style to be "ToolWindow" (hidden from Alt-Tab) and
        /// "NoActivate" (doesn't steal focus).
        /// </summary>
        public static void SetOverlayWindowStyles(IntPtr hWnd)
        {
            int exStyle = GetWindowLong(hWnd, GwlExStyle);
            _ = SetWindowLong(hWnd, GwlExStyle, exStyle | WsExNoActivate | WsExToolWindow);
        }

        /// <summary>
        /// Forces the window to stay on top of all other windows without stealing focus.
        /// </summary>
        public static void EnforceTopMost(IntPtr hWnd)
        {
            // SwpNoActivate is critical to ensure we don't steal focus while the user types
            SetWindowPos(hWnd, HwndTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpShowWindow | SwpNoActivate);
        }

        /// <summary>
        /// Applies Windows 11 rounded corners preference to the window.
        /// </summary>
        public static void SetRoundedCorners(IntPtr hWnd)
        {
            int cornerPreference = DwmwcpRound;
            _ = DwmSetWindowAttribute(hWnd, DwmwaWindowCornerPreference, ref cornerPreference, sizeof(int));
        }

        // ---------------------------------------------------------
        // Native Constants & Private Imports
        // ---------------------------------------------------------

        // Window Pos Flags
        private static readonly IntPtr HwndTopmost = new IntPtr(-1);
        private const uint SwpNoSize = 0x0001;
        private const uint SwpNoMove = 0x0002;
        private const uint SwpNoActivate = 0x0010;
        private const uint SwpShowWindow = 0x0040;

        // Window Styles
        private const int GwlExStyle = -20;
        private const int WsExNoActivate = 0x08000000;
        private const int WsExToolWindow = 0x00000080;

        // DWM Constants
        private const int DwmwaWindowCornerPreference = 33;
        private const int DwmwcpRound = 2;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    }
}
