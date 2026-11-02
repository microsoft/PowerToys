// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;

using MouseJump.Common.Win32Gen;
using MouseJump.Models.Drawing;

using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MouseJump.Common.Helpers;

public static class MouseHelper
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
    /// Get the current position of the cursor.
    /// </summary>
    public static PointInfo GetCursorPosition()
    {
        _ = User32.GetCursorPos(out var point).ThrowIfFailed();
        return new(point.X, point.Y);
    }

    /// <summary>
    /// Moves the cursor to the specified location.
    /// </summary>
    /// <remarks>
    /// See https://github.com/mikeclayton/FancyMouse/pull/3
    /// </remarks>
    public static void SetCursorPosition(PointInfo position)
    {
        MouseHelper.SetCursorPositionInternal(position);

        // temporary workaround for issue #1273
        MouseHelper.SimulateMouseMovementEvent(position);
    }

    private static void SetCursorPositionInternal(PointInfo position)
    {
        // set the new cursor position *twice* - the cursor sometimes end up in
        // the wrong place if we try to cross the dead space between non-aligned
        // monitors - e.g. when trying to move the cursor from (a) to (b) through
        // the dotted area we can *sometimes* - for no clear reason - end up at
        // (c) instead.
        //
        // ..........+----------------+
        // ..........|(c)    (b)      |
        // ..........|                |
        // ..........|                |
        // ..........|                |
        // +---------+                |
        // |  (a)    |                |
        // +---------+----------------+
        //
        // setting the position more than once seems to fix this and moves the
        // cursor to the expected location (b)
        var targetPosition = position.ToPoint();
        for (var i = 0; i < 2; i++)
        {
            _ = User32.SetCursorPos(targetPosition.X, targetPosition.Y)
                .ThrowIfFailed();
            _ = User32.GetCursorPos(out var currentPosition)
                .ThrowIfFailed();
            if ((currentPosition.X == position.X) && (currentPosition.Y == position.Y))
            {
                break;
            }
        }
    }

    /// <summary>
    /// Sends an input simulating an absolute mouse move to the new location.
    /// </summary>
    /// <remarks>
    /// See https://github.com/microsoft/PowerToys/issues/24523
    ///     https://github.com/microsoft/PowerToys/pull/24527
    /// </remarks>
    private static void SimulateMouseMovementEvent(PointInfo location)
    {
        var inputs = new INPUT[]
        {
            new()
            {
                type = INPUT_TYPE.INPUT_MOUSE,
                Anonymous = new()
                {
                    mi = new MOUSEINPUT
                    {
                        dx = (int)MouseHelper.CalculateAbsoluteCoordinateX(location.X),
                        dy = (int)MouseHelper.CalculateAbsoluteCoordinateY(location.Y),
                        mouseData = 0,
                        dwFlags = MOUSE_EVENT_FLAGS.MOUSEEVENTF_MOVE | MOUSE_EVENT_FLAGS.MOUSEEVENTF_ABSOLUTE,
                        time = 0,
                        dwExtraInfo = default,
                    },
                },
            },
        };

        // cbSize is the size of an INPUT struct, *not* the number of inputs - SendInput
        // rejects the call if it's wrong, and the crosshair silently stops following the
        // cursor (see microsoft#48290).
        //
        // the cursor has already moved by this point, so don't crash if the synthetic
        // mouse move can't be sent - but assert in debug builds so a regression like
        // the one above gets noticed during development
        var cbSize = Marshal.SizeOf<INPUT>();
        var result = User32.SendInput(inputs, cbSize)
            .IgnoreFailure();
        Debug.Assert(result.Success, $"SendInput failed - LastError: {result.LastError}");
    }

    private static decimal CalculateAbsoluteCoordinateX(decimal x)
    {
        // If MOUSEEVENTF_ABSOLUTE value is specified, dx and dy contain normalized absolute coordinates between 0 and 65,535.
        // see https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-mouseinput
        var result = User32.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXSCREEN)
            .ThrowIfFailed()
            .GetValue();
        return (x * 65535) / result;
    }

    private static decimal CalculateAbsoluteCoordinateY(decimal y)
    {
        // If MOUSEEVENTF_ABSOLUTE value is specified, dx and dy contain normalized absolute coordinates between 0 and 65,535.
        // see https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-mouseinput
        var result = User32.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYSCREEN)
            .ThrowIfFailed()
            .GetValue();
        return (y * 65535) / result;
    }
}
