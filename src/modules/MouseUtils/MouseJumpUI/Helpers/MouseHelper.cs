// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Windows.Forms;
using MouseJumpUI.Drawing.Models;

namespace MouseJumpUI.Helpers;

internal static class MouseHelper
{
    /// <summary>
    /// Calculates where to move the cursor to by projecting a point from
    /// the preview image onto the desktop and using that as the target location.
    /// </summary>
    /// <remarks>
    /// The preview image origin is (0, 0) but the desktop origin may be non-zero,
    /// or even negative if the primary monitor is not the at the top-left of the
    /// entire desktop rectangle, so results may contain negative coordinates.
    /// </remarks>
    public static PointInfo GetJumpLocation(PointInfo previewLocation, SizeInfo previewSize, RectangleInfo desktopBounds)
    {
        return previewLocation
            .Scale(previewSize.ScaleToFitRatio(desktopBounds.Size))
            .Offset(desktopBounds.Location);
    }

    /// <summary>
    /// Moves the cursor to the specified location.
    /// </summary>
    /// <remarks>
    /// See https://github.com/mikeclayton/FancyMouse/pull/3
    /// </remarks>
    public static void JumpCursor(PointInfo location)
    {
        // set the new cursor position *twice* - the cursor sometimes end up in
        // the wrong place if we try to cross the dead space between non-aligned
        // monitors - e.g. when trying to move the cursor from (a) to (b) we can
        // *sometimes* - for no clear reason - end up at (c) instead.
        //
        //           +----------------+
        //           |(c)    (b)      |
        //           |                |
        //           |                |
        //           |                |
        // +---------+                |
        // |  (a)    |                |
        // +---------+----------------+
        //
        // setting the position a second time seems to fix this and moves the
        // cursor to the expected location (b)
        var point = location.ToPoint();
        Cursor.Position = point;
        Cursor.Position = point;
    }

    /// <summary>
    /// Sends an input simulating an absolute mouse move to the new location.
    /// </summary>
    /// <remarks>
    /// See https://github.com/microsoft/PowerToys/issues/24523
    ///     https://github.com/microsoft/PowerToys/pull/24527
    /// </remarks>
    public static void SimulateMouseMovementEvent(Point location)
    {
        var mouseMoveInput = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUTTYPE.INPUT_MOUSE,
            data = new NativeMethods.InputUnion
            {
                mi = new NativeMethods.MOUSEINPUT
                {
                    dx = NativeMethods.CalculateAbsoluteCoordinateX(location.X),
                    dy = NativeMethods.CalculateAbsoluteCoordinateY(location.Y),
                    mouseData = 0,
                    dwFlags = (uint)NativeMethods.MOUSE_INPUT_FLAGS.MOUSEEVENTF_MOVE
                        | (uint)NativeMethods.MOUSE_INPUT_FLAGS.MOUSEEVENTF_ABSOLUTE,
                    time = 0,
                    dwExtraInfo = 0,
                },
            },
        };
        var inputs = new NativeMethods.INPUT[] { mouseMoveInput };
        _ = NativeMethods.SendInput(1, inputs, NativeMethods.INPUT.Size);
    }
}
