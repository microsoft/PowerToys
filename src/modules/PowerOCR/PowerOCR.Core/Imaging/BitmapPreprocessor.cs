// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace PowerOCR.Core.Imaging;

public sealed class BitmapPreprocessor : IBitmapPreprocessor
{
    private const int MinimumDimension = 64;
    private const int Padding = 8;

    public Size GetOutputSize(Bitmap source, double scale)
    {
        var dimensions = CalculateDimensions(source, scale);
        return dimensions.Output;
    }

    public PreparedBitmap Prepare(Bitmap source, double scale)
    {
        var dimensions = CalculateDimensions(source, scale);
        var output = new Bitmap(dimensions.Output.Width, dimensions.Output.Height, PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(output);
        graphics.Clear(source.GetPixel(0, 0));
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.DrawImage(
            source,
            new Rectangle(dimensions.Offset, dimensions.Scaled),
            new Rectangle(Point.Empty, source.Size),
            GraphicsUnit.Pixel);

        return new PreparedBitmap(
            output,
            dimensions.Scaled.Width / (double)source.Width,
            dimensions.Scaled.Height / (double)source.Height,
            dimensions.Offset.X,
            dimensions.Offset.Y);
    }

    private static (Size Scaled, Size Output, Point Offset) CalculateDimensions(Bitmap source, double scale)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(scale, 0);

        int scaledWidth = Math.Max(1, (int)Math.Round(source.Width * scale));
        int scaledHeight = Math.Max(1, (int)Math.Round(source.Height * scale));
        bool requiresHorizontalPadding = scaledWidth < MinimumDimension;
        bool requiresVerticalPadding = scaledHeight < MinimumDimension;
        int outputWidth = requiresHorizontalPadding
            ? Math.Max(scaledWidth + (Padding * 2), MinimumDimension + (Padding * 2))
            : scaledWidth;
        int outputHeight = requiresVerticalPadding
            ? Math.Max(scaledHeight + (Padding * 2), MinimumDimension + (Padding * 2))
            : scaledHeight;
        int offsetX = requiresHorizontalPadding ? Padding : 0;
        int offsetY = requiresVerticalPadding ? Padding : 0;

        return (
            new Size(scaledWidth, scaledHeight),
            new Size(outputWidth, outputHeight),
            new Point(offsetX, offsetY));
    }
}
