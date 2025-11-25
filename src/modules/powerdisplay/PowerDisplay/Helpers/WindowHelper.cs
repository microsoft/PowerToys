// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using WinUIEx;

namespace PowerDisplay.Helpers
{
    internal static partial class WindowHelper
    {
        // Window Styles
        private const int GwlStyle = -16;
        private const int WsCaption = 0x00C00000;
        private const int WsThickframe = 0x00040000;
        private const int WsMinimizebox = 0x00020000;
        private const int WsMaximizebox = 0x00010000;
        private const int WsSysmenu = 0x00080000;

        // Extended Window Styles
        private const int GwlExstyle = -20;
        private const int WsExDlgmodalframe = 0x00000001;
        private const int WsExWindowedge = 0x00000100;
        private const int WsExClientedge = 0x00000200;
        private const int WsExStaticedge = 0x00020000;
        private const int WsExToolwindow = 0x00000080;
        private const int WsExLayered = 0x00080000;
        private const int WsExTransparent = 0x00000020;

        // Layered window attributes
        private const int LwaColorkey = 0x00000001;
        private const int LwaAlpha = 0x00000002;

        // Window Messages
        private const int WmNclbuttondown = 0x00A1;
        private const int WmSyscommand = 0x0112;
        private const int ScMove = 0xF010;

        private const uint SwpNosize = 0x0001;
        private const uint SwpNomove = 0x0002;
        private const uint SwpFramechanged = 0x0020;
        private static readonly IntPtr HwndTopmost = new IntPtr(-1);
        private static readonly IntPtr HwndNotopmost = new IntPtr(-2);

        // ShowWindow commands
        private const int SwHide = 0;
        private const int SwShow = 5;
        private const int SwMinimize = 6;
        private const int SwRestore = 9;

