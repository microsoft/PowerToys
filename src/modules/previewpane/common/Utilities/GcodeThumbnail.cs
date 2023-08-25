// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using QOI.Core;

namespace Common.Utilities
{
    /// <summary>
    /// Represents a gcode thumbnail.
    /// </summary>
    public class GcodeThumbnail
    {
        /// <summary>
        /// Gets the gcode thumbnail image format.
        /// </summary>
        public GcodeThumbnailFormat Format { get; }

        /// <summary>
        /// Gets the gcode thumbnail image data in base64.
        /// </summary>
        public string Data { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GcodeThumbnail"/> class.
        /// </summary>
        /// <param name="format">The gcode thumbnail image format.</param>
        /// <param name="data">The gcode thumbnail image data in base64.</param>
        public GcodeThumbnail(GcodeThumbnailFormat format, string data)
        {
            Format = format;
            Data = data;
        }

        /// <summary>
        /// Gets a <see cref="Bitmap"/> representing this thumbnail.
        /// </summary>
        /// <returns>A <see cref="Bitmap"/> representing this thumbnail.</returns>
        public Bitmap? GetBitmap()
        {
            switch (Format)
            {
                case GcodeThumbnailFormat.JPG:
                case GcodeThumbnailFormat.PNG:
                    return BitmapFromBase64String();

                case GcodeThumbnailFormat.QOI:
                    return BitmapFromQoiBase64String();

                default:
                    return null;
            }
        }

        private Bitmap BitmapFromBase64String()
        {
            var bitmapBytes = Convert.FromBase64String(Data);

            return new Bitmap(new MemoryStream(bitmapBytes));
        }

        private Bitmap BitmapFromQoiBase64String()
        {
            var bitmapBytes = Convert.FromBase64String(Data);

            var qoiDecoder = new QoiDecoder();
            var qoiImage = qoiDecoder.Read(new MemoryStream(bitmapBytes));

            var pixelFormat = qoiImage.HasAlpha ? PixelFormat.Format32bppArgb : PixelFormat.Format24bppRgb;
            var bitmap = new Bitmap((int)qoiImage.Width, (int)qoiImage.Height, pixelFormat);

            var bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.WriteOnly, pixelFormat);
            var pixelSize = qoiImage.HasAlpha ? 4 : 3;

            unsafe
            {
                for (int index = 0; index < qoiImage.Pixels.Length; index++)
                {
                    var qoiPixel = qoiImage.Pixels[index];
                    var pixel = (byte*)bitmapData.Scan0 + (index * pixelSize);

                    pixel[0] = qoiPixel.B;
                    pixel[1] = qoiPixel.G;
                    pixel[2] = qoiPixel.R;
                    if (qoiImage.HasAlpha)
                    {
                        pixel[3] = qoiPixel.A;
                    }
                }
            }

            bitmap.UnlockBits(bitmapData);

            return bitmap;
        }
    }
}
