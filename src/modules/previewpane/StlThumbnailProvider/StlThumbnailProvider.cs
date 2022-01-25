// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Common.ComInterlop;
using Common.Utilities;
using HelixToolkit.Wpf;
using Bitmap = System.Drawing.Bitmap;

namespace Microsoft.PowerToys.ThumbnailHandler.Stl
{
    /// <summary>
    /// Stl Thumbnail Provider.
    /// </summary>
    [Guid("8BC8AFC2-4E7C-4695-818E-8C1FFDCEA2AF")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    public class StlThumbnailProvider : IInitializeWithStream, IThumbnailProvider
    {
        /// <summary>
        /// Gets the stream object to access file.
        /// </summary>
        public IStream Stream { get; private set; }

        /// <summary>
        ///  The maximum dimension (width or height) thumbnail we will generate.
        /// </summary>
        private const uint MaxThumbnailSize = 10000;

        /// <summary>
        /// Loads the Stl model into a Viewport3D and renders a bitmap of it.
        /// </summary>
        /// <param name="stream">The Stream instance for the Stl content.</param>
        /// <param name="cx">The maximum thumbnail size, in pixels.</param>
        /// <returns>A thumbnail rendered from the Stl model.</returns>
        public static Bitmap GetThumbnail(Stream stream, uint cx)
        {
            if (cx > MaxThumbnailSize || stream == null || stream.Length == 0)
            {
                return null;
            }

            Bitmap thumbnail = null;

            var stlReader = new StLReader
            {
                DefaultMaterial = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(255, 201, 36))),
            };

            var model = stlReader.Read(stream);

            if (model.Bounds == Rect3D.Empty)
            {
                return null;
            }

            var viewport = new System.Windows.Controls.Viewport3D();

            viewport.Measure(new Size(cx, cx));
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

            return thumbnail;
        }

        /// <inheritdoc/>
        public void Initialize(IStream pstream, uint grfMode)
        {
            // Ignore the grfMode always use read mode to access the file.
            this.Stream = pstream;
        }

        /// <inheritdoc/>
        public void GetThumbnail(uint cx, out IntPtr phbmp, out WTS_ALPHATYPE pdwAlpha)
        {
            phbmp = IntPtr.Zero;
            pdwAlpha = WTS_ALPHATYPE.WTSAT_UNKNOWN;

            if (cx == 0 || cx > MaxThumbnailSize)
            {
                return;
            }

            using (var stream = new ReadonlyStream(this.Stream as IStream))
            {
                using (var memStream = new MemoryStream())
                {
                    stream.CopyTo(memStream);

                    memStream.Position = 0;

                    using (Bitmap thumbnail = GetThumbnail(memStream, cx))
                    {
                        if (thumbnail != null && thumbnail.Size.Width > 0 && thumbnail.Size.Height > 0)
                        {
                            phbmp = thumbnail.GetHbitmap(System.Drawing.Color.Transparent);
                            pdwAlpha = WTS_ALPHATYPE.WTSAT_ARGB;
                        }
                    }
                }
            }
        }
    }
}
