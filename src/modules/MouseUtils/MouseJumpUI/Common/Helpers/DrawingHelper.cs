// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using MouseJumpUI.Common.Imaging;
using MouseJumpUI.Common.Models.Drawing;
using MouseJumpUI.Common.Models.Layout;
using MouseJumpUI.Common.Models.Styles;

namespace MouseJumpUI.Common.Helpers;

internal static class DrawingHelper
{
    public static Bitmap RenderPreview(
        PreviewLayout previewLayout,
        IImageRegionCopyService imageCopyService,
        Action<Bitmap>? previewImageCreatedCallback = null,
        Action? previewImageUpdatedCallback = null)
    {
        var stopwatch = Stopwatch.StartNew();

        // initialize the preview image
        var previewBounds = previewLayout.PreviewBounds.OuterBounds.ToRectangle();
        var previewImage = new Bitmap(previewBounds.Width, previewBounds.Height, PixelFormat.Format32bppPArgb);
        var previewGraphics = Graphics.FromImage(previewImage);
        previewImageCreatedCallback?.Invoke(previewImage);

        DrawingHelper.DrawRaisedBorder(previewGraphics, previewLayout.PreviewStyle.CanvasStyle, previewLayout.PreviewBounds);
        DrawingHelper.DrawBackgroundFill(
            previewGraphics,
            previewLayout.PreviewStyle.CanvasStyle,
            previewLayout.PreviewBounds,
            []);

        // sort the source and target screen areas into the order we want to
        // draw them, putting the activated screen first (we need to capture
        // and draw the activated screen before we show the form because
        // otherwise we'll capture the form as part of the screenshot!)
        var sourceScreens = new List<RectangleInfo> { previewLayout.Screens[previewLayout.ActivatedScreenIndex] }
            .Concat(previewLayout.Screens.Where((_, idx) => idx != previewLayout.ActivatedScreenIndex))
            .ToList();
        var targetScreens = new List<BoxBounds> { previewLayout.ScreenshotBounds[previewLayout.ActivatedScreenIndex] }
            .Concat(previewLayout.ScreenshotBounds.Where((_, idx) => idx != previewLayout.ActivatedScreenIndex))
            .ToList();

        // draw all the screenshot bezels
        foreach (var screenshotBounds in previewLayout.ScreenshotBounds)
        {
            DrawingHelper.DrawRaisedBorder(
                previewGraphics, previewLayout.PreviewStyle.ScreenStyle, screenshotBounds);
        }

        var refreshRequired = false;
        var placeholdersDrawn = false;
        for (var i = 0; i < sourceScreens.Count; i++)
        {
            imageCopyService.CopyImageRegion(previewGraphics, sourceScreens[i], targetScreens[i].ContentBounds);
            refreshRequired = true;

            // show the placeholder images and show the form if it looks like it might take
            // a while to capture the remaining screenshot images (but only if there are any)
            if (stopwatch.ElapsedMilliseconds > 250)
            {
                // draw placeholder backgrounds for any undrawn screens
                if (!placeholdersDrawn)
                {
                    DrawingHelper.DrawScreenPlaceholders(
                        previewGraphics,
                        previewLayout.PreviewStyle.ScreenStyle,
                        targetScreens.GetRange(i + 1, targetScreens.Count - i - 1));
                    placeholdersDrawn = true;
                }

                previewImageUpdatedCallback?.Invoke();
                refreshRequired = false;
            }
        }

        if (refreshRequired)
        {
            previewImageUpdatedCallback?.Invoke();
        }

        stopwatch.Stop();

        return previewImage;
    }

