// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

using HelixToolkit.Wpf;
using Microsoft.PowerToys.Settings.UI.Library;

using Bitmap = System.Drawing.Bitmap;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace Microsoft.PowerToys.ThumbnailHandler.ThreeMf
{
    /// <summary>
    /// ThreeMf Thumbnail Provider.
    /// </summary>
    public class ThreeMfThumbnailProvider
    {
        public ThreeMfThumbnailProvider(string filePath)
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
        /// Loads the ThreeMf model into a Viewport3D and renders a bitmap of it.
        /// </summary>
        /// <param name="stream">The Stream instance for the ThreeMf content.</param>
        /// <param name="cx">The maximum thumbnail size, in pixels.</param>
        /// <returns>A thumbnail rendered from the ThreeMf model.</returns>
        public static Bitmap GetThumbnail(Stream stream, uint cx)
        {
            if (cx > MaxThumbnailSize || stream == null || stream.Length == 0)
            {
                return null;
            }

            Bitmap thumbnail = null;

            try
            {
                if (stream.CanSeek)
                {
                    stream.Position = 0;
                }

                thumbnail = ThreeMfModelLoader.TryLoadEmbeddedThumbnail(stream, cx);
                if (thumbnail != null)
                {
                    return thumbnail;
                }

                if (stream.CanSeek)
                {
                    stream.Position = 0;
                }

                var model = ThreeMfModelLoader.LoadModel(stream, DefaultMaterialColor);
                if (model == null || model.Children.Count == 0 || model.Bounds == Rect3D.Empty)
                {
                    return null;
                }

                var viewport = new System.Windows.Controls.Viewport3D();

                viewport.Measure(new System.Windows.Size(cx, cx));
                viewport.Arrange(new Rect(0, 0, cx, cx));

                var modelVisual = new ModelVisual3D()
                {
                    Transform = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 0, 1), 180)),
                };
                viewport.Children.Add(modelVisual);
                viewport.Children.Add(new DefaultLights());

                var perspectiveCamera = new PerspectiveCamera
                {
                    Position = new Point3D(1, 2, 1),
                    LookDirection = new Vector3D(-1, -2, -1),
                    UpDirection = new Vector3D(0, 0, 1),
                    FieldOfView = 20,
                    NearPlaneDistance = 0.1,
                    FarPlaneDistance = double.PositiveInfinity,
                };
                viewport.Camera = perspectiveCamera;

                modelVisual.Content = model;

                perspectiveCamera.ZoomExtents(viewport);

                var bitmapExporter = new BitmapExporter
                {
                    Background = new SolidColorBrush(Colors.Transparent),
                    OversamplingMultiplier = 1,
                };

                var bitmapStream = new MemoryStream();

                bitmapExporter.Export(viewport, bitmapStream);

                bitmapStream.Position = 0;

                thumbnail = new Bitmap(bitmapStream);
                return ResizeImage(thumbnail, cx);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Resize the image with high quality to fit within the specified maximum dimension.
        /// </summary>
        /// <param name="image">The image to resize.</param>
        /// <param name="maxSize">The maximum width or height.</param>
        /// <returns>The resized image.</returns>
        public static Bitmap ResizeImage(Bitmap image, uint maxSize)
        {
            if (image == null || maxSize == 0 || maxSize > MaxThumbnailSize)
            {
                return null;
            }

            if (image.Width <= maxSize && image.Height <= maxSize && image.PixelFormat == PixelFormat.Format32bppArgb)
            {
                return image;
            }

            float scale = Math.Min((float)maxSize / image.Width, (float)maxSize / image.Height);
            int scaleWidth = (int)(image.Width * scale);
            int scaleHeight = (int)(image.Height * scale);

            if (scaleWidth <= 0 || scaleHeight <= 0)
            {
                return null;
            }

            Bitmap destImage = new Bitmap(scaleWidth, scaleHeight, PixelFormat.Format32bppArgb);
            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(image, 0, 0, scaleWidth, scaleHeight);
            }

            image.Dispose();
            return destImage;
        }

        /// <summary>
        /// Generate thumbnail bitmap for provided ThreeMf file/stream.
        /// </summary>
        /// <param name="cx">Maximum thumbnail size, in pixels.</param>
        /// <returns>Generated bitmap</returns>
        public Bitmap GetThumbnail(uint cx)
        {
            if (cx == 0 || cx > MaxThumbnailSize)
            {
                return null;
            }

            if (global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredThreeMfThumbnailsEnabledValue() == global::PowerToys.GPOWrapper.GpoRuleConfigured.Disabled)
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

        /// <summary>
        /// Gets a value indicating what color to use.
        /// </summary>
        public static Color DefaultMaterialColor
        {
            get
            {
                try
                {
                    var moduleSettings = SettingsUtils.Default;

                    var colorString = moduleSettings.GetSettings<PowerPreviewSettings>(PowerPreviewSettings.ModuleName).Properties.ThreeMfThumbnailColor.Value;

                    return (Color)ColorConverter.ConvertFromString(colorString);
                }
                catch (FileNotFoundException)
                {
                    // Couldn't read the settings.
                    // Assume default color value.
                    return Color.FromRgb(255, 201, 36);
                }
            }
        }
    }
}
