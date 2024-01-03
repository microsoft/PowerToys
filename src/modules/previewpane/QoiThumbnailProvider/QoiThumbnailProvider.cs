// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Microsoft.PowerToys.FilePreviewCommon;

namespace Microsoft.PowerToys.ThumbnailHandler.Qoi
{
    /// <summary>
    /// Qoi Thumbnail Provider.
    /// </summary>
    public class QoiThumbnailProvider
    {
        public QoiThumbnailProvider(string filePath)
        {
            FilePath = filePath;
            Stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        }

        /// <summary>
        /// Gets the file path to the file creating thumbnail for.
        /// </summary>
        public string FilePath { get; private set; }

        /// <summary>
        /// Gets the stream object to access file.
        /// </summary>
        public Stream Stream { get; private set; }

        /// <summary>
        ///  The maximum dimension (width or height) thumbnail we will generate.
        /// </summary>
        private const uint MaxThumbnailSize = 10000;

        /// <summary>
        /// Generate thumbnail bitmap for provided Qoi stream.
        /// </summary>
        /// <param name="stream">The Stream instance for the Qoi bitmap.</param>
        /// <param name="cx">The maximum thumbnail size, in pixels.</param>
        /// <returns>A thumbnail rendered from the Qoi bitmap.</returns>
        public static Bitmap GetThumbnail(Stream stream, uint cx)
        {
            if (cx > MaxThumbnailSize || stream == null || stream.Length == 0)
            {
                return null;
            }

            Bitmap thumbnail = null;
            try
            {
                thumbnail = QoiImage.FromStream(stream);
            }
            catch (Exception)
            {
                // TODO: add logger
            }

            if (thumbnail != null && (
                ((thumbnail.Width != cx || thumbnail.Height > cx) && (thumbnail.Height != cx || thumbnail.Width > cx)) ||
                thumbnail.PixelFormat != PixelFormat.Format32bppArgb))
            {
                // We are not the appropriate size for caller.  Resize now while
                // respecting the aspect ratio.
                float scale = Math.Min((float)cx / thumbnail.Width, (float)cx / thumbnail.Height);
                int scaleWidth = (int)(thumbnail.Width * scale);
                int scaleHeight = (int)(thumbnail.Height * scale);
                thumbnail = ResizeImage(thumbnail, scaleWidth, scaleHeight);
            }

            return thumbnail;
        }

        /// <summary>
        /// Resize the image with high quality to the specified width and height.
        /// </summary>
        /// <param name="image">The image to resize.</param>
        /// <param name="width">The width to resize to.</param>
        /// <param name="height">The height to resize to.</param>
        /// <returns>The resized image.</returns>
        public static Bitmap ResizeImage(Image image, int width, int height)
        {
            if (width <= 0 ||
                height <= 0 ||
                width > MaxThumbnailSize ||
                height > MaxThumbnailSize ||
                image == null)
            {
                return null;
            }

            Bitmap destImage = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                graphics.DrawImage(image, 0, 0, width, height);
            }

            image.Dispose();

            return destImage;
        }

        /// <summary>
        /// Generate thumbnail bitmap for provided Qoi file/stream.
        /// </summary>
        /// <param name="cx">Maximum thumbnail size, in pixels.</param>
        /// <returns>Generated bitmap</returns>
        public Bitmap GetThumbnail(uint cx)
        {
            if (cx == 0 || cx > MaxThumbnailSize)
            {
                return null;
            }

            if (global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredQoiThumbnailsEnabledValue() == global::PowerToys.GPOWrapper.GpoRuleConfigured.Disabled)
            {
                // GPO is disabling this utility.
                return null;
            }

            Bitmap thumbnail = GetThumbnail(this.Stream, cx);
            if (thumbnail != null && thumbnail.Size.Width > 0 && thumbnail.Size.Height > 0)
            {
                return thumbnail;
            }

            return null;
        }
    }
}
