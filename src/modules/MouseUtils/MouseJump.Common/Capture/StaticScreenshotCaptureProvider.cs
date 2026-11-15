// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

using MouseJump.Models.Drawing;

namespace MouseJump.Common.Capture;

/// <summary>
/// Implements an <see cref="IScreenshotCaptureProvider"/> that captures from a fixed source
/// image instead of the interactive desktop. This is used for testing <see cref="Helpers.DrawingHelper"/>
/// and the preview capture pipeline rather than as part of the main application.
/// </summary>
public sealed class StaticScreenshotCaptureProvider : IScreenshotCaptureProvider
{
    public StaticScreenshotCaptureProvider(Image sourceImage)
    {
        this.SourceImage = sourceImage ?? throw new ArgumentNullException(nameof(sourceImage));
    }

    private Image SourceImage
    {
        get;
    }

    public Task<Bitmap> CaptureAsync(
        RectangleInfo sourceArea,
        SizeInfo thumbnailSize,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var target = thumbnailSize.Round().ToSize();
        var thumbnailImage = new Bitmap(target.Width, target.Height, PixelFormat.Format32bppPArgb);
        using (var thumbnailGraphics = Graphics.FromImage(thumbnailImage))
        {
            // prevent the background bleeding through into screen images
            // (see https://github.com/mikeclayton/FancyMouse/issues/44)
            thumbnailGraphics.PixelOffsetMode = PixelOffsetMode.Half;
            thumbnailGraphics.InterpolationMode = InterpolationMode.NearestNeighbor;

            thumbnailGraphics.DrawImage(
                image: this.SourceImage,
                destRect: new Rectangle(0, 0, target.Width, target.Height),
                srcRect: sourceArea.ToRectangle(),
                srcUnit: GraphicsUnit.Pixel);
        }

        return Task.FromResult(thumbnailImage);
    }
}
