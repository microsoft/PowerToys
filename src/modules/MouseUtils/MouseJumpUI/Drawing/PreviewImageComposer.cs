// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using MouseJumpUI.Drawing.Models;
using MouseJumpUI.NativeMethods.Core;
using MouseJumpUI.NativeWrappers;

namespace MouseJumpUI.Drawing;

internal static class PreviewImageComposer
{
    public static LayoutCoords CalculateCoords(
        LayoutConfig layoutConfig)
    {
        if (layoutConfig is null)
        {
            throw new ArgumentNullException(nameof(layoutConfig));
        }

        var builder = new LayoutCoords.Builder
        {
            LayoutConfig = layoutConfig,
        };

        builder.ActivatedScreen = layoutConfig.ScreenBounds[layoutConfig.ActivatedScreen];

        // work out the maximum *constrained* form size
        // * can't be bigger than the activated screen
        // * can't be bigger than the max form size
        var maxFormSize = builder.ActivatedScreen.Size
            .Intersect(layoutConfig.MaximumFormSize);

        // the drawing area for screen images is inside the
        // form border and inside the preview border
        var maxDrawingSize = maxFormSize
            .Shrink(layoutConfig.FormPadding)
            .Shrink(layoutConfig.PreviewPadding);

        // scale the virtual screen to fit inside the drawing bounds
        var scalingRatio = layoutConfig.VirtualScreen.Size
            .ScaleToFitRatio(maxDrawingSize);

        // position the drawing bounds inside the preview border
        var drawingBounds = layoutConfig.VirtualScreen.Size
            .Scale(scalingRatio)
            .PlaceAt(layoutConfig.PreviewPadding.Left, layoutConfig.PreviewPadding.Top);

        // now we know the size of the drawing area we can work out the preview size
        builder.PreviewBounds = drawingBounds.Enlarge(layoutConfig.PreviewPadding);

        // ... and the form size
        // * center the form to the activated position, but nudge it back
        //   inside the visible area of the activated screen if it falls outside
        builder.FormBounds = builder.PreviewBounds.Size
            .PlaceAt(0, 0)
            .Enlarge(layoutConfig.FormPadding)
            .Center(layoutConfig.ActivatedLocation)
            .Clamp(builder.ActivatedScreen);

        // now calculate the positions of each of the screen images
        builder.ScreenBounds = layoutConfig.ScreenBounds
            .Select(
                screen => screen
                    .Offset(layoutConfig.VirtualScreen.Location.Size.Negate())
                    .Scale(scalingRatio)
                    .Offset(layoutConfig.PreviewPadding.Left, layoutConfig.PreviewPadding.Top))
            .ToList();

        return builder.Build();
    }

    /// <summary>
    /// Captures the specified regions of the current desktop and composes them
    /// into a single screenshot. The resulting image is a scaled to fit the
    /// specified screenshot size and individual regions drawn according to
    /// their proportional positions on the main desktop.
    /// </summary>
    public static void CopyFromScreen(
        HDC sourceHdc,
        HDC targetHdc,
        IList<RectangleInfo> sourceBounds,
        IList<RectangleInfo> targetBounds)
    {
        // note - it's faster to capture each screen individually and assemble them into
        // a single image as we don't have to transfer all the regions that are outside
        // the screen regions - e.g. the *** in the ascii art below:
        //
        // +----------------+********
        // |                |********
        // |       1        +-------+
        // |                |       |
        // +----------------+   0   |
        // *****************|       |
        // *****************+-------+
        //
        // for "jagged" monitor layouts this can be a big percentage of the entire desktop rectangle.
        _ = Gdi32.SetStretchBltMode(targetHdc, NativeMethods.Gdi32.STRETCH_BLT_MODE.STRETCH_HALFTONE);

        for (var i = 0; i < sourceBounds.Count; i++)
        {
            var source = sourceBounds[i].ToRectangle();
            var target = targetBounds[i].ToRectangle();
            _ = Gdi32.StretchBlt(
                targetHdc,
                target.X,
                target.Y,
                target.Width,
                target.Height,
                sourceHdc,
                source.X,
                source.Y,
                source.Width,
                source.Height,
                NativeMethods.Gdi32.ROP_CODE.SRCCOPY);
        }
    }
}
