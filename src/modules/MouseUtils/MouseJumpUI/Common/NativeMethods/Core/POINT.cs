// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace MouseJumpUI.Common.NativeMethods;

internal static partial class Core
{
    /// <summary>
    /// The POINT structure defines the x- and y-coordinates of a point.
    /// </summary>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/api/windef/ns-windef-point
    /// </remarks>
    [SuppressMessage("SA1307", "SA1307:AccessibleFieldsMustBeginWithUpperCaseLetter", Justification = "Names match Win32 api")]
    internal readonly struct POINT
    {
        /// <summary>
        /// Specifies the x-coordinate of the point.
        /// </summary>
        public readonly LONG x;

        /// <summary>
        /// Specifies the y-coordinate of the point.
        /// </summary>
        public readonly LONG y;

        public POINT(
            int x,
            int y)
        {
            this.x = x;
            this.y = y;
        }

        public POINT(
            LONG x,
            LONG y)
        {
            this.x = x;
            this.y = y;
        }

        public static int Size =>
            Marshal.SizeOf(typeof(POINT));

        public override string ToString()
        {
            return $"x={this.x},y={this.y}";
        }
    }
}
