// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

using MouseJump.Common.Bezels;
using MouseJump.Models.Drawing;
using MouseJump.Models.Styles;

namespace MouseJump.Common.Helpers;

public static class DrawingHelper
{
    /// <summary>
    /// Renders the gradient-filled background for a box of the given <paramref name="size"/>.
    /// Can be used to render the main background for the preview pane, and the screen
    /// background image for delayed screenshot capture scenarios.
    /// </summary>
    public static Bitmap RenderBackground(SizeInfo size, BackgroundStyle backgroundStyle)
    {
        // prepare the bitmap (with a transparent background) to draw the background fill onto
        var backgroundBounds = new RectangleInfo(size).ToRectangle();
        var image = new Bitmap(backgroundBounds.Width, backgroundBounds.Height, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(image);
        graphics.Clear(Color.Transparent);

        // pick a brush matching the configured background - a two-colour gradient, a solid fill
        // (whichever single colour is set), or nothing at all if neither colour is configured
        using var backgroundBrush = backgroundStyle switch
        {
            { Color1: not null, Color2: not null } =>
                /* draw a gradient fill if both colors are specified */
                new LinearGradientBrush(
                    backgroundBounds,
                    backgroundStyle.Color1.Value,
                    backgroundStyle.Color2.Value,
                    LinearGradientMode.ForwardDiagonal),
            { Color1: not null } =>
                /* draw a solid fill if only one color is specified */
                new SolidBrush(backgroundStyle.Color1.Value),
            { Color2: not null } =>
                /* draw a solid fill if only one color is specified */
                new SolidBrush(backgroundStyle.Color2.Value),
            _ => (Brush?)null,
        };

        // fill the background, if a colour was actually configured
        if (backgroundBrush is not null)
        {
            graphics.FillRectangle(backgroundBrush, backgroundBounds);
        }

        return image;
    }

    /// <summary>
    /// Renders a raised border ring for a box of the given <paramref name="size"/>.
    /// Can be used to render the main preview window border, and the individual
    /// screen bezels.
    /// </summary>
    public static Bitmap RenderBorder(SizeInfo size, BorderStyle borderStyle)
    {
        // prepare the bitmap (with a transparent background) to draw the border / bezel onto
        var borderBounds = new RectangleInfo(size).ToRectangle();
        var image = new Bitmap(borderBounds.Width, borderBounds.Height, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(image);
        graphics.Clear(Color.Transparent);

        if ((borderStyle.Horizontal < 1) || (borderStyle.Vertical < 1))
        {
            return image;
        }

        if (borderStyle.Color is null)
        {
            return image;
        }

        // draw a contoured border (could be an outer border or a bezel)
        using var renderer = new BezelRenderer(borderStyle);
        renderer.DrawBezel(graphics, borderBounds.X, borderBounds.Y, borderBounds.Width, borderBounds.Height);

        return image;
    }
}
