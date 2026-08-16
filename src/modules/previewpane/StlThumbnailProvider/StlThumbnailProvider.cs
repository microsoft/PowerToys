// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

using HelixToolkit.Wpf;
using Microsoft.PowerToys.Settings.UI.Library;

using Bitmap = System.Drawing.Bitmap;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace Microsoft.PowerToys.ThumbnailHandler.Stl
{
    /// <summary>
    /// Stl Thumbnail Provider.
    /// </summary>
    public class StlThumbnailProvider
    {
        public StlThumbnailProvider(string filePath)
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
        /// Loads the Stl model into a Viewport3D and renders a bitmap of it.
        /// </summary>
        /// <param name="stream">The Stream instance for the Stl content.</param>
        /// <param name="cx">The maximum thumbnail size, in pixels.</param>
        /// <returns>A thumbnail rendered from the Stl model.</returns>
        public static Bitmap GetThumbnail(Stream stream, uint cx)
        {
            if (cx > MaxThumbnailSize || !IsSafeToParse(stream))
            {
                return null;
            }

            Bitmap thumbnail = null;

            var stlReader = new StLReader
            {
                DefaultMaterial = new DiffuseMaterial(new SolidColorBrush(DefaultMaterialColor)),
            };

            try
            {
                var model = stlReader.Read(stream);

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
            }
            catch (Exception)
            {
                return null;
            }

            return thumbnail;
        }

        /// <summary>
        /// Checks that HelixToolkit can safely dispatch the stream to its binary or ASCII STL parser.
        /// </summary>
        /// <param name="stream">The STL stream.</param>
        /// <returns><see langword="true"/> when the selected parser can make bounded forward progress.</returns>
        public static bool IsSafeToParse(Stream stream)
        {
            if (stream == null || !stream.CanRead || !stream.CanSeek || stream.Position < 0 || stream.Position >= stream.Length)
            {
                return false;
            }

            var initialPosition = stream.Position;
            try
            {
                var remainingLength = stream.Length - initialPosition;
                if (remainingLength < 84)
                {
                    return false;
                }

                stream.Position = initialPosition + 80;
                Span<byte> triangleCountBytes = stackalloc byte[sizeof(uint)];
                if (stream.Read(triangleCountBytes) != triangleCountBytes.Length)
                {
                    return false;
                }

                var triangleCount = BitConverter.ToUInt32(triangleCountBytes);
                var payloadLength = (ulong)(remainingLength - 84);

                // HelixToolkit 2.24 tries binary first and compares the payload length with a
                // 32-bit triangleCount * 50 expression. Reject an overflow that would make that
                // check succeed and then drive billions of binary facet reads.
                var helixPayloadLength = unchecked(triangleCount * 50U);
                if (payloadLength == helixPayloadLength)
                {
                    return payloadLength == 50UL * triangleCount;
                }

                // A failed binary length check rewinds the stream and dispatches to the ASCII
                // parser. Validate the line structure that parser relies on for forward progress.
                return IsSafeAsciiStl(stream, initialPosition);
            }
            finally
            {
                stream.Position = initialPosition;
            }
        }

        private static bool IsSafeAsciiStl(Stream stream, long initialPosition)
        {
            stream.Position = initialPosition;

            Span<byte> bom = stackalloc byte[3];
            if (stream.Read(bom) != bom.Length ||
                bom[0] != 0xEF ||
                bom[1] != 0xBB ||
                bom[2] != 0xBF)
            {
                stream.Position = initialPosition;
            }

            var state = AsciiStlState.OutsideFacet;
            var sawSolid = false;
            var sawEndSolid = false;
            var solidOpen = false;
            var lineLength = 0;
            var tokenLength = 0;
            var tokenComplete = false;
            var comment = false;
            var previousLineTerminatorWasCr = false;
            Span<byte> token = stackalloc byte[16];
            Span<byte> buffer = stackalloc byte[4096];

            while (true)
            {
                var bytesRead = stream.Read(buffer);
                if (bytesRead == 0)
                {
                    break;
                }

                foreach (var value in buffer[..bytesRead])
                {
                    if (value == '\n' && previousLineTerminatorWasCr)
                    {
                        previousLineTerminatorWasCr = false;
                        continue;
                    }

                    if (value == '\r' || value == '\n')
                    {
                        if (!ProcessAsciiLine(token[..tokenLength], comment, ref state, ref sawSolid, ref sawEndSolid, ref solidOpen))
                        {
                            return false;
                        }

                        lineLength = 0;
                        tokenLength = 0;
                        tokenComplete = false;
                        comment = false;
                        previousLineTerminatorWasCr = value == '\r';
                        continue;
                    }

                    previousLineTerminatorWasCr = false;

                    if (++lineLength > 4096)
                    {
                        return false;
                    }

                    if (value == '\t' || value == ' ')
                    {
                        tokenComplete |= tokenLength != 0;
                        continue;
                    }

                    if (value < 0x20 || value > 0x7E)
                    {
                        return false;
                    }

                    if (tokenLength == 0 && (value == '#' || value == '!' || value == '$'))
                    {
                        comment = true;
                    }

                    if (!tokenComplete && !comment)
                    {
                        if (tokenLength == token.Length)
                        {
                            tokenComplete = true;
                        }
                        else
                        {
                            token[tokenLength++] = value >= (byte)'A' && value <= (byte)'Z' ? (byte)(value + 32) : value;
                        }
                    }
                }
            }

            if (lineLength != 0 &&
                !ProcessAsciiLine(token[..tokenLength], comment, ref state, ref sawSolid, ref sawEndSolid, ref solidOpen))
            {
                return false;
            }

            return state == AsciiStlState.OutsideFacet && sawSolid && sawEndSolid && !solidOpen;
        }

        private static bool ProcessAsciiLine(
            ReadOnlySpan<byte> token,
            bool comment,
            ref AsciiStlState state,
            ref bool sawSolid,
            ref bool sawEndSolid,
            ref bool solidOpen)
        {
            if (comment || token.IsEmpty)
            {
                return true;
            }

            switch (state)
            {
                case AsciiStlState.OutsideFacet:
                    if (token.SequenceEqual("solid"u8))
                    {
                        if (solidOpen)
                        {
                            return false;
                        }

                        sawSolid = true;
                        solidOpen = true;
                        return true;
                    }

                    if (!solidOpen)
                    {
                        return false;
                    }

                    if (token.SequenceEqual("facet"u8))
                    {
                        state = AsciiStlState.ExpectOuterLoop;
                    }
                    else if (token.SequenceEqual("endsolid"u8))
                    {
                        sawEndSolid = true;
                        solidOpen = false;
                    }

                    return true;

                case AsciiStlState.ExpectOuterLoop:
                    if (!token.SequenceEqual("outer"u8))
                    {
                        return false;
                    }

                    state = AsciiStlState.InLoop;
                    return true;

                case AsciiStlState.InLoop:
                    if (token.SequenceEqual("endloop"u8))
                    {
                        state = AsciiStlState.ExpectEndFacet;
                    }

                    return true;

                case AsciiStlState.ExpectEndFacet:
                    if (!token.SequenceEqual("endfacet"u8))
                    {
                        return false;
                    }

                    state = AsciiStlState.OutsideFacet;
                    return true;

                default:
                    return false;
            }
        }

        private enum AsciiStlState
        {
            OutsideFacet,
            ExpectOuterLoop,
            InLoop,
            ExpectEndFacet,
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

            if (global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredStlThumbnailsEnabledValue() == global::PowerToys.GPOWrapper.GpoRuleConfigured.Disabled)
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

                    var colorString = moduleSettings.GetSettings<PowerPreviewSettings>(PowerPreviewSettings.ModuleName).Properties.StlThumbnailColor.Value;

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
