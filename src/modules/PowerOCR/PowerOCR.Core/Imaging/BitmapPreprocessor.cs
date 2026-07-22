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

        return new PreparedBitmap(output, scale, dimensions.Offset.X, dimensions.Offset.Y);
    }

    private static (Size Scaled, Size Output, Point Offset) CalculateDimensions(Bitmap source, double scale)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(scale, 0);

        int scaledWidth = (int)Math.Round(source.Width * scale);
        int scaledHeight = (int)Math.Round(source.Height * scale);
        bool requiresPadding = scaledWidth < MinimumDimension || scaledHeight < MinimumDimension;
        int outputWidth = requiresPadding
            ? Math.Max(scaledWidth + (Padding * 2), MinimumDimension + (Padding * 2))
            : scaledWidth;
        int outputHeight = requiresPadding
            ? Math.Max(scaledHeight + (Padding * 2), MinimumDimension + (Padding * 2))
            : scaledHeight;
        int offsetX = requiresPadding ? Padding : 0;
        int offsetY = requiresPadding ? Padding : 0;

        return (
            new Size(scaledWidth, scaledHeight),
            new Size(outputWidth, outputHeight),
            new Point(offsetX, offsetY));
    }
}
