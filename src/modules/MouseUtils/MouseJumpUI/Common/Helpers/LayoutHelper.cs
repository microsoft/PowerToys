// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using MouseJumpUI.Common.Models.Drawing;
using MouseJumpUI.Common.Models.Layout;
using MouseJumpUI.Common.Models.Styles;

namespace MouseJumpUI.Common.Helpers;

internal static class LayoutHelper
{
    public static PreviewLayout GetPreviewLayout(
        PreviewStyle previewStyle, List<RectangleInfo> screens, PointInfo activatedLocation)
    {
        ArgumentNullException.ThrowIfNull(previewStyle);
        ArgumentNullException.ThrowIfNull(screens);

        if (screens.Count == 0)
        {
            throw new ArgumentException("Value must contain at least one item.", nameof(screens));
        }

        var builder = new PreviewLayout.Builder();
        builder.Screens = screens.ToList();

        // calculate the bounding rectangle for the virtual screen
        builder.VirtualScreen = LayoutHelper.GetCombinedScreenBounds(builder.Screens);

        // find the screen that contains the activated location - this is the
        // one we'll show the preview form on
        var activatedScreen = builder.Screens.Single(
            screen => screen.Contains(activatedLocation));
        builder.ActivatedScreenIndex = builder.Screens.IndexOf(activatedScreen);

        // work out the maximum allowed size of the preview form:
        // * can't be bigger than the activated screen
        // * can't be bigger than the configured canvas size
        var maxPreviewSize = activatedScreen.Size
            .Intersect(previewStyle.CanvasSize);

        // the "content area" (i.e. drawing area) for screenshots is inside the
        // preview border and inside the preview padding (if any)
        var maxContentSize = maxPreviewSize
            .Shrink(previewStyle.CanvasStyle.MarginStyle)
            .Shrink(previewStyle.CanvasStyle.BorderStyle)
            .Shrink(previewStyle.CanvasStyle.PaddingStyle);

        // scale the virtual screen to fit inside the content area
        var screenScalingRatio = builder.VirtualScreen.Size
            .ScaleToFitRatio(maxContentSize);

        // work out the actual size of the "content area" by scaling the virtual screen
        // to fit inside the maximum content area while maintaining its aspect ration.
        // we'll also offset it to allow for any margins, borders and padding
        var contentBounds = builder.VirtualScreen.Size
            .Scale(screenScalingRatio)
            .Floor()
            .PlaceAt(0, 0)
            .Offset(previewStyle.CanvasStyle.MarginStyle.Left, previewStyle.CanvasStyle.MarginStyle.Top)
            .Offset(previewStyle.CanvasStyle.BorderStyle.Left, previewStyle.CanvasStyle.BorderStyle.Top)
            .Offset(previewStyle.CanvasStyle.PaddingStyle.Left, previewStyle.CanvasStyle.PaddingStyle.Top);

        // now we know the actual size of the content area we can work outwards to
        // get the size of the background bounds including margins, borders and padding
        builder.PreviewStyle = previewStyle;
        builder.PreviewBounds = LayoutHelper.GetBoxBoundsFromContentBounds(
            contentBounds,
            previewStyle.CanvasStyle);

        // ... and then the size and position of the preview form on the activated screen
        // * center the form to the activated position, but nudge it back
        //   inside the visible area of the activated screen if it falls outside
        var formBounds = builder.PreviewBounds.OuterBounds
            .Center(activatedLocation)
            .Clamp(activatedScreen);
        builder.FormBounds = formBounds;

        // now calculate the positions of each of the screenshot images on the preview
        builder.ScreenshotBounds = builder.Screens
            .Select(
                screen => LayoutHelper.GetBoxBoundsFromOuterBounds(
                    screen
                        .Offset(builder.VirtualScreen.Location.ToSize().Invert())
                        .Scale(screenScalingRatio)
                        .Offset(builder.PreviewBounds.ContentBounds.Location.ToSize())
                        .Truncate(),
                    previewStyle.ScreenStyle))
            .ToList();

        return builder.Build();
    }

    internal static RectangleInfo GetCombinedScreenBounds(List<RectangleInfo> screens)
    {
        return screens.Skip(1).Aggregate(
            seed: screens.First(),
            (bounds, screen) => bounds.Union(screen));
    }

    /// <summary>
    /// Calculates the bounds of the various areas of a box, given the content bounds and the box style.
    /// Starts with the content bounds and works outward, enlarging the content bounds by the padding, border, and margin sizes to calculate the outer bounds of the box.
    /// </summary>
    /// <param name="contentBounds">The content bounds of the box.</param>
    /// <param name="boxStyle">The style of the box, which includes the sizes of the margin, border, and padding areas.</param>
    /// <returns>A <see cref="BoxBounds"/> object that represents the bounds of the different areas of the box.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="contentBounds"/> or <paramref name="boxStyle"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when any of the styles in <paramref name="boxStyle"/> is null.</exception>
    internal static BoxBounds GetBoxBoundsFromContentBounds(
        RectangleInfo contentBounds,
        BoxStyle boxStyle)
    {
        ArgumentNullException.ThrowIfNull(contentBounds);
        ArgumentNullException.ThrowIfNull(boxStyle);
        if (boxStyle.PaddingStyle == null || boxStyle.BorderStyle == null || boxStyle.MarginStyle == null)
        {
            throw new ArgumentException(null, nameof(boxStyle));
        }

        var paddingBounds = contentBounds.Enlarge(boxStyle.PaddingStyle);
        var borderBounds = paddingBounds.Enlarge(boxStyle.BorderStyle);
        var marginBounds = borderBounds.Enlarge(boxStyle.MarginStyle);
        var outerBounds = marginBounds;
        return new(
            outerBounds, marginBounds, borderBounds, paddingBounds, contentBounds);
    }

    /// <summary>
    /// Calculates the bounds of the various areas of a box, given the outer bounds and the box style.
    /// This method starts with the outer bounds and works inward, shrinking the outer bounds by the margin, border, and padding sizes to calculate the content bounds of the box.
    /// </summary>
    /// <param name="outerBounds">The outer bounds of the box.</param>
    /// <param name="boxStyle">The style of the box, which includes the sizes of the margin, border, and padding areas.</param>
    /// <returns>A <see cref="BoxBounds"/> object that represents the bounds of the different areas of the box.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="outerBounds"/> or <paramref name="boxStyle"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when any of the styles in <paramref name="boxStyle"/> is null.</exception>
    internal static BoxBounds GetBoxBoundsFromOuterBounds(
        RectangleInfo outerBounds,
        BoxStyle boxStyle)
    {
        ArgumentNullException.ThrowIfNull(outerBounds);
        ArgumentNullException.ThrowIfNull(boxStyle);
        if (outerBounds == null || boxStyle.MarginStyle == null || boxStyle.BorderStyle == null || boxStyle.PaddingStyle == null)
        {
            throw new ArgumentException(null, nameof(boxStyle));
        }

        var marginBounds = outerBounds;
        var borderBounds = marginBounds.Shrink(boxStyle.MarginStyle);
        var paddingBounds = borderBounds.Shrink(boxStyle.BorderStyle);
        var contentBounds = paddingBounds.Shrink(boxStyle.PaddingStyle);
        return new(
            outerBounds, marginBounds, borderBounds, paddingBounds, contentBounds);
    }
}
