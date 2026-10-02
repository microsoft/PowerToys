// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Drawing.Drawing2D;

namespace MouseJump.Common.Bezels;

public static class BezelGraphics
{
    /// <summary>
    /// Enables antialiased drawing on <paramref name="g"/>, with pixel offsets
    /// aligned to whole pixels rather than fractional (0.5, 0.5) coordinates.
    /// </summary>
    private static void EnableAntialias(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.Half;
    }

    /// <summary>
    /// Creates a <see cref="GraphicsPath"/> for a rectangle with rounded corners.
    /// The caller owns the returned path and is responsible for disposing the return value.
    /// </summary>
    private static GraphicsPath GetRoundedRectanglePath(int x, int y, int width, int height, int cornerRadius)
    {
        var path = new GraphicsPath();
        if (cornerRadius > 0)
        {
            var d = 2 * cornerRadius;
            path.AddArc(x,             y,              d, d, 180, 90); // TL
            path.AddArc(x + width - d, y,              d, d, 270, 90); // TR
            path.AddArc(x + width - d, y + height - d, d, d,   0, 90); // BR
            path.AddArc(x,             y + height - d, d, d,  90, 90); // BL
        }
        else
        {
            path.AddRectangle(new Rectangle(x, y, width, height));
        }

        path.CloseFigure();
        return path;
    }

    /// <summary>
    /// Draws a 1-pixel thick, 3-stage gradient line as part of a bezel's edge.
    /// </summary>
    /// <remarks>
    /// The line starts at (x1, y1) using strongColour and fades to fadeColour as it approaches (x2, y2).
    /// The 3-stage gradient is asymmetrical - to reverse the direction swap (x1, y1) and (x2, y2)
    /// (don't just swap the colours).
    /// </remarks>
    /// <param name="strongColor">The more intense colour, at the corner-adjacent end of the line.</param>
    /// <param name="fadeColor">The colour the line fades to, away from the corner.</param>
    private static void DrawBezelEdgeLine(
        Graphics g,
        int x1,
        int y1,
        int x2,
        int y2,
        Color strongColor,
        Color fadeColor)
    {
        // make sure the coordinates represent a vertical or horizontal line,
        // not an arbitrary rectangle - each pixel row (or column) of the bezel
        // edge needs to be rendered separately to account for the bezel profile.
        if ((x1 != x2) && (y1 != y2))
        {
            throw new ArgumentException("Coordinates must represent a single-pixel line, not a rectangle - i.e. x1 == x2 or y1 == y2.");
        }

        // the gradient is asymmetrical, but the direction is controlled by the
        // brush not the drawing bounds, so we'll normalise the coordinates here
        var edgeBounds = (y1 == y2)
            ? new RectangleF(Math.Min(x1, x2), y1, Math.Abs(x2 - x1), 1f)
            : new RectangleF(x1, Math.Min(y1, y2), 1f, Math.Abs(y2 - y1));

        var savedMode = g.SmoothingMode;
        var savedPixelOffset = g.PixelOffsetMode;

        g.SmoothingMode = SmoothingMode.None;
        g.PixelOffsetMode = PixelOffsetMode.None;

        // set a default 2-stop gradient brush; this is overwritten with the full 4-stop
        // gradient below if BezelConstants.EdgeGradientStart/EdgeGradientEnd are ever
        // changed to invalid values, so rendering degrades instead of throwing.
        // (note the coordinates are directional to ensure the gradient blends the right way)
        using var brush = new LinearGradientBrush(
            new Point(x1, y1), new Point(x2, y2), strongColor, fadeColor);

        // for gradient fills, the default WrapMode.Tile fills the gradient region as
        // a series of tiles and it antialiases the edge where they join - this means
        // the *start* color of the gradient from the neighbouring tile can bleed into
        // the *end* color of the adjoining tile:
        //
        //     |▓▓▓▓▒▒▒▒░░░░   ▒|▓▓▓▓▒▒▒▒░░░░   ▒|▓▓▓▓▒▒▒▒░░░░   ▒|▓▓▓▓▒▒▒▒░░░░   ▒|
        //                     ^
        //                     WrapMode.Tile simply repeats the gradient brush,
        //                     and anti-aliasing of a high contrast pixel in the
        //                     next (off-screen) tile bleeds back over into the
        //                     previous tile
        //
        // in our case it means the *start* color for the neighbouring (off-screen) tile
        // can bleed into the *end* color of our edge region, causing a 1-pixel wide
        // rendering artifact on the resulting image.
        //
        // to prevent this, TileFlipXY *mirrors* the gradient at both ends so antialiasing
        // with the neighboring tile uses the same colour at the join and there's no
        // rendering artifact.
        //
        //     |▓▓▓▓▒▒▒▒░░░░    |    ░░░░▒▒▒▒▓▓▓▓|▓▓▓▓▒▒▒▒░░░░    |    ░░░░▒▒▒▒▓▓▓▓|
        //                     ^
        //                     WrapMode.TileFlipXy *mirrors* the gradient brush
        //                     so anti-aliasing blends two pixels of the same color
        //                     and there's no visible bleed effect between tiles
        brush.WrapMode = WrapMode.TileFlipXY;

        // only draw lighting effects fade for valid gradient stepping points
        if ((BezelConstants.EdgeGradientEnd > BezelConstants.EdgeGradientStart) && (BezelConstants.EdgeGradientEnd < 1f))
        {
            // overall, the shape of the resulting gradient is 3 stages,
            // transitioning at <EdgeGradientStart>% and <EdgeGradientEnd>%:
            //
            //     * stage 1 - solid:    strongColor
            //     * stage 2 - gradient: strongColor -> fadeColor
            //     * stage 3 - solid:    fadeColor
            //
            //     |solid|            gradient             |  solid  |
            //     |▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▒▒▒▒▒▒▒▒▒▒▒▒░░░░░░░░░░░░---------|
            //     0     ^                                 ^         1.0
            //           |                                 |
            //           EdgeGradientStart                 EdgeGradientEnd
            //
            //     * the first solid color is held flat at strongColor to avoid a
            //       visually jarring gradient starting straight away
            //
            //     * the gradient fades from strongColor to fadeColor
            //
            //     * the final solid color is held flat at fadeColor for the rest
            //       of the edge
            brush.InterpolationColors = new ColorBlend(4)
            {
                Colors = [strongColor, strongColor, fadeColor, fadeColor],
                Positions = [0f, BezelConstants.EdgeGradientStart, BezelConstants.EdgeGradientEnd, 1f],
            };
        }

        g.FillRectangle(brush, edgeBounds);

        g.SmoothingMode = savedMode;
        g.PixelOffsetMode = savedPixelOffset;
    }

