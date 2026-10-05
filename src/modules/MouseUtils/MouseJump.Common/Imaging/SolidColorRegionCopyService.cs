// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Drawing.Drawing2D;

using MouseJump.Models.Drawing;

namespace MouseJump.Common.Imaging;

/// <summary>
/// Implements an IImageRegionCopyService that paints a solid block of the specified color.
/// This is used for testing the DrawingHelper rather than as part of the main application.
/// </summary>
public sealed class SolidColorRegionCopyService : IImageRegionCopyService
{
    public SolidColorRegionCopyService(Color color)
    {
        this.Color = color;
    }

    private Color Color
    {
        get;
    }

    /// <summary>
    /// Draws a region in a solid color.
    /// </summary>
    public void CopyImageRegion(
        Graphics targetGraphics,
        RectangleInfo sourceBounds,
        RectangleInfo targetBounds)
    {
        using var brush = new SolidBrush(this.Color);

        // prevent the background bleeding through into screen images
        // (see https://github.com/mikeclayton/FancyMouse/issues/44)
        targetGraphics.PixelOffsetMode = PixelOffsetMode.Half;
        targetGraphics.InterpolationMode = InterpolationMode.NearestNeighbor;

        targetGraphics.FillRectangle(
            brush: brush,
            rect: targetBounds.ToRectangle());
    }
}
