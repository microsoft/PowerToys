// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinRT.Interop;

namespace PowerDisplay.Helpers
{
    internal static partial class WindowHelper
    {
        // Cursor position structure for GetCursorPos
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        // Cursor position for detecting the monitor with the mouse
        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GetCursorPos(out POINT lpPoint);

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
        private static partial nint GetWindowLongPtr(nint hWnd, int nIndex);

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

        [LibraryImport("user32.dll", EntryPoint = "GetDpiForWindow")]
        private static partial uint GetDpiForWindowNative(nint hWnd);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool SetForegroundWindow(nint hWnd);

        // DWM Window Cloaking - hides window at compositor level while keeping it fully functional
        private const int DwmwaCloak = 13;

        [LibraryImport("dwmapi.dll", EntryPoint = "DwmSetWindowAttribute")]
        private static partial int DwmSetWindowAttributeNative(nint hwnd, int attr, ref int attrValue, int attrSize);

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
            nint style = GetWindowLongPtr(hWnd, GwlStyle);

            // Remove resizable borders, title bar, and system menu
            style &= ~WsThickframe;
            style &= ~WsMaximizebox;
            style &= ~WsMinimizebox;
            style &= ~WsCaption;  // Remove entire title bar
            style &= ~WsSysmenu;   // Remove system menu

            // Set new window style
            _ = SetWindowLong(hWnd, GwlStyle, style);

            // Get extended style and remove related borders
            nint exStyle = GetWindowLongPtr(hWnd, GwlExstyle);
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
            nint exStyle = GetWindowLongPtr(hWnd, GwlExstyle);

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
        /// Configure a window as a borderless popup (no title bar, no taskbar entry,
        /// not resizable/maximizable/minimizable). Both MainWindow and IdentifyWindow
        /// share this base configuration.
        /// </summary>
        /// <param name="window">WinUI window to configure</param>
        /// <param name="hWnd">Window handle</param>
        /// <param name="alwaysOnTop">Whether the window should stay on top of other windows</param>
        public static void ConfigureAsPopupWindow(Window window, nint hWnd, bool alwaysOnTop = false)
        {
            // Disable maximize, minimize, resize
            if (window.AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
                presenter.IsResizable = false;
                if (alwaysOnTop)
                {
                    presenter.IsAlwaysOnTop = true;
                }
            }

            // Hide from Alt+Tab / taskbar
            HideFromTaskbar(hWnd);

            // Collapse title bar completely
            var titleBar = window.AppWindow.TitleBar;
            if (titleBar != null)
            {
                titleBar.ExtendsContentIntoTitleBar = true;
                titleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;
            }
        }

        /// <summary>
        /// Get the DPI value for a window
        /// </summary>
        /// <param name="window">WinUI window</param>
        /// <returns>DPI value (96 = 100%, 120 = 125%, 144 = 150%, 192 = 200%)</returns>
        public static uint GetDpiForWindow(Window window)
        {
            var hWnd = WindowNative.GetWindowHandle(window);
            return GetDpiForWindowNative(hWnd);
        }