    /// <summary>
    /// Draws the straight edge rectangles for a bezel ring.
    /// Each edge is drawn as a series of 1-pixel thick lines
    /// with highlight and shadow effect according to the bezel profile.
    /// </summary>
    internal static void DrawBezelEdges(
        Graphics g,
        int x,
        int y,
        int width,
        int height,
        int bezelWidth,
        Color bezelColor,
        IBezelProfile bezelProfile)
    {
        var n = bezelWidth;

        // draw the four straight edge strips with the flat border color first
        var savedMode = g.SmoothingMode;
        var savedPixelOffset = g.PixelOffsetMode;
        g.SmoothingMode = SmoothingMode.None;
        g.PixelOffsetMode = PixelOffsetMode.None;
        using (var flatBrush = new SolidBrush(bezelColor))
        {
            g.FillRectangle(flatBrush, x + n,         y,              width - (2 * n), n);                // top
            g.FillRectangle(flatBrush, x + n,         y + height - n, width - (2 * n), n);                // bottom
            g.FillRectangle(flatBrush, x,             y + n,          n,               height - (2 * n)); // left
            g.FillRectangle(flatBrush, x + width - n, y + n,          n,               height - (2 * n)); // right
        }

        g.SmoothingMode = savedMode;
        g.PixelOffsetMode = savedPixelOffset;

        // pre-compute the vertical and horizontal endpoints for the edge lines
        var horizontalEdgeX1 = x + n;         // left   end of top  / bottom horizontal segments
        var horizontalEdgeX2 = x + width - n; // right  end of top  / bottom horizontal segments
        var verticalEdgeY1 = y + n;           // top    end of left / right  vertical  segments
        var verticalEdgeY2 = y + height - n;  // bottom end of left / right  vertical  segments

        Color GetHighlightColor(double strength, double magnitude) => BezelPrimitives.ApplyHighlight(bezelColor, strength * magnitude, BezelConstants.HighlightMax);

        Color GetShadowColor(double strength, double magnitude) => BezelPrimitives.ApplyShadow(bezelColor, strength * magnitude, BezelConstants.ShadowMax);

        // positions with a magnitude below this are in the flat zone - already
        // covered by the flat fill above, so no line is drawn for them at all.
        const double flatEdgeThreshold = 1e-10;

        // iterate across the thickness of the border one pixel at a time and draw
        // a layer of the bezel "ring" at each position. we work from the outside
        // in on all four sides - the bezel profile faces "outward" on each side
        // so the top and left edges face *toward* the light source while the
        // bottom and right edges face *away* from it (i.e. they're reversed).
        // as a result we need to calculate two sets of profile values ("forward"
        // and "reverse") for each iteration.
        for (var pos = 0; pos < n; pos++)
        {
            // calculate the normal, intensity and magnitude for the Top and Left edges
            // (the edge of these borders at 0 on the profile function faces upward / leftward)
            var forwardNormal = bezelProfile.GetEdgeNormal(n, pos);
            var forwardIntensity = BezelProfile.GetLightingEffectIntensity(forwardNormal);
            var frontFacingLight = forwardIntensity > 0.0;
            var forwardMagnitude = Math.Abs(forwardIntensity);

            // calculate the normal, intensity and magnitude for the Bottom and Right edges
            // (the edge of these borders at 0 on the profile function faces downward / rightward
            // so the normal is reversed 180 degrees (or Math.PI radians) from the Top and Left edges above)
            var reverseNormal = Math.PI - forwardNormal;
            var reverseIntensity = BezelProfile.GetLightingEffectIntensity(reverseNormal);
            var backFacingLight = reverseIntensity > 0.0;
            var reverseMagnitude = Math.Abs(reverseIntensity);

            // calculate the coordinates for the individual lines of the border edges
            // that we're drawing in this iteration
            var topLineY = y + pos;
            var bottomLineY = y + height - pos - 1;
            var leftLineX = x + pos;
            var rightLineX = x + width - pos - 1;

            // Top:    HL when facing light, SH when facing away (left→right)
            if (forwardMagnitude >= flatEdgeThreshold)
            {
                BezelGraphics.DrawBezelEdgeLine(
                    g: g,
                    x1: horizontalEdgeX1,
                    y1: topLineY,
                    x2: horizontalEdgeX2,
                    y2: topLineY,
                    strongColor: frontFacingLight ? GetHighlightColor(BezelConstants.EdgeForwardHighlightStrong, forwardMagnitude) : GetShadowColor(BezelConstants.EdgeForwardShadowStrong, forwardMagnitude),
                    fadeColor: frontFacingLight ? GetHighlightColor(BezelConstants.EdgeForwardHighlightFade, forwardMagnitude) : GetShadowColor(BezelConstants.EdgeForwardShadowFade, forwardMagnitude));
            }

            // Right:  HL when facing light, SH when facing away (bottom→top)
            if (reverseMagnitude >= flatEdgeThreshold)
            {
                BezelGraphics.DrawBezelEdgeLine(
                    g: g,
                    x1: rightLineX,
                    y1: verticalEdgeY2,
                    x2: rightLineX,
                    y2: verticalEdgeY1,
                    strongColor: backFacingLight ? GetHighlightColor(BezelConstants.EdgeReverseHighlightStrong, reverseMagnitude) : GetShadowColor(BezelConstants.EdgeReverseShadowStrong, reverseMagnitude),
                    fadeColor: backFacingLight ? GetHighlightColor(BezelConstants.EdgeReverseHighlightFade, reverseMagnitude) : GetShadowColor(BezelConstants.EdgeReverseShadowFade, reverseMagnitude));
            }

            // Bottom: HL when facing light, SH when facing away (right→left)
            if (reverseMagnitude >= flatEdgeThreshold)
            {
                BezelGraphics.DrawBezelEdgeLine(
                    g: g,
                    x1: horizontalEdgeX2,
                    y1: bottomLineY,
                    x2: horizontalEdgeX1,
                    y2: bottomLineY,
                    strongColor: backFacingLight ? GetHighlightColor(BezelConstants.EdgeReverseHighlightStrong, reverseMagnitude) : GetShadowColor(BezelConstants.EdgeReverseShadowStrong, reverseMagnitude),
                    fadeColor: backFacingLight ? GetHighlightColor(BezelConstants.EdgeReverseHighlightFade, reverseMagnitude) : GetShadowColor(BezelConstants.EdgeReverseShadowFade, reverseMagnitude));
            }

            // Left:   HL when facing light, SH when facing away (top→bottom)
            if (forwardMagnitude >= flatEdgeThreshold)
            {
                BezelGraphics.DrawBezelEdgeLine(
                    g: g,
                    x1: leftLineX,
                    y1: verticalEdgeY1,
                    x2: leftLineX,
                    y2: verticalEdgeY2,
                    strongColor: frontFacingLight ? GetHighlightColor(BezelConstants.EdgeForwardHighlightStrong, forwardMagnitude) : GetShadowColor(BezelConstants.EdgeForwardShadowStrong, forwardMagnitude),
                    fadeColor: frontFacingLight ? GetHighlightColor(BezelConstants.EdgeForwardHighlightFade, forwardMagnitude) : GetShadowColor(BezelConstants.EdgeForwardShadowFade, forwardMagnitude));
            }
        }
    }

