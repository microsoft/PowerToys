// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

using MouseJump.Common.Imaging;
using MouseJump.Models.Drawing;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    /// <summary>
    /// Copies regions of a static image using high quality scaling, so the
    /// downscaled wallpaper in the Mouse Jump Settings preview doesn't look pixelated.
    /// </summary>
    internal sealed class SmoothImageRegionCopyService : IImageRegionCopyService
    {
        private readonly Image _sourceImage;

        public SmoothImageRegionCopyService(Image sourceImage)
        {
            _sourceImage = sourceImage ?? throw new ArgumentNullException(nameof(sourceImage));
        }

        public void CopyImageRegion(System.Drawing.Graphics targetGraphics, RectangleInfo sourceBounds, RectangleInfo targetBounds)
        {
            var previousInterpolation = targetGraphics.InterpolationMode;
            var previousPixelOffset = targetGraphics.PixelOffsetMode;
            targetGraphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            targetGraphics.PixelOffsetMode = PixelOffsetMode.Half;

            using var attributes = new ImageAttributes();
            attributes.SetWrapMode(WrapMode.TileFlipXY);
            var source = sourceBounds.ToRectangle();
            targetGraphics.DrawImage(
                _sourceImage,
                targetBounds.ToRectangle(),
                source.X,
                source.Y,
                source.Width,
                source.Height,
                GraphicsUnit.Pixel,
                attributes);

            targetGraphics.InterpolationMode = previousInterpolation;
            targetGraphics.PixelOffsetMode = previousPixelOffset;
        }
    }
}
