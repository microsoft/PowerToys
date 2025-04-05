// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.IO;

namespace Microsoft.PowerToys.FilePreviewCommon
{
    /// <summary>
    /// Represents a bgcode thumbnail.
    /// </summary>
    public class BgcodeThumbnail
    {
        /// <summary>
        /// Gets the bgcode thumbnail image format.
        /// </summary>
        public BgcodeThumbnailFormat Format { get; }

        /// <summary>
        /// Gets the bgcode thumbnail image data.
        /// </summary>
        public byte[] Data { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BgcodeThumbnail"/> class.
        /// </summary>
        /// <param name="format">The bgcode thumbnail image format.</param>
        /// <param name="data">The bgcode thumbnail image data.</param>
        public BgcodeThumbnail(BgcodeThumbnailFormat format, byte[] data)
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
                case BgcodeThumbnailFormat.JPG:
                case BgcodeThumbnailFormat.PNG:
                    return BitmapFromByteArray();

                case BgcodeThumbnailFormat.QOI:
                    return BitmapFromQoiByteArray();

                default:
                    return null;
            }
        }

        private Bitmap BitmapFromByteArray()
        {
            return new Bitmap(new MemoryStream(Data));
        }

        private Bitmap BitmapFromQoiByteArray()
        {
            return QoiImage.FromStream(new MemoryStream(Data));
        }
    }
}
