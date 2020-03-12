// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImageResizer.Test
{
    internal static class BitmapSourceExtensions
    {
        public static Color GetFirstPixel(this BitmapSource source)
        {
            var pixel = new byte[4];
            new FormatConvertedBitmap(
                    new CroppedBitmap(source, new Int32Rect(0, 0, 1, 1)),
                    PixelFormats.Bgra32,
                    destinationPalette: null,
                    alphaThreshold: 0)
                .CopyPixels(pixel, 4, 0);

            return Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]);
        }
    }
}