    /// <summary>
    /// Draws a solid colored bezel "ring" onto <paramref name="g"/> at position (x, y)
    /// with size (width × height), filled with <paramref name="bezelColor"/> and no 3-D effect.
    ///
    /// The outer boundary is a rounded rectangle with radius <paramref name="cornerRadius"/>.
    /// The inner boundary is a plain square-cornered rectangle, inset by
    /// <paramref name="cornerRadius"/> on every side.
    /// </summary>
    internal static void DrawFlatBezelRing(
        Graphics g,
        int x,
        int y,
        int width,
        int height,
        int cornerRadius,
        Color bezelColor)
    {
        // fail hard on invalid geometry rather than silently drawing nothing
        if (width < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "width cannot be negative.");
        }

        if (height < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "height cannot be negative.");
        }

        if (cornerRadius < 0 || (width < 2 * cornerRadius) || (height < 2 * cornerRadius))
        {
            throw new ArgumentOutOfRangeException(nameof(cornerRadius), "cornerRadius cannot be negative, and cannot be greater than half of width and height.");
        }

        if (width == 0 || height == 0)
        {
            // a zero-width or zero-height ring has nothing to draw
            return;
        }

        var innerWidth = width - (2 * cornerRadius);
        var innerHeight = height - (2 * cornerRadius);

        BezelGraphics.EnableAntialias(g);

        using var outerPath = BezelGraphics.GetRoundedRectanglePath(x, y, width, height, cornerRadius);
        using var fillBrush = new SolidBrush(bezelColor);

        // Two-step rendering:
        //
        //   step 1 — fill the entire rounded rectangle (including the central content area rectangle)
        //   step 2 — erase the inner content area rectangle with SmoothingMode.None
        //
        //     ______________          ______________
        //    /..............\        /..............\
        //   |................|      |..+----------+..|
        //   |................|      |..|          |..|
        //   |................|      |..|          |..|
        //   |................|      |..+----------+..|
        //   |................|      |................|
        //    \--------------/        \--------------/
        //         step 1                  step 2
        //
        // note - using FillMode.Alternate to draw the "ring" in one gdi call
        // doesn't work because it leaves antialiased pixels in the corners of
        // the inner rectangle in GDI+'s PixelOffsetMode.Half coordinate space.
        //
        // The two-pass approach with a regular fill avoids the antialaising by
        // removing the entire rectangle in a separate drawing operation.
        g.FillPath(fillBrush, outerPath);

        var savedMode = g.SmoothingMode;
        var savedPixelOffset = g.PixelOffsetMode;
        var savedCompositing = g.CompositingMode;

        g.SmoothingMode = SmoothingMode.None;
        g.PixelOffsetMode = PixelOffsetMode.None;
        g.CompositingMode = CompositingMode.SourceCopy;

        using var clearBrush = new SolidBrush(Color.Transparent);

        g.FillRectangle(clearBrush, x + cornerRadius, y + cornerRadius, innerWidth, innerHeight);
        g.SmoothingMode = savedMode;
        g.PixelOffsetMode = savedPixelOffset;
        g.CompositingMode = savedCompositing;
    }
}
