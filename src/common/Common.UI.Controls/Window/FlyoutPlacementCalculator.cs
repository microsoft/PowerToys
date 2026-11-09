// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Windows.Graphics;

namespace Microsoft.PowerToys.Common.UI.Controls.Window;

/// <summary>
/// Calculates flyout placement without reading display, cursor, or window state.
/// </summary>
public static class FlyoutPlacementCalculator
{
    /// <summary>
    /// Determine the taskbar edge from its confirmed display rectangle, or from a single reserved
    /// work area strip. Uses Bottom when the available geometry has no unambiguous taskbar edge.
    /// </summary>
    /// <param name="outerBounds">The target display bounds in screen physical pixels.</param>
    /// <param name="workArea">The target display work area in screen physical pixels.</param>
    /// <param name="taskbarOnDisplay">The system taskbar rectangle, already confirmed to be on this display.</param>
    /// <returns>The edge occupied by the taskbar, or Bottom as an explicit fallback.</returns>
    public static FlyoutPlacementEdge GetTaskbarEdge(RectInt32 outerBounds, RectInt32 workArea, RectInt32? taskbarOnDisplay)
    {
        if (taskbarOnDisplay is RectInt32 taskbar && taskbar.Width > 0 && taskbar.Height > 0)
        {
            if (taskbar.Width >= taskbar.Height)
            {
                return (2L * taskbar.Y) + taskbar.Height < (2L * outerBounds.Y) + outerBounds.Height
                    ? FlyoutPlacementEdge.Top
                    : FlyoutPlacementEdge.Bottom;
            }

            return (2L * taskbar.X) + taskbar.Width < (2L * outerBounds.X) + outerBounds.Width
                ? FlyoutPlacementEdge.Left
                : FlyoutPlacementEdge.Right;
        }

        bool left = workArea.X > outerBounds.X;
        bool top = workArea.Y > outerBounds.Y;
        bool right = (long)workArea.X + workArea.Width < (long)outerBounds.X + outerBounds.Width;
        bool bottom = (long)workArea.Y + workArea.Height < (long)outerBounds.Y + outerBounds.Height;
        int reservedEdges = (left ? 1 : 0) + (top ? 1 : 0) + (right ? 1 : 0) + (bottom ? 1 : 0);
        if (reservedEdges == 1)
        {
            if (left)
            {
                return FlyoutPlacementEdge.Left;
            }

            if (top)
            {
                return FlyoutPlacementEdge.Top;
            }

            if (right)
            {
                return FlyoutPlacementEdge.Right;
            }
        }

        // A missing or ambiguous reserved strip (including auto-hide) has an explicit default.
        return FlyoutPlacementEdge.Bottom;
    }

    /// <summary>
    /// Places a flyout beside its anchor, toward the desktop from the taskbar, and within the work area.
    /// Leaves a gap at each work area boundary, reducing it only when necessary to retain a visible pixel.
    /// </summary>
    /// <param name="context">The captured placement context.</param>
    /// <param name="widthPx">The requested width in physical pixels.</param>
    /// <param name="heightPx">The requested height in physical pixels.</param>
    /// <param name="gapPx">The anchor and work area gap in physical pixels.</param>
    /// <returns>The final window rectangle in absolute screen physical-pixel coordinates.</returns>
    public static RectInt32 Calculate(FlyoutPlacementContext context, int widthPx, int heightPx, int gapPx)
    {
        ArgumentNullException.ThrowIfNull(context);

        var work = context.WorkArea;
        int workWidth = Math.Max(0, work.Width);
        int workHeight = Math.Max(0, work.Height);
        int gap = Math.Max(0, gapPx);
        int horizontalMargin = Math.Min(gap, Math.Max(0, (workWidth - 1) / 2));
        int verticalMargin = Math.Min(gap, Math.Max(0, (workHeight - 1) / 2));
        int width = Math.Min(Math.Max(0, widthPx), workWidth - (2 * horizontalMargin));
        int height = Math.Min(Math.Max(0, heightPx), workHeight - (2 * verticalMargin));

        // Use wider intermediates for negative virtual-screen origins and large rectangles or gaps.
        long workRight = (long)work.X + workWidth;
        long workBottom = (long)work.Y + workHeight;
        long anchorRight = (long)context.Anchor.X + Math.Max(0, context.Anchor.Width);
        long anchorBottom = (long)context.Anchor.Y + Math.Max(0, context.Anchor.Height);
        long x = context.Anchor.X + ((long)Math.Max(0, context.Anchor.Width) / 2) - (width / 2);
        long y = context.Anchor.Y + ((long)Math.Max(0, context.Anchor.Height) / 2) - (height / 2);

        switch (context.TaskbarEdge)
        {
            case FlyoutPlacementEdge.Top:
                y = Math.Max(anchorBottom, work.Y) + gap;
                break;
            case FlyoutPlacementEdge.Left:
                x = Math.Max(anchorRight, work.X) + gap;
                break;
            case FlyoutPlacementEdge.Right:
                x = Math.Min(context.Anchor.X, workRight) - gap - width;
                break;
            default:
                y = Math.Min(context.Anchor.Y, workBottom) - gap - height;
                break;
        }

        x = Math.Clamp(x, (long)work.X + horizontalMargin, workRight - horizontalMargin - width);
        y = Math.Clamp(y, (long)work.Y + verticalMargin, workBottom - verticalMargin - height);

        return new RectInt32(
            (int)Math.Clamp(x, int.MinValue, int.MaxValue),
            (int)Math.Clamp(y, int.MinValue, int.MaxValue),
            width,
            height);
    }
}