    /// <summary>
    /// Draws a border shape with an optional raised 3d highlight and shadow effect.
    /// </summary>
    private static void DrawRaisedBorder(
        Graphics graphics, BoxStyle boxStyle, BoxBounds boxBounds)
    {
        var borderStyle = boxStyle.BorderStyle;
        if ((borderStyle.Horizontal == 0) || (borderStyle.Vertical == 0))
        {
            return;
        }

        // draw the main box border
        using var borderBrush = new SolidBrush(borderStyle.Color);
        var borderRegion = new Region(boxBounds.BorderBounds.ToRectangle());
        borderRegion.Exclude(boxBounds.PaddingBounds.ToRectangle());
        graphics.FillRegion(borderBrush, borderRegion);

        // draw the highlight and shadow
        var bounds = boxBounds.BorderBounds.ToRectangle();
        using var highlight = new Pen(Color.FromArgb(0x44, 0xFF, 0xFF, 0xFF));
        using var shadow = new Pen(Color.FromArgb(0x44, 0x00, 0x00, 0x00));

        var outer = (
            Left: bounds.Left,
            Top: bounds.Top,
            Right: bounds.Right - 1,
            Bottom: bounds.Bottom - 1
        );
        var inner = (
            Left: bounds.Left + (int)borderStyle.Left - 1,
            Top: bounds.Top + (int)borderStyle.Top - 1,
            Right: bounds.Right - (int)borderStyle.Right,
            Bottom: bounds.Bottom - (int)borderStyle.Bottom
        );

        for (var i = 0; i < borderStyle.Depth; i++)
        {
            // left edge
            if (borderStyle.Left >= i * 2)
            {
                graphics.DrawLine(highlight, outer.Left, outer.Top, outer.Left, outer.Bottom);
                graphics.DrawLine(shadow, inner.Left, inner.Top, inner.Left, inner.Bottom);
            }

            // top edge
            if (borderStyle.Top >= i * 2)
            {
                graphics.DrawLine(highlight, outer.Left, outer.Top, outer.Right, outer.Top);
                graphics.DrawLine(shadow, inner.Left, inner.Top, inner.Right, inner.Top);
            }

            // right edge
            if (borderStyle.Right >= i * 2)
            {
                graphics.DrawLine(highlight, inner.Right, inner.Top, inner.Right, inner.Bottom);
                graphics.DrawLine(shadow, outer.Right, outer.Top, outer.Right, outer.Bottom);
            }

            // bottom edge
            if (borderStyle.Bottom >= i * 2)
            {
                graphics.DrawLine(highlight, inner.Left, inner.Bottom, inner.Right, inner.Bottom);
                graphics.DrawLine(shadow, outer.Left, outer.Bottom, outer.Right, outer.Bottom);
            }

            // shrink the outer border for the next iteration
            outer = (
                outer.Left + 1,
                outer.Top + 1,
                outer.Right - 1,
                outer.Bottom - 1
            );

            // enlarge the inner border for the next iteration
            inner = (
                inner.Left - 1,
                inner.Top - 1,
                inner.Right + 1,
                inner.Bottom + 1
            );
        }
    }

    /// <summary>
    /// Draws a gradient-filled background shape.
    /// </summary>
    private static void DrawBackgroundFill(
        Graphics graphics, BoxStyle boxStyle, BoxBounds boxBounds, IEnumerable<RectangleInfo> excludeBounds)
    {
        var backgroundBounds = boxBounds.PaddingBounds;

        using var backgroundBrush = DrawingHelper.GetBackgroundStyleBrush(boxStyle.BackgroundStyle, backgroundBounds);
        if (backgroundBrush == null)
        {
            return;
        }

        // it's faster to build a region with the screen areas excluded
        // and fill that than it is to fill the entire bounding rectangle
        var backgroundRegion = new Region(backgroundBounds.ToRectangle());
        foreach (var exclude in excludeBounds)
        {
            backgroundRegion.Exclude(exclude.ToRectangle());
        }

        graphics.FillRegion(backgroundBrush, backgroundRegion);
    }

    /// <summary>
    /// Draws placeholder background images for the specified screens on the preview.
    /// </summary>
    private static void DrawScreenPlaceholders(
        Graphics graphics, BoxStyle screenStyle, IList<BoxBounds> screenBounds)
    {
        if (screenBounds.Count == 0)
        {
            return;
        }

        if (screenStyle?.BackgroundStyle?.Color1 == null)
        {
            return;
        }

        using var brush = new SolidBrush(screenStyle.BackgroundStyle.Color1.Value);
        graphics.FillRectangles(brush, screenBounds.Select(bounds => bounds.PaddingBounds.ToRectangle()).ToArray());
    }

    private static Brush? GetBackgroundStyleBrush(BackgroundStyle backgroundStyle, RectangleInfo backgroundBounds)
    {
        var backgroundBrush = backgroundStyle switch
        {
            { Color1: not null, Color2: not null } =>
                /* draw a gradient fill if both colors are specified */
                new LinearGradientBrush(
                    backgroundBounds.ToRectangle(),
                    backgroundStyle.Color1.Value,
                    backgroundStyle.Color2.Value,
                    LinearGradientMode.ForwardDiagonal),
            { Color1: not null } =>
                /* draw a solid fill if only one color is specified */
                new SolidBrush(
                    backgroundStyle.Color1.Value),
            { Color2: not null } =>
                /* draw a solid fill if only one color is specified */
                new SolidBrush(
                    backgroundStyle.Color2.Value),
            _ => (Brush?)null,
        };
        return backgroundBrush;
    }
}
