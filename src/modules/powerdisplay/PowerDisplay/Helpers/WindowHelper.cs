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

        private const uint SwpNosize = 0x0001;
        private const uint SwpNomove = 0x0002;
        private const uint SwpFramechanged = 0x0020;
        private const nint HwndTopmost = -1;
        private const nint HwndNotopmost = -2;

        // ShowWindow commands
        private const int SwHide = 0;
        private const int SwShow = 5;

        // P/Invoke declarations (64-bit only - PowerToys only builds for x64/ARM64)
        [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static partial nint GetWindowLong(nint hWnd, int nIndex);

        [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static partial nint SetWindowLong(nint hWnd, int nIndex, nint dwNewLong);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool SetWindowPos(
            nint hWnd,
            nint hWndInsertAfter,
            int x,
            int y,
            int cx,
            int cy,
            uint uFlags);

        [LibraryImport("user32.dll", EntryPoint = "ShowWindow")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool ShowWindowNative(nint hWnd, int nCmdShow);

        [LibraryImport("user32.dll", EntryPoint = "IsWindowVisible")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool IsWindowVisibleNative(nint hWnd);

        /// <summary>
        /// Check if window is visible
        /// </summary>
        public static bool IsWindowVisible(nint hWnd)
        {
            return IsWindowVisibleNative(hWnd);
        }

        /// <summary>
        /// Disable window moving and resizing functionality
        /// </summary>
        public static void DisableWindowMovingAndResizing(nint hWnd)
        {
            // Get current window style
            nint style = GetWindowLong(hWnd, GwlStyle);

            // Remove resizable borders, title bar, and system menu
            style &= ~WsThickframe;
            style &= ~WsMaximizebox;
            style &= ~WsMinimizebox;
            style &= ~WsCaption;  // Remove entire title bar
            style &= ~WsSysmenu;   // Remove system menu

            // Set new window style
            _ = SetWindowLong(hWnd, GwlStyle, style);

            // Get extended style and remove related borders
            nint exStyle = GetWindowLong(hWnd, GwlExstyle);
            exStyle &= ~WsExDlgmodalframe;
            exStyle &= ~WsExWindowedge;
            exStyle &= ~WsExClientedge;
            exStyle &= ~WsExStaticedge;
            _ = SetWindowLong(hWnd, GwlExstyle, exStyle);

            // Refresh window frame
            SetWindowPos(
                hWnd,
                0,
                0,
                0,
                0,
                0,
                SwpNomove | SwpNosize | SwpFramechanged);
        }

        /// <summary>
        /// Set whether window is topmost
        /// </summary>
        public static void SetWindowTopmost(nint hWnd, bool topmost)
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
        public static void ShowWindow(nint hWnd, bool show)
        {
            ShowWindowNative(hWnd, show ? SwShow : SwHide);
        }

        /// <summary>
        /// Hide window from taskbar
        /// </summary>
        public static void HideFromTaskbar(nint hWnd)
        {
            // Get current extended style
            nint exStyle = GetWindowLong(hWnd, GwlExstyle);

            // Add WS_EX_TOOLWINDOW style to hide window from taskbar
            exStyle |= WsExToolwindow;

            // Set new extended style
            _ = SetWindowLong(hWnd, GwlExstyle, exStyle);

            // Refresh window frame
            SetWindowPos(
                hWnd,
                0,
                0,
                0,
                0,
                0,
                SwpNomove | SwpNosize | SwpFramechanged);
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
