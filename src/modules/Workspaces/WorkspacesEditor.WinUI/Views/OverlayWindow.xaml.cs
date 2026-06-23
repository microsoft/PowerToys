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
    public sealed partial class OverlayWindow : Window
    {
        private readonly AppWindow _appWindow;

        public OverlayWindow()
        {
            this.InitializeComponent();

            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            // Remove title bar and make non-resizable
            if (_appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
                presenter.IsAlwaysOnTop = true;
                presenter.SetBorderAndTitleBar(false, false);
            }

            // Make window transparent (click-through) via Win32 extended styles
            MakeTransparent(hwnd);
        }

        public void SetBounds(int x, int y, int width, int height)
        {
            // Position using DPI-unaware context for correct multi-monitor placement
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

        private static void MakeTransparent(IntPtr hwnd)
        {
            int exStyle = GetWindowLong(hwnd, GwlExstyle);
            exStyle |= WsExLayered | WsExTransparent;
            _ = SetWindowLong(hwnd, GwlExstyle, exStyle);

            // Set full transparency with a colored key (the red border is drawn by XAML)
            SetLayeredWindowAttributes(hwnd, 0, 200, LwaAlpha);
        }

        // Win32 interop
        private static readonly IntPtr DpiAwarenessContextUnaware = new(-1);
        private const int GwlExstyle = -20;
        private const int WsExLayered = 0x00080000;
        private const int WsExTransparent = 0x00000020;
        private const uint LwaAlpha = 0x02;
        private const uint SwpNoActivate = 0x0010;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);
    }
}
