// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerToys.UITest
{
    public enum MouseActionType
    {
        LeftClick,
        RightClick,
        MiddleClick,
        LeftDoubleClick,
        RightDoubleClick,
        LeftDown,
        LeftUp,
        RightDown,
        RightUp,
        MiddleDown,
        MiddleUp,
        ScrollUp,
        ScrollDown,
    }

    internal static class MouseHelper
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

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

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
#pragma warning disable SA1300 // Element should begin with upper-case letter
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
#pragma warning restore SA1300 // Element should begin with upper-case letter

        /// <summary>
        /// Gets the current position of the mouse cursor as a tuple.
        /// </summary>
        /// <returns>A tuple containing the X and Y coordinates of the cursor.</returns>
        public static Tuple<int, int> GetMousePosition()
        {
            GetCursorPos(out POINT point);
            return Tuple.Create(point.X, point.Y);
        }

        /// <summary>
        /// Moves the mouse cursor to the specified screen coordinates.
        /// </summary>
        /// <param name="x">The new x-coordinate of the cursor.</param>
        /// <param name="y">The new y-coordinate of the cursor.</param
        public static void MoveMouseTo(int x, int y)
        {
            SetCursorPos(x, y);
        }

        /// <summary>
        /// The delay in milliseconds between mouse down and up events to simulate a click.
        /// </summary>
        private const int ClickDelay = 100;

        /// <summary>
        /// The amount of scroll units to simulate a single mouse wheel tick.
        /// </summary>
        private const int ScrollAmount = 120;

        /// <summary>
        /// Simulates a left mouse click (press and release).
        /// </summary>
        public static void LeftClick()
        {
            LeftDown();
            Thread.Sleep(ClickDelay);
            LeftUp();
        }

        /// <summary>
        /// Simulates a right mouse click (press and release).
        /// </summary>
        public static void RightClick()
        {
            RightDown();
            Thread.Sleep(ClickDelay);
            RightUp();
        }

        /// <summary>
        /// Simulates a middle mouse click (press and release).
        /// </summary>
        public static void MiddleClick()
        {
            MiddleDown();
            Thread.Sleep(ClickDelay);
            MiddleUp();
        }

        /// <summary>
        /// Simulates a left mouse double-click.
        /// </summary>
        public static void LeftDoubleClick()
        {
            LeftClick();
            Thread.Sleep(ClickDelay);
            LeftClick();
        }

        /// <summary>
        /// Simulates a right mouse double-click.
        /// </summary>
        public static void RightDoubleClick()
        {
            RightClick();
            Thread.Sleep(ClickDelay);
            RightClick();
        }

        /// <summary>
        /// Simulates pressing the left mouse button down.
        /// </summary>
        public static void LeftDown()
        {
            mouse_event((uint)MouseEvent.LeftDown, 0, 0, 0, UIntPtr.Zero);
        }

        /// <summary>
        /// Simulates pressing the right mouse button down.
        /// </summary>
        public static void RightDown()
        {
            mouse_event((uint)MouseEvent.RightDown, 0, 0, 0, UIntPtr.Zero);
        }

        /// <summary>
        /// Simulates pressing the middle mouse button down.
        /// </summary>
        public static void MiddleDown()
        {
            mouse_event((uint)MouseEvent.MiddleDown, 0, 0, 0, UIntPtr.Zero);
        }

        /// <summary>
        /// Simulates releasing the left mouse button.
        /// </summary>
        public static void LeftUp()
        {
            mouse_event((uint)MouseEvent.LeftUp, 0, 0, 0, UIntPtr.Zero);
        }

        /// <summary>
        /// Simulates releasing the right mouse button.
        /// </summary>
        public static void RightUp()
        {
            mouse_event((uint)MouseEvent.RightUp, 0, 0, 0, UIntPtr.Zero);
        }

        /// <summary>
        /// Simulates releasing the middle mouse button.
        /// </summary>
        public static void MiddleUp()
        {
            mouse_event((uint)MouseEvent.MiddleUp, 0, 0, 0, UIntPtr.Zero);
        }

        /// <summary>
        /// Simulates a mouse scroll wheel action by a specified amount.
        /// Positive values scroll up, negative values scroll down.
        /// </summary>
        /// <param name="amount">The scroll amount. Typically 120 or -120 per tick.</param>
        public static void ScrollWheel(int amount)
        {
            mouse_event((uint)MouseEvent.Wheel, 0, 0, (uint)amount, UIntPtr.Zero);
        }

        /// <summary>
        /// Simulates scrolling the mouse wheel up by one tick.
        /// </summary>
        public static void ScrollUp()
        {
            ScrollWheel(ScrollAmount);
        }

        /// <summary>
        /// Simulates scrolling the mouse wheel down by one tick.
        /// </summary>
        public static void ScrollDown()
        {
            ScrollWheel(-ScrollAmount);
        }
    }
}
