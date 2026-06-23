// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace WorkspacesEditor.Views
{
    /// <summary>
    /// A thin opaque red bar used as one edge of a capture-mode border.
    /// Four instances per monitor form a complete border frame.
    /// </summary>
    public sealed partial class OverlayWindow : Window
    {
        private const int BorderThickness = 30;
        private static readonly IntPtr DpiAwarenessContextUnaware = new(-1);

        public OverlayWindow()
        {
            this.InitializeComponent();

            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
                presenter.IsAlwaysOnTop = true;
                presenter.SetBorderAndTitleBar(false, false);
            }
        }

        public void SetBounds(int x, int y, int width, int height)
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            IntPtr oldContext = SetThreadDpiAwarenessContext(DpiAwarenessContextUnaware);
            try
            {
                SetWindowPos(hwnd, IntPtr.Zero, x, y, width, height, SwpNoActivate);
            }
            finally
            {
                SetThreadDpiAwarenessContext(oldContext);
            }
        }

        /// <summary>
        /// Creates 4 thin border strip windows forming a red frame around the given screen bounds.
        /// </summary>
        public static OverlayWindow[] CreateBorderStrips(int x, int y, int width, int height)
        {
            // Debug: just one big window to test rendering
            var test = new OverlayWindow();
            test.Activate();
            test.SetBounds(x + 100, y + 100, 400, 400);

            return [test];
        }

        private const uint SwpNoActivate = 0x0010;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);
    }
}
