// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using PowerDisplay.Common.Drivers;
using WinUIEx;

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

        [LibraryImport("user32.dll")]
        private static partial nint MonitorFromPoint(POINT pt, uint dwFlags);

        [LibraryImport("shcore.dll")]
        private static partial int GetDpiForMonitor(nint hMonitor, uint dpiType, out uint dpiX, out uint dpiY);

        [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW", StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GetMonitorInfo(nint hMonitor, ref MonitorInfoEx lpmi);

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
        private const uint SwpNosize = 0x0001;
        private const uint SwpNomove = 0x0002;
        private const uint SwpFramechanged = 0x0020;
        private const uint MonitorDefaultToNearest = 2;
        private const uint MdtEffectiveDpi = 0;
        private const int DefaultDpi = 96;

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
        /// Get the DPI scale factor for a window (relative to standard 96 DPI)
        /// </summary>
        /// <param name="window">WinUIEx window</param>
        /// <returns>DPI scale factor (1.0 = 100%, 1.25 = 125%, 1.5 = 150%, 2.0 = 200%)</returns>
        public static double GetDpiScale(WindowEx window)
        {
            return (double)window.GetDpiForWindow() / DefaultDpi;
        }

        /// <summary>
        /// Get the DPI scale factor for a display area (relative to standard 96 DPI)
        /// </summary>
        /// <param name="displayArea">Target display area</param>
        /// <returns>DPI scale factor (1.0 = 100%, 1.25 = 125%, 1.5 = 150%, 2.0 = 200%)</returns>
        public static double GetDpiScale(DisplayArea displayArea)
        {
            return (double)GetEffectiveDpi(global::Microsoft.UI.Win32Interop.GetMonitorFromDisplayId(displayArea.DisplayId)) / DefaultDpi;
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
        /// Position a window at the bottom-right corner of the monitor where the mouse cursor is located.
        /// Correctly handles all edge cases:
        /// - Multi-monitor setups
        /// - Taskbar at any position (top/bottom/left/right)
        /// - Different DPI settings
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
            if (TryGetMonitorAtCursor(out var monitorInfo, out var dpi))
            {
                MoveWindowBottomRight(window, monitorInfo.RcWork, dpi, width, height, rightMargin);
                return;
            }

            var displayArea = DisplayArea.GetFromWindowId(window.AppWindow.Id, DisplayAreaFallback.Primary);
            if (displayArea is null)
            {
                ManagedCommon.Logger.LogWarning("PositionWindowBottomRight: Unable to determine target display, skipping positioning");
                return;
            }

            var workArea = displayArea.WorkArea;
            var fallbackRect = new Rect(workArea.X, workArea.Y, workArea.X + workArea.Width, workArea.Y + workArea.Height);
            MoveWindowBottomRight(window, fallbackRect, GetEffectiveDpi(global::Microsoft.UI.Win32Interop.GetMonitorFromDisplayId(displayArea.DisplayId)), width, height, rightMargin);
        }

        private static void MoveWindowBottomRight(
            WindowEx window,
            Rect workArea,
            int dpi,
            int width,
            int height,
            int rightMargin)
        {
            double dpiScale = (double)dpi / DefaultDpi;
            int physicalWidth = ScaleToPhysicalPixels(width, dpiScale);
            int physicalHeight = ScaleToPhysicalPixels(height, dpiScale);
            int physicalX = workArea.Right - ScaleToPhysicalPixels(width + rightMargin, dpiScale);
            int physicalY = workArea.Bottom - physicalHeight;

            window.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(physicalX, physicalY, physicalWidth, physicalHeight));
        }

        private static unsafe bool TryGetMonitorAtCursor(out MonitorInfoEx monitorInfo, out int dpi)
        {
            monitorInfo = default;
            dpi = DefaultDpi;

            if (!GetCursorPos(out var cursorPos))
            {
                return false;
            }

            var hMonitor = MonitorFromPoint(cursorPos, MonitorDefaultToNearest);
            if (hMonitor == 0)
            {
                return false;
            }

            monitorInfo = new MonitorInfoEx
            {
                CbSize = (uint)sizeof(MonitorInfoEx),
            };

            if (!GetMonitorInfo(hMonitor, ref monitorInfo))
            {
                return false;
            }

            dpi = GetEffectiveDpi(hMonitor);
            return true;
        }

        private static int GetEffectiveDpi(nint hMonitor)
        {
            if (hMonitor == 0)
            {
                return DefaultDpi;
            }

            var hr = GetDpiForMonitor(hMonitor, MdtEffectiveDpi, out var dpiX, out _);
            return hr >= 0 && dpiX > 0 ? (int)dpiX : DefaultDpi;
        }
    }
}
