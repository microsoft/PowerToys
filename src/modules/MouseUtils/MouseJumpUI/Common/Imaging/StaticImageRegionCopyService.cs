// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using MouseJumpUI.Common.Models.Drawing;

namespace MouseJumpUI.Common.Imaging;

/// <summary>
/// Implements an IImageRegionCopyService that uses the specified image as the copy source.
/// This is used for testing the DrawingHelper rather than as part of the main application.
/// </summary>
internal sealed class StaticImageRegionCopyService : IImageRegionCopyService
{
    public StaticImageRegionCopyService(Image sourceImage)
    {
        this.SourceImage = sourceImage ?? throw new ArgumentNullException(nameof(sourceImage));
    }

    private Image SourceImage
    {
        get;
    }

    /// <summary>
    /// Copies the source region from the static source image
    /// to the target region on the specified Graphics object.
    /// </summary>
    public void CopyImageRegion(
        Graphics targetGraphics,
        RectangleInfo sourceBounds,
        RectangleInfo targetBounds)
    {
        targetGraphics.DrawImage(
            image: this.SourceImage,
            destRect: targetBounds.ToRectangle(),
            srcRect: sourceBounds.ToRectangle(),
            srcUnit: GraphicsUnit.Pixel);
    }
}