        /// <summary>
        /// Get the DPI scale factor for a window (relative to standard 96 DPI)
        /// </summary>
        /// <param name="window">WinUI window</param>
        /// <returns>DPI scale factor (1.0 = 100%, 1.25 = 125%, 1.5 = 150%, 2.0 = 200%)</returns>
        public static double GetDpiScale(Window window)
        {
            return GetDpiForWindow(window) / 96.0;
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
        /// Bring a window to the foreground
        /// </summary>
        public static void BringToFront(nint hWnd)
        {
            SetForegroundWindow(hWnd);
        }

        /// <summary>
        /// Cloak a window using DWM. The window is invisible at the compositor level
        /// but remains fully functional (processes messages, layout, DPI changes).
        /// </summary>
        /// <returns>True if cloaking succeeded</returns>
        public static bool CloakWindow(nint hWnd)
        {
            int cloak = 1;
            return DwmSetWindowAttributeNative(hWnd, DwmwaCloak, ref cloak, sizeof(int)) == 0;
        }

        /// <summary>
        /// Uncloak a previously cloaked window, making it visible again.
        /// </summary>
        /// <returns>True if uncloaking succeeded</returns>
        public static bool UncloakWindow(nint hWnd)
        {
            int cloak = 0;
            return DwmSetWindowAttributeNative(hWnd, DwmwaCloak, ref cloak, sizeof(int)) == 0;
        }

        /// <summary>
        /// Position a window at the bottom-right corner of the monitor where the mouse cursor is located.
        /// Correctly handles all edge cases:
        /// - Multi-monitor setups
        /// - Taskbar at any position (top/bottom/left/right)
        /// - Different DPI settings
        /// </summary>
        /// <param name="window">WinUI window to position</param>
        /// <param name="width">Window width in device-independent units (DIU)</param>
        /// <param name="height">Window height in device-independent units (DIU)</param>
        /// <param name="rightMargin">Right margin in device-independent units (DIU)</param>
        public static void PositionWindowBottomRight(
            Window window,
            int width,
            int height,
            int rightMargin = 0)
        {
            var displayAreas = DisplayArea.FindAll();
            if (displayAreas == null || displayAreas.Count == 0)
            {
                ManagedCommon.Logger.LogWarning("PositionWindowBottomRight: No display areas found, skipping positioning");
                return;
            }

            // Find the display area where the mouse cursor is located
            var targetArea = GetDisplayAreaAtCursor(displayAreas);
            var workArea = targetArea.WorkArea;

            // Only move to the target monitor if the window is not already on it.
            // This avoids a visible jump to the monitor center on every resize.
            // The move is needed for cross-monitor DPI transitions: without it,
            // GetDpiScale returns the wrong DPI and WM_DPICHANGED auto-scaling
            // breaks the bottom-right alignment.
            var windowPos = window.AppWindow.Position;
            if (windowPos.X < workArea.X || windowPos.X >= workArea.X + workArea.Width ||
                windowPos.Y < workArea.Y || windowPos.Y >= workArea.Y + workArea.Height)
            {
                window.AppWindow.Move(new PointInt32(
                    workArea.X + (workArea.Width / 2),
                    workArea.Y + (workArea.Height / 2)));
            }

            // Now the window is on the target monitor, DPI is correct.
            // No DPI change will occur during MoveAndResize, so no auto-scaling.
            double dpiScale = GetDpiScale(window);

            // Convert DIU to physical pixels
            int physWidth = (int)Math.Ceiling(width * dpiScale);
            int physHeight = (int)Math.Ceiling(height * dpiScale);
            int physMargin = (int)Math.Ceiling(rightMargin * dpiScale);

            // Calculate bottom-right position in physical pixels
            // WorkArea already accounts for taskbar position
            int x = workArea.X + workArea.Width - physWidth - physMargin;
            int y = workArea.Y + workArea.Height - physHeight;

            window.AppWindow.MoveAndResize(new RectInt32(x, y, physWidth, physHeight));
        }

        /// <summary>
        /// Get the display area where the mouse cursor is currently located.
        /// Falls back to primary display area if cursor position cannot be determined.
        /// </summary>
        /// <param name="displayAreas">List of available display areas</param>
        /// <returns>DisplayArea containing the cursor</returns>
        private static DisplayArea GetDisplayAreaAtCursor(IReadOnlyList<DisplayArea> displayAreas)
        {
            // Try to get cursor position using Win32 API
            if (GetCursorPos(out var cursorPos))
            {
                // Find the display area that contains the cursor point
                foreach (var area in displayAreas)
                {
                    var bounds = area.OuterBounds;
                    if (cursorPos.X >= bounds.X &&
                        cursorPos.X < bounds.X + bounds.Width &&
                        cursorPos.Y >= bounds.Y &&
                        cursorPos.Y < bounds.Y + bounds.Height)
                    {
                        return area;
                    }
                }
            }

            // Fallback to first display area (typically primary)
            return displayAreas[0];
        }
    }
}
