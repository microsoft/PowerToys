// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace MouseJumpUI.Helpers;

internal static class LayoutHelper
{
    /// <summary>
    /// Center an object on the given origin.
    /// </summary>
    public static Point CenterObject(Size obj, Point origin)
    {
        return new Point(
            x: (int)(origin.X - ((float)obj.Width / 2)),
            y: (int)(origin.Y - ((float)obj.Height / 2)));
    }

    /// <summary>
    /// Combines the specified regions and returns the smallest rectangle that contains them.
    /// </summary>
    /// <param name="regions">The regions to combine.</param>
    /// <returns>
    /// Returns the smallest rectangle that contains all the specified regions.
    /// </returns>
    public static Rectangle CombineRegions(List<Rectangle> regions)
    {
        if (regions == null)
        {
            throw new ArgumentNullException(nameof(regions));
        }

        if (regions.Count == 0)
        {
            return Rectangle.Empty;
        }

        var combined = regions.Aggregate(
            seed: regions[0],
            func: Rectangle.Union);
        return combined;
    }

    /// <summary>
    /// Returns the midpoint of the given region.
    /// </summary>
    public static Point GetMidpoint(Rectangle region)
    {
        return new Point(
            (region.Left + region.Right) / 2,
            (region.Top + region.Bottom) / 2);
    }

    /// <summary>
    /// Returns the largest Size object that can fit inside
    /// all of the given sizes. (Equivalent to a Size
    /// object with the smallest Width and smallest Height from
    /// all of the specified sizes).
    /// </summary>
    public static Size IntersectSizes(params Size[] sizes)
    {
        return new Size(
            sizes.Min(s => s.Width),
            sizes.Min(s => s.Height));
    }

    /// <summary>
    /// Returns the location to move the inner rectangle so that it sits entirely inside
    /// the outer rectangle. Returns the inner rectangle's current position if it is
    /// already inside the outer rectangle.
    /// </summary>
    public static Rectangle MoveInside(Rectangle inner, Rectangle outer)
    {
        if ((inner.Width > outer.Width) || (inner.Height > outer.Height))
        {
            throw new ArgumentException($"{nameof(inner)} cannot be larger than {nameof(outer)}.");
        }

        return inner with
        {
            X = Math.Clamp(inner.X, outer.X, outer.Right - inner.Width),
            Y = Math.Clamp(inner.Y, outer.Y, outer.Bottom - inner.Height),
        };
    }

    /// <summary>
    /// Scales a location within a reference region onto a new region
    /// so that it's proportionally in the same position in the new region.
    /// </summary>
    public static Point ScaleLocation(Rectangle originalBounds, Point originalLocation, Rectangle scaledBounds)
    {
        return new Point(
           (int)(originalLocation.X / (double)originalBounds.Width * scaledBounds.Width) + scaledBounds.Left,
           (int)(originalLocation.Y / (double)originalBounds.Height * scaledBounds.Height) + scaledBounds.Top);
    }

    /// <summary>
    /// Scale an object to fit inside the specified bounds while maintaining aspect ratio.
    /// </summary>
    public static Size ScaleToFit(Size obj, Size bounds)
    {
        if (bounds.Width == 0 || bounds.Height == 0)
        {
            return Size.Empty;
        }

        var widthRatio = (double)obj.Width / bounds.Width;
        var heightRatio = (double)obj.Height / bounds.Height;
        var scaledSize = (widthRatio > heightRatio)
            ? bounds with
            {
                Height = (int)(obj.Height / widthRatio),
            }
            : bounds with
            {
                Width = (int)(obj.Width / heightRatio),
            };
        return scaledSize;
    }

    /// <summary>
    /// Calculates the position to show the preview form based on a number of factors.
    /// </summary>
    /// <param name="desktopBounds">
    /// The bounds of the entire desktop / virtual screen. Might start at a negative
    /// x, y if a non-primary screen is located left of or above the primary screen.
    /// </param>
    /// <param name="activatedPosition">
    /// The current position of the cursor on the virtual desktop.
    /// </param>
    /// <param name="activatedMonitorBounds">
    /// The bounds of the screen the cursor is currently on. Might start at a negative
    /// x, y if a non-primary screen is located left of or above the primary screen.
    /// </param>
    /// <param name="maximumThumbnailImageSize">
    /// The largest allowable size of the preview image. This is literally the just
    /// image itself, not including padding around the image.
    /// </param>
    /// <param name="thumbnailImagePadding">
    /// The total width and height of padding around the preview image.
    /// </param>
    /// <returns>
    /// The size and location to use when showing the preview image form.
    /// </returns>
    public static Rectangle GetPreviewFormBounds(
        Rectangle desktopBounds,
        Point activatedPosition,
        Rectangle activatedMonitorBounds,
        Size maximumThumbnailImageSize,
        Size thumbnailImagePadding)
    {
        // see https://learn.microsoft.com/en-gb/windows/win32/gdi/the-virtual-screen
        // calculate the maximum size the form is allowed to be
        var maxFormSize = LayoutHelper.IntersectSizes(
            new[]
            {
                // can't be bigger than the current screen
                activatedMonitorBounds.Size,

                // can't be bigger than the max preview image
                // *plus* the padding around the preview image
                // (max thumbnail image size doesn't include the padding)
                maximumThumbnailImageSize + thumbnailImagePadding,
            });

        // calculate the actual form size by scaling the entire
        // desktop bounds into the max thumbnail size while accounting
        // for the size of the padding around the preview
        var thumbnailImageSize = LayoutHelper.ScaleToFit(
            obj: desktopBounds.Size,
            bounds: maxFormSize - thumbnailImagePadding);
        var formSize = thumbnailImageSize + thumbnailImagePadding;

        // center the form to the activated position, but nudge it back
        // inside the visible area of the screen if it falls outside
        var formBounds = LayoutHelper.MoveInside(
            inner: new Rectangle(
                LayoutHelper.CenterObject(
                    obj: formSize,
                    origin: activatedPosition),
                formSize),
            outer: activatedMonitorBounds);

        return formBounds;
    }
}
