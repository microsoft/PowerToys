// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

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
    }
}
