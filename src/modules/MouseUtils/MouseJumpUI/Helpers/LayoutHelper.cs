// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MouseJumpUI.Models.Drawing;
using MouseJumpUI.Models.Layout;

namespace MouseJumpUI.Helpers;

internal static class LayoutHelper
{
    public static LayoutInfo CalculateLayoutInfo(
        LayoutConfig layoutConfig)
    {
        if (layoutConfig is null)
        {
            throw new ArgumentNullException(nameof(layoutConfig));
        }

        var builder = new LayoutInfo.Builder
        {
            LayoutConfig = layoutConfig,
        };

        builder.ActivatedScreenBounds = layoutConfig.Screens[layoutConfig.ActivatedScreenIndex].Bounds;

        // work out the maximum *constrained* form size
        // * can't be bigger than the activated screen
        // * can't be bigger than the max form size
        var maxFormSize = builder.ActivatedScreenBounds.Size
            .Intersect(layoutConfig.MaximumFormSize);

        // the drawing area for screen images is inside the
        // form border and inside the preview border
        var maxDrawingSize = maxFormSize
            .Shrink(layoutConfig.FormPadding)
            .Shrink(layoutConfig.PreviewPadding);

        // scale the virtual screen to fit inside the drawing bounds
        var scalingRatio = layoutConfig.VirtualScreenBounds.Size
            .ScaleToFitRatio(maxDrawingSize);

        // position the drawing bounds inside the preview border
        var drawingBounds = layoutConfig.VirtualScreenBounds.Size
            .ScaleToFit(maxDrawingSize)
            .PlaceAt(layoutConfig.PreviewPadding.Left, layoutConfig.PreviewPadding.Top);

        // now we know the size of the drawing area we can work out the preview size
        builder.PreviewBounds = drawingBounds.Enlarge(layoutConfig.PreviewPadding);

        // ... and the form size
        // * center the form to the activated position, but nudge it back
        //   inside the visible area of the activated screen if it falls outside
        builder.FormBounds = builder.PreviewBounds
            .Enlarge(layoutConfig.FormPadding)
            .Center(layoutConfig.ActivatedLocation)
            .Clamp(builder.ActivatedScreenBounds);

        // now calculate the positions of each of the screen images on the preview
        builder.ScreenBounds = layoutConfig.Screens
            .Select(
                screen => screen.Bounds
                    .Offset(layoutConfig.VirtualScreenBounds.Location.ToSize().Negate())
                    .Scale(scalingRatio)
                    .Offset(layoutConfig.PreviewPadding.Left, layoutConfig.PreviewPadding.Top))
            .ToList();

        return builder.Build();
    }

    /// <summary>
    /// Resize and position the specified form.
    /// </summary>
    public static void PositionForm(
        Form form, RectangleInfo formBounds)
    {
        // note - do this in two steps rather than "this.Bounds = formBounds" as there
        // appears to be an issue in WinForms with dpi scaling even when using PerMonitorV2,
        // where the form scaling uses either the *primary* screen scaling or the *previous*
        // screen's scaling when the form is moved to a different screen. i've got no idea
        // *why*, but the exact sequence of calls below seems to be a workaround...
        // see https://github.com/mikeclayton/FancyMouse/issues/2
        var bounds = formBounds.ToRectangle();
        form.Location = bounds.Location;
        _ = form.PointToScreen(Point.Empty);
        form.Size = bounds.Size;
    }
}
