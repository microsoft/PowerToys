// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System.Drawing.Drawing2D;
using System.Text;

namespace Microsoft.PowerToys.ThumbnailHandler.Gcode
{
    /// <summary>
    /// G-code Thumbnail Provider.
    /// </summary>
    public class GcodeThumbnailProvider
    {
        public GcodeThumbnailProvider(string filePath)
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
        /// Reads the G-code content searching for thumbnails and returns the largest.
        /// </summary>
        /// <param name="reader">The TextReader instance for the G-code content.</param>
        /// <param name="cx">The maximum thumbnail size, in pixels.</param>
        /// <returns>A thumbnail extracted from the G-code content.</returns>
        public static Bitmap GetThumbnail(TextReader reader, uint cx)
        {
            if (cx > MaxThumbnailSize || reader == null)
            {
                return null;
            }

            Bitmap thumbnail = null;

            var bitmapBase64 = GetBase64Thumbnails(reader)
                .OrderByDescending(x => x.Length)
                .FirstOrDefault();

            if (!string.IsNullOrEmpty(bitmapBase64))
            {
                var bitmapBytes = Convert.FromBase64String(bitmapBase64);

                thumbnail = new Bitmap(new MemoryStream(bitmapBytes));

                if (thumbnail.Width != cx && thumbnail.Height != cx)
                {
                    // We are not the appropriate size for caller.  Resize now while
                    // respecting the aspect ratio.
                    float scale = Math.Min((float)cx / thumbnail.Width, (float)cx / thumbnail.Height);
                    int scaleWidth = (int)(thumbnail.Width * scale);
                    int scaleHeight = (int)(thumbnail.Height * scale);
                    thumbnail = ResizeImage(thumbnail, scaleWidth, scaleHeight);
                }
            }

            return thumbnail;
        }

        /// <summary>
        /// Gets all thumbnails in base64 format found on the G-code data.
        /// </summary>
        /// <param name="reader">The TextReader instance for the G-code content.</param>
        /// <returns>An enumeration of thumbnails in base64 format found on the G-code.</returns>
        private static IEnumerable<string> GetBase64Thumbnails(TextReader reader)
        {
            string line;
            StringBuilder capturedText = null;

            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith("; thumbnail begin", StringComparison.InvariantCulture))
                {
                    capturedText = new StringBuilder();
                }
                else if (line == "; thumbnail end")
                {
                    if (capturedText != null)
                    {
                        yield return capturedText.ToString();

                        capturedText = null;
                    }
                }
                else if (capturedText != null)
                {
                    capturedText.Append(line[2..]);
                }
            }
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

            Bitmap destImage = new Bitmap(width, height);

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

            return destImage;
        }

        /// <summary>
        /// Generate thumbnail bitmap for provided Gcode file/stream.
        /// </summary>
        /// <param name="cx">Maximum thumbnail size, in pixels.</param>
        /// <returns>Generated bitmap</returns>
        public Bitmap GetThumbnail(uint cx)
        {
            if (cx == 0 || cx > MaxThumbnailSize)
            {
                return null;
            }

            if (global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredGcodeThumbnailsEnabledValue() == global::PowerToys.GPOWrapper.GpoRuleConfigured.Disabled)
            {
                // GPO is disabling this utility.
                return null;
            }

            using (var reader = new StreamReader(this.Stream))
            {
                Bitmap thumbnail = GetThumbnail(reader, cx);
                if (thumbnail != null && thumbnail.Size.Width > 0 && thumbnail.Size.Height > 0)
                {
                    return thumbnail;
                }
            }

            return null;
        }
    }
}
