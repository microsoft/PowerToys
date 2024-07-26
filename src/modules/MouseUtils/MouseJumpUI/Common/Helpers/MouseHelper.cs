// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.InteropServices;
using MouseJumpUI.Common.Models.Drawing;
using MouseJumpUI.Common.NativeMethods;
using static MouseJumpUI.Common.NativeMethods.Core;
using static MouseJumpUI.Common.NativeMethods.User32;

namespace MouseJumpUI.Common.Helpers;

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
    internal static PointInfo GetJumpLocation(PointInfo previewLocation, SizeInfo previewSize, RectangleInfo desktopBounds)
    {
        return previewLocation
            .Scale(previewSize.ScaleToFitRatio(desktopBounds.Size))
            .Offset(desktopBounds.Location);
    }

    /// <summary>
    /// Get the current position of the cursor.
    /// </summary>
    internal static PointInfo GetCursorPosition()
    {
        var lpPoint = new LPPOINT(new POINT(0, 0));
        var result = User32.GetCursorPos(lpPoint);
        if (!result)
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error());
        }

        var point = lpPoint.ToStructure();
        lpPoint.Free();

        return new PointInfo(
            point.x, point.y);
    }

    /// <summary>
    /// Moves the cursor to the specified location.
    /// </summary>
    /// <remarks>
    /// See https://github.com/mikeclayton/FancyMouse/pull/3
    /// </remarks>
    internal static void SetCursorPosition(PointInfo location)
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
        var target = location.ToPoint();
        for (var i = 0; i < 2; i++)
        {
            var result = User32.SetCursorPos(target.X, target.Y);
            if (!result)
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error());
            }

            var current = MouseHelper.GetCursorPosition();
            if ((current.X == target.X) || (current.Y == target.Y))
            {
                break;
            }
        }

        // temporary workaround for issue #1273
        MouseHelper.SimulateMouseMovementEvent(location);
    }

    /// <summary>
    /// Sends an input simulating an absolute mouse move to the new location.
    /// </summary>
    /// <remarks>
    /// See https://github.com/microsoft/PowerToys/issues/24523
    ///     https://github.com/microsoft/PowerToys/pull/24527
    /// </remarks>
    internal static void SimulateMouseMovementEvent(PointInfo location)
    {
        var inputs = new User32.INPUT[]
        {
            new(
                type: INPUT_TYPE.INPUT_MOUSE,
                data: new INPUT.DUMMYUNIONNAME(
                    mi: new MOUSEINPUT(
                        dx: (int)MouseHelper.CalculateAbsoluteCoordinateX(location.X),
                        dy: (int)MouseHelper.CalculateAbsoluteCoordinateY(location.Y),
                        mouseData: 0,
                        dwFlags: MOUSE_EVENT_FLAGS.MOUSEEVENTF_MOVE | MOUSE_EVENT_FLAGS.MOUSEEVENTF_ABSOLUTE,
                        time: 0,
                        dwExtraInfo: ULONG_PTR.Null))),
        };
        var result = User32.SendInput(
            (UINT)inputs.Length,
            new LPINPUT(inputs),
            INPUT.Size * inputs.Length);
        if (result != inputs.Length)
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error());
        }
    }

    private static decimal CalculateAbsoluteCoordinateX(decimal x)
    {
        // If MOUSEEVENTF_ABSOLUTE value is specified, dx and dy contain normalized absolute coordinates between 0 and 65,535.
        // see https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-mouseinput
        return (x * 65535) / User32.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXSCREEN);
    }

    private static decimal CalculateAbsoluteCoordinateY(decimal y)
    {
        // If MOUSEEVENTF_ABSOLUTE value is specified, dx and dy contain normalized absolute coordinates between 0 and 65,535.
        // see https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-mouseinput
        return (y * 65535) / User32.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYSCREEN);
    }
}
