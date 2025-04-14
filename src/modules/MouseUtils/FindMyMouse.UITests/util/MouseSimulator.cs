// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace FindMyMouse.UITests
{
    public class MouseSimulator
    {
        // 引入 mouse_event API
        [DllImport("user32.dll")]
#pragma warning disable SA1300 // Element should begin with upper-case letter
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
#pragma warning restore SA1300 // Element should begin with upper-case letter

        // 鼠标事件常量定义
        [Flags]
        internal enum MouseEvent
        {
            LeftDown = 0x0002,
            LeftUp = 0x0004,
            RightDown = 0x0008,
            RightUp = 0x0010,
            MiddleDown = 0x0020,
            MiddleUp = 0x0040,
            Wheel = 0x0800,
        }

        // 模拟点击间隔
        private const int ClickDelay = 100;

        // --- 单击事件 ---
        public static void LeftClick()
        {
            LeftDown();
            Thread.Sleep(ClickDelay);
            LeftUp();
        }

        public static void RightClick()
        {
            RightDown();
            Thread.Sleep(ClickDelay);
            RightUp();
        }

        public static void MiddleClick()
        {
            MiddleDown();
            Thread.Sleep(ClickDelay);
            MiddleUp();
        }

        // --- 双击事件 ---
        public static void LeftDoubleClick()
        {
            LeftClick();
            Thread.Sleep(ClickDelay);
            LeftClick();
        }

        public static void RightDoubleClick()
        {
            RightClick();
            Thread.Sleep(ClickDelay);
            RightClick();
        }

        // --- 按下 ---
        public static void LeftDown()
        {
            mouse_event((uint)MouseEvent.LeftDown, 0, 0, 0, UIntPtr.Zero);
        }

        public static void RightDown()
        {
            mouse_event((uint)MouseEvent.RightDown, 0, 0, 0, UIntPtr.Zero);
        }

        public static void MiddleDown()
        {
            mouse_event((uint)MouseEvent.MiddleDown, 0, 0, 0, UIntPtr.Zero);
        }

        // --- 抬起 ---
        public static void LeftUp()
        {
            mouse_event((uint)MouseEvent.LeftUp, 0, 0, 0, UIntPtr.Zero);
        }

        public static void RightUp()
        {
            mouse_event((uint)MouseEvent.RightUp, 0, 0, 0, UIntPtr.Zero);
        }

        public static void MiddleUp()
        {
            mouse_event((uint)MouseEvent.MiddleUp, 0, 0, 0, UIntPtr.Zero);
        }

        public static void ScrollWheel(int amount)
        {
            mouse_event((uint)MouseEvent.Wheel, 0, 0, (uint)amount, UIntPtr.Zero);
        }

        public static void ScrollUp()
        {
            ScrollWheel(120);
        }

        public static void ScrollDown()
        {
            ScrollWheel(-120);
        }
    }
}
