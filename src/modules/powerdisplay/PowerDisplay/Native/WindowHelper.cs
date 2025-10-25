// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using static PowerDisplay.Native.PInvoke;

namespace PowerDisplay.Native
{
    internal static class WindowHelper
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

        /// <summary>
        /// 禁用窗口的拖动和缩放功能
        /// </summary>
        public static void DisableWindowMovingAndResizing(IntPtr hWnd)
        {
            // 获取当前窗口样式
#if WIN64
            int style = (int)GetWindowLong(hWnd, GwlStyle);
#else
            int style = GetWindowLong(hWnd, GwlStyle);
#endif

            // 移除可调整大小的边框、标题栏和系统菜单
            style &= ~WsThickframe;
            style &= ~WsMaximizebox;
            style &= ~WsMinimizebox;
            style &= ~WsCaption;  // 移除整个标题栏
            style &= ~WsSysmenu;   // 移除系统菜单

            // 设置新的窗口样式
#if WIN64
            _ = SetWindowLong(hWnd, GwlStyle, new IntPtr(style));
#else
            _ = SetWindowLong(hWnd, GwlStyle, style);
#endif

            // 获取扩展样式并移除相关边框
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

            // 刷新窗口框架
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
        /// 设置窗口是否置顶
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
        /// 显示或隐藏窗口
        /// </summary>
        public static void ShowWindow(IntPtr hWnd, bool show)
        {
            PInvoke.ShowWindow(hWnd, show ? SwShow : SwHide);
        }

        /// <summary>
        /// 最小化窗口
        /// </summary>
        public static void MinimizeWindow(IntPtr hWnd)
        {
            PInvoke.ShowWindow(hWnd, SwMinimize);
        }

        /// <summary>
        /// 恢复窗口
        /// </summary>
        public static void RestoreWindow(IntPtr hWnd)
        {
            PInvoke.ShowWindow(hWnd, SwRestore);
        }

        /// <summary>
        /// 设置窗口不在任务栏显示
        /// </summary>
        public static void HideFromTaskbar(IntPtr hWnd)
        {
            // 获取当前扩展样式
#if WIN64
            int exStyle = (int)GetWindowLong(hWnd, GwlExstyle);
#else
            int exStyle = GetWindowLong(hWnd, GwlExstyle);
#endif

            // 添加 WS_EX_TOOLWINDOW 样式，这会让窗口不在任务栏显示
            exStyle |= WsExToolwindow;

            // 设置新的扩展样式
#if WIN64
            _ = SetWindowLong(hWnd, GwlExstyle, new IntPtr(exStyle));
#else
            _ = SetWindowLong(hWnd, GwlExstyle, exStyle);
#endif

            // 刷新窗口框架
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
