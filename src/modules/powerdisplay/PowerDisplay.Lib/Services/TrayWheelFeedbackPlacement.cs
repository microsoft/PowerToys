// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Windows.Graphics;

namespace PowerDisplay.Common.Services;

/// <summary>
/// Calculates a tray-feedback rectangle from physical-pixel display geometry.
/// </summary>
public static class TrayWheelFeedbackPlacement
{
    /// <summary>
    /// Positions the overlay inward from the nearest outer display edge and clamps it to work area.
    /// </summary>
    public static RectInt32 Calculate(
        TrayIconBounds icon,
        RectInt32 outer,
        RectInt32 work,
        int width,
        int height,
        int gap)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegative(gap);

        width = Math.Min(width, Math.Max(1, work.Width));
        height = Math.Min(height, Math.Max(1, work.Height));

        var centerX = ((long)icon.Left + icon.Right) / 2;
        var centerY = ((long)icon.Top + icon.Bottom) / 2;
        var bottomDistance = Math.Abs((long)outer.Y + outer.Height - centerY);
        var topDistance = Math.Abs(centerY - outer.Y);
        var leftDistance = Math.Abs(centerX - outer.X);
        var rightDistance = Math.Abs((long)outer.X + outer.Width - centerX);

        var edge = Edge.Bottom;
        var nearest = bottomDistance;
        if (topDistance < nearest)
        {
            edge = Edge.Top;
            nearest = topDistance;
        }

        if (leftDistance < nearest)
        {
            edge = Edge.Left;
            nearest = leftDistance;
        }

        if (rightDistance < nearest)
        {
            edge = Edge.Right;
        }

        long x;
        long y;
        switch (edge)
        {
            case Edge.Top:
                x = centerX - (width / 2);
                y = (long)icon.Bottom + gap;
                break;
            case Edge.Left:
                x = (long)icon.Right + gap;
                y = centerY - (height / 2);
                break;
            case Edge.Right:
                x = (long)icon.Left - gap - width;
                y = centerY - (height / 2);
                break;
            default:
                x = centerX - (width / 2);
                y = (long)icon.Top - gap - height;
                break;
        }

        var minX = work.X;
        var minY = work.Y;
        var maxX = Math.Max(minX, (long)work.X + work.Width - width);
        var maxY = Math.Max(minY, (long)work.Y + work.Height - height);
        x = Math.Clamp(x, minX, maxX);
        y = Math.Clamp(y, minY, maxY);
        return new RectInt32((int)x, (int)y, width, height);
    }

    private enum Edge
    {
        Bottom,
        Top,
        Left,
        Right,
    }
}
