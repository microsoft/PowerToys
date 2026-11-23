// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

using Windows.Graphics;
using WinRT.Interop;

namespace WorkspacesEditor.Views
{
    /// <summary>
    /// Creates 4 thin opaque red bar windows forming a border frame around a display area.
    /// Click-through so the user can interact with their desktop beneath.
    /// </summary>
    internal sealed class OverlayBorder : IDisposable
    {
        private const int BorderThickness = 6;
        private readonly List<Window> _windows = new();

        /// <summary>
        /// Gets the bounds of all monitors via Win32 EnumDisplayMonitors.
        /// </summary>
        public static List<RectInt32> GetAllMonitorBounds()
        {
            var monitors = new List<RectInt32>();
            EnumDisplayMonitors(
                IntPtr.Zero,
                IntPtr.Zero,
                (IntPtr hMonitor, IntPtr hdc, ref Rect lprcMonitor, IntPtr dwData) =>
                {
                    monitors.Add(new RectInt32(
                        lprcMonitor.Left,
                        lprcMonitor.Top,
                        lprcMonitor.Right - lprcMonitor.Left,
                        lprcMonitor.Bottom - lprcMonitor.Top));
                    return true;
                },
                IntPtr.Zero);
            return monitors;
        }

        /// <summary>
        /// Creates overlay borders around all monitors.
        /// </summary>
        public static OverlayBorder CreateForAllMonitors(IEnumerable<RectInt32> monitorBounds)
        {
            var overlay = new OverlayBorder();
            foreach (var bounds in monitorBounds)
            {
                overlay.CreateBorderForRect(bounds);
            }

            return overlay;
        }

        /// <summary>
        /// Creates 4 strip windows (top, bottom, left, right) forming a red frame.
        /// All bars extend to full length so corners connect cleanly.
        /// </summary>
        private void CreateBorderForRect(RectInt32 bounds)
        {
            // Top bar — full width
            CreateStrip(bounds.X, bounds.Y, bounds.Width, BorderThickness);

            // Bottom bar — full width
            CreateStrip(bounds.X, bounds.Y + bounds.Height - BorderThickness, bounds.Width, BorderThickness);

            // Left bar — full height (overlaps corners)
            CreateStrip(bounds.X, bounds.Y, BorderThickness, bounds.Height);

            // Right bar — full height (overlaps corners)
            CreateStrip(bounds.X + bounds.Width - BorderThickness, bounds.Y, BorderThickness, bounds.Height);
        }

        private void CreateStrip(int x, int y, int width, int height)
        {
            var window = new Window();
            window.Content = new Microsoft.UI.Xaml.Controls.Grid
            {
                Background = new SolidColorBrush(Microsoft.UI.Colors.Red),
            };

            // Get native handle and configure
            var hwnd = WindowNative.GetWindowHandle(window);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            // Remove title bar and borders
            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsAlwaysOnTop = true;
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
                presenter.SetBorderAndTitleBar(false, false);
            }

            // Disable DWM shadow/gradient and window chrome completely
            int ncrpDisabled = 2; // DWMNCRP_DISABLED
            _ = DwmSetWindowAttribute(hwnd, 2, ref ncrpDisabled, sizeof(int)); // DWMWA_NCRENDERING_POLICY

            // Remove rounded corners (Windows 11)
            int cornerPref = 1; // DWMWCP_DONOTROUND
            _ = DwmSetWindowAttribute(hwnd, 33, ref cornerPref, sizeof(int)); // DWMWA_WINDOW_CORNER_PREFERENCE

            // Remove window border color
            int colorNone = unchecked((int)0xFFFFFFFE); // DWMWA_COLOR_NONE
            _ = DwmSetWindowAttribute(hwnd, 34, ref colorNone, sizeof(int)); // DWMWA_BORDER_COLOR

            // Disable shadow
            var margins = new Margins { Left = 0, Right = 0, Top = 0, Bottom = 0 };
            _ = DwmExtendFrameIntoClientArea(hwnd, ref margins);

            // Remove WS_OVERLAPPEDWINDOW style, set WS_POPUP for minimal chrome
            int style = GetWindowLong(hwnd, GwlStyle);
            style &= ~WsOverlappedwindow;
            style |= WsPopup;
            _ = SetWindowLong(hwnd, GwlStyle, style);

            // Make click-through + no taskbar entry
            int exStyle = GetWindowLong(hwnd, GwlExstyle);
            _ = SetWindowLong(hwnd, GwlExstyle, exStyle | WsExTransparent | WsExToolwindow | WsExTopmost);

            // Position and size via SetWindowPos (bypasses AppWindow min-size constraints)
            _ = SetWindowPos(hwnd, HwndTopmost, x, y, width, height, SwpNoactivate | SwpShowwindow);

            // Show
            window.Activate();

            _windows.Add(window);
        }

        public void Dispose()
        {
            foreach (var window in _windows)
            {
                try
                {
                    window.Close();
                }
                catch
                {
                }
            }

            _windows.Clear();
        }

        // Win32 interop
        private const int GwlStyle = -16;
        private const int GwlExstyle = -20;
        private const int WsOverlappedwindow = 0x00CF0000;
        private const int WsPopup = unchecked((int)0x80000000);
        private const int WsExTransparent = 0x00000020;
        private const int WsExToolwindow = 0x00000080;
        private const int WsExTopmost = 0x00000008;
        private const int SwpNoactivate = 0x0010;
        private const int SwpShowwindow = 0x0040;
        private static readonly IntPtr HwndTopmost = new IntPtr(-1);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);

        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref Rect lprcMonitor, IntPtr dwData);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Margins
        {
            public int Left;
            public int Right;
            public int Top;
            public int Bottom;
        }
    }
}
