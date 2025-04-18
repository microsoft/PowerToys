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
    internal static class MouseHelper
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(int x, int y);

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
    }
}