        // P/Invoke declarations
#if WIN64
        [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static partial IntPtr GetWindowLong(IntPtr hWnd, int nIndex);
#else
        [LibraryImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static partial int GetWindowLong(IntPtr hWnd, int nIndex);
#endif

#if WIN64
        [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static partial IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
#else
        [LibraryImport("user32.dll", EntryPoint = "SetWindowLongW")]
        private static partial int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
#endif

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int x,
            int y,
            int cx,
            int cy,
            uint uFlags);

        [LibraryImport("user32.dll", EntryPoint = "ShowWindow")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool ShowWindowNative(IntPtr hWnd, int nCmdShow);

        [LibraryImport("user32.dll", EntryPoint = "IsWindowVisible")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool IsWindowVisibleNative(IntPtr hWnd);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);

        [LibraryImport("dwmapi.dll")]
        private static partial int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

        [StructLayout(LayoutKind.Sequential)]
        private struct MARGINS
        {
            public int Left;
            public int Right;
            public int Top;
            public int Bottom;
        }

        /// <summary>
        /// Check if window is visible
        /// </summary>
        public static bool IsWindowVisible(IntPtr hWnd)
        {
            return IsWindowVisibleNative(hWnd);
        }

        /// <summary>
        /// Disable window moving and resizing functionality
        /// </summary>
        public static void DisableWindowMovingAndResizing(IntPtr hWnd)
        {
            // Get current window style
#if WIN64
            int style = (int)GetWindowLong(hWnd, GwlStyle);
#else
            int style = GetWindowLong(hWnd, GwlStyle);
#endif

            // Remove resizable borders, title bar, and system menu
            style &= ~WsThickframe;
            style &= ~WsMaximizebox;
            style &= ~WsMinimizebox;
            style &= ~WsCaption;  // Remove entire title bar
            style &= ~WsSysmenu;   // Remove system menu

            // Set new window style
#if WIN64
            _ = SetWindowLong(hWnd, GwlStyle, new IntPtr(style));
#else
            _ = SetWindowLong(hWnd, GwlStyle, style);
#endif

            // Get extended style and remove related borders
#if WIN64
            int exStyle = (int)GetWindowLong(hWnd, GwlExstyle);
#else
            int exStyle = GetWindowLong(hWnd, GwlExstyle);
#endif
            exStyle &= ~WsExDlgmodalframe;
            exStyle &= ~WsExWindowedge;
            exStyle &= ~WsExClientedge;
            exStyle &= ~WsExStaticedge;
#if WIN64
            _ = SetWindowLong(hWnd, GwlExstyle, new IntPtr(exStyle));
#else
            _ = SetWindowLong(hWnd, GwlExstyle, exStyle);
#endif

            // Refresh window frame
            SetWindowPos(
                hWnd,
                IntPtr.Zero,
                0,
                0,
                0,
                0,
                SwpNomove | SwpNosize | SwpFramechanged);
        }

        /// <summary>
        /// Set whether window is topmost
        /// </summary>
        public static void SetWindowTopmost(IntPtr hWnd, bool topmost)
        {
            SetWindowPos(
                hWnd,
                topmost ? HwndTopmost : HwndNotopmost,
                0,
                0,
                0,
                0,
                SwpNomove | SwpNosize);
        }

        /// <summary>
        /// Show or hide window
        /// </summary>
        public static void ShowWindow(IntPtr hWnd, bool show)
        {
            ShowWindowNative(hWnd, show ? SwShow : SwHide);
        }

        /// <summary>
        /// Minimize window
        /// </summary>
        public static void MinimizeWindow(IntPtr hWnd)
        {
            ShowWindowNative(hWnd, SwMinimize);
        }

        /// <summary>
        /// Restore window
        /// </summary>
        public static void RestoreWindow(IntPtr hWnd)
        {
            ShowWindowNative(hWnd, SwRestore);
        }

        /// <summary>
        /// Hide window from taskbar
        /// </summary>
        public static void HideFromTaskbar(IntPtr hWnd)
        {
            // Get current extended style
#if WIN64
            int exStyle = (int)GetWindowLong(hWnd, GwlExstyle);
#else
            int exStyle = GetWindowLong(hWnd, GwlExstyle);
#endif

            // Add WS_EX_TOOLWINDOW style to hide window from taskbar
            exStyle |= WsExToolwindow;

            // Set new extended style
#if WIN64
            _ = SetWindowLong(hWnd, GwlExstyle, new IntPtr(exStyle));
#else
            _ = SetWindowLong(hWnd, GwlExstyle, exStyle);
#endif

            // Refresh window frame
            SetWindowPos(
                hWnd,
                IntPtr.Zero,
                0,
                0,
                0,
                0,
                SwpNomove | SwpNosize | SwpFramechanged);
        }

        /// <summary>
        /// Make window fully transparent using DWM glass effect
        /// </summary>
        /// <param name="hWnd">Window handle</param>
        public static void MakeWindowTransparent(IntPtr hWnd)
        {
            // Extend glass frame into entire client area for transparency
            var margins = new MARGINS { Left = -1, Right = -1, Top = -1, Bottom = -1 };
            DwmExtendFrameIntoClientArea(hWnd, ref margins);
        }

        /// <summary>
        /// Get the DPI scale factor for a window (relative to standard 96 DPI)
        /// </summary>
        /// <param name="window">WinUIEx window</param>
        /// <returns>DPI scale factor (1.0 = 100%, 1.25 = 125%, 1.5 = 150%, 2.0 = 200%)</returns>
        public static double GetDpiScale(WindowEx window)
        {
            return (float)window.GetDpiForWindow() / 96.0;
        }

        /// <summary>
        /// Convert device-independent units (DIU) to physical pixels
        /// </summary>
        /// <param name="diu">Device-independent unit value</param>
        /// <param name="dpiScale">DPI scale factor</param>
        /// <returns>Physical pixel value</returns>
        public static int ScaleToPhysicalPixels(int diu, double dpiScale)
        {
            return (int)Math.Ceiling(diu * dpiScale);
        }

        /// <summary>
        /// Position a window at the bottom-right corner of its display area
        /// </summary>
        /// <param name="window">WinUIEx window to position</param>
        /// <param name="width">Window width in device-independent units (DIU)</param>
        /// <param name="height">Window height in device-independent units (DIU)</param>
        /// <param name="rightMargin">Right margin in device-independent units (DIU)</param>
        public static void PositionWindowBottomRight(
            WindowEx window,
            int width,
            int height,
            int rightMargin = 0)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Nearest);

            if (displayArea == null)
            {
                return;
            }

            // Get DPI scale for this display
            double dpiScale = GetDpiScale(window);

            // Calculate position in physical pixels
            // WorkArea dimensions are in physical pixels, so we need to scale our DIU values
            double x = displayArea.WorkArea.Width - (dpiScale * (width + rightMargin));
            double y = displayArea.WorkArea.Height - (dpiScale * height);

            // MoveAndResize expects x,y in physical pixels and width,height in DIU
            window.MoveAndResize(x, y, width, height);
        }
    }
}
