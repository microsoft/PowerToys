// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.IO;

namespace Microsoft.PowerToys.FilePreviewCommon
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

            return QoiImage.FromStream(new MemoryStream(bitmapBytes));
        }
    }
}
