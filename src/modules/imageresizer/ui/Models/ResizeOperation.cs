#pragma warning disable IDE0073, SA1636
// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/
#pragma warning restore IDE0073, SA1636

using System;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using ImageResizer.Helpers;
using ImageResizer.Properties;
using ImageResizer.Services;
using ImageResizer.Utilities;
using Microsoft.VisualBasic.FileIO;
using FileSystem = Microsoft.VisualBasic.FileIO.FileSystem;

namespace ImageResizer.Models
{
    internal class ResizeOperation
    {
        internal enum EncodeStrategy
        {
            Transcode,
            Reencode,
        }

        // The resize pipeline needs to carry the same geometry through strategy selection,
        // pixel writes, WIC metadata copy, and JPEG EXIF fix-up. Keeping it together avoids
        // subtle drift between the image dimensions written to pixels and the dimensions
        // written back into metadata.
        internal readonly record struct TransformPlan(
            int OriginalWidth,
            int OriginalHeight,
            uint ScaledWidth,
            uint ScaledHeight,
            BitmapBounds? CropBounds,
            bool NoTransformNeeded)
        {
            public int OutputWidth => NoTransformNeeded
                ? OriginalWidth
                : CropBounds.HasValue
                    ? (int)CropBounds.Value.Width
                    : (int)ScaledWidth;

            public int OutputHeight => NoTransformNeeded
                ? OriginalHeight
                : CropBounds.HasValue
                    ? (int)CropBounds.Value.Height
                    : (int)ScaledHeight;
        }

        private readonly IFileSystem _fileSystem = new System.IO.Abstractions.FileSystem();

        private readonly string _file;
        private readonly string _destinationDirectory;
        private readonly Settings _settings;
        private readonly IAISuperResolutionService _aiSuperResolutionService;

        // Cache CompositeFormat for AI error message formatting (CA1863)
        private static CompositeFormat _aiErrorFormat;

        private static CompositeFormat AiErrorFormat =>
            _aiErrorFormat ??= CompositeFormat.Parse(ResourceLoaderInstance.GetString("Error_AiProcessingFailed"));

        // These canonical property names are the best-effort WinRT/Shell fallback for paths
        // that still rely on the platform metadata writer. They intentionally cover the
        // descriptive EXIF fields users notice in Explorer, plus GPS fields needed when the
        // source metadata can be projected through the platform stack without a custom rewrite.
        private static readonly string[] KnownMetadataProperties =
        [
            "System.Photo.DateTaken",
            "System.Photo.CameraModel",
            "System.Photo.CameraManufacturer",
            "System.Photo.LensModel",
            "System.Photo.ExposureTime",
            "System.Photo.FNumber",
            "System.Photo.ISOSpeed",
            "System.Photo.ExposureBias",
            "System.Photo.MeteringMode",
            "System.Photo.Flash",
            "System.Photo.FocalLength",
            "System.Photo.WhiteBalance",
            "System.GPS.VersionID",
            "System.GPS.Latitude",
            "System.GPS.LatitudeRef",
            "System.GPS.Longitude",
            "System.GPS.LongitudeRef",
            "System.GPS.Altitude",
            "System.GPS.AltitudeRef",
            "System.Photo.Orientation",
            "System.Image.ColorSpace",
            "System.Comment",
            "System.Author",
            "System.Copyright",
        ];

        // These raw query paths are narrower than a full metadata clone on purpose. They are
        // the EXIF/GPS values that remain valid after a resize; structural tags such as image
        // dimensions, offsets, and thumbnail references are rewritten or removed elsewhere.
        private static readonly string[] KnownWritableMetadataQueryPaths =
        [
            $"/app1/ifd/exif/{{ushort={MetadataTagIds.Exif.DateTakenOriginal}}}",
            $"/app1/ifd/exif/{{ushort={MetadataTagIds.Exif.DateTakenDigitized}}}",
            $"/app1/ifd/{{ushort={MetadataTagIds.Ifd.DateTime}}}",
            $"/app1/ifd/{{ushort={MetadataTagIds.Ifd.Make}}}",
            $"/app1/ifd/{{ushort={MetadataTagIds.Ifd.Model}}}",
            $"/app1/ifd/exif/{{ushort={MetadataTagIds.Exif.LensModel}}}",
            $"/app1/ifd/exif/{{ushort={MetadataTagIds.Exif.ExposureTime}}}",
            $"/app1/ifd/exif/{{ushort={MetadataTagIds.Exif.FNumber}}}",
            $"/app1/ifd/exif/{{ushort={MetadataTagIds.Exif.IsoSpeed}}}",
            $"/app1/ifd/exif/{{ushort={MetadataTagIds.Exif.ExposureBias}}}",
            $"/app1/ifd/exif/{{ushort={MetadataTagIds.Exif.MeteringMode}}}",
            $"/app1/ifd/exif/{{ushort={MetadataTagIds.Exif.Flash}}}",
            $"/app1/ifd/exif/{{ushort={MetadataTagIds.Exif.FocalLength}}}",
            $"/app1/ifd/exif/{{ushort={MetadataTagIds.Exif.WhiteBalance}}}",
            $"/app1/ifd/{{ushort={MetadataTagIds.Ifd.Orientation}}}",
            $"/app1/ifd/exif/{{ushort={MetadataTagIds.Exif.ColorSpace}}}",
            $"/app1/ifd/{{ushort={MetadataTagIds.Ifd.Artist}}}",
            $"/app1/ifd/{{ushort={MetadataTagIds.Ifd.Copyright}}}",
            $"/app1/ifd/gps/{{ushort={MetadataTagIds.Gps.VersionId}}}",
            $"/app1/ifd/gps/{{ushort={MetadataTagIds.Gps.LatitudeRef}}}",
            $"/app1/ifd/gps/{{ushort={MetadataTagIds.Gps.Latitude}}}",
            $"/app1/ifd/gps/{{ushort={MetadataTagIds.Gps.LongitudeRef}}}",
            $"/app1/ifd/gps/{{ushort={MetadataTagIds.Gps.Longitude}}}",
            $"/app1/ifd/gps/{{ushort={MetadataTagIds.Gps.AltitudeRef}}}",
            $"/app1/ifd/gps/{{ushort={MetadataTagIds.Gps.Altitude}}}",
            $"/ifd/exif/{{ushort={MetadataTagIds.Exif.DateTakenOriginal}}}",
            $"/ifd/exif/{{ushort={MetadataTagIds.Exif.DateTakenDigitized}}}",
            $"/ifd/{{ushort={MetadataTagIds.Ifd.DateTime}}}",
            $"/ifd/{{ushort={MetadataTagIds.Ifd.Make}}}",
            $"/ifd/{{ushort={MetadataTagIds.Ifd.Model}}}",
            $"/ifd/exif/{{ushort={MetadataTagIds.Exif.LensModel}}}",
            $"/ifd/exif/{{ushort={MetadataTagIds.Exif.ExposureTime}}}",
            $"/ifd/exif/{{ushort={MetadataTagIds.Exif.FNumber}}}",
            $"/ifd/exif/{{ushort={MetadataTagIds.Exif.IsoSpeed}}}",
            $"/ifd/exif/{{ushort={MetadataTagIds.Exif.ExposureBias}}}",
            $"/ifd/exif/{{ushort={MetadataTagIds.Exif.MeteringMode}}}",
            $"/ifd/exif/{{ushort={MetadataTagIds.Exif.Flash}}}",
            $"/ifd/exif/{{ushort={MetadataTagIds.Exif.FocalLength}}}",
            $"/ifd/exif/{{ushort={MetadataTagIds.Exif.WhiteBalance}}}",
            $"/ifd/{{ushort={MetadataTagIds.Ifd.Orientation}}}",
            $"/ifd/exif/{{ushort={MetadataTagIds.Exif.ColorSpace}}}",
            $"/ifd/{{ushort={MetadataTagIds.Ifd.Artist}}}",
            $"/ifd/{{ushort={MetadataTagIds.Ifd.Copyright}}}",
            $"/ifd/gps/{{ushort={MetadataTagIds.Gps.VersionId}}}",
            $"/ifd/gps/{{ushort={MetadataTagIds.Gps.LatitudeRef}}}",
            $"/ifd/gps/{{ushort={MetadataTagIds.Gps.Latitude}}}",
            $"/ifd/gps/{{ushort={MetadataTagIds.Gps.LongitudeRef}}}",
            $"/ifd/gps/{{ushort={MetadataTagIds.Gps.Longitude}}}",
            $"/ifd/gps/{{ushort={MetadataTagIds.Gps.AltitudeRef}}}",
            $"/ifd/gps/{{ushort={MetadataTagIds.Gps.Altitude}}}",
        ];

        // Filenames to avoid
        private static readonly string[] _avoidFilenames =
            [
                "CON", "PRN", "AUX", "NUL",
                "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
                "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
            ];

        public ResizeOperation(string file, string destinationDirectory, Settings settings, IAISuperResolutionService aiSuperResolutionService = null)
        {
            _file = file;
            _destinationDirectory = destinationDirectory;
            _settings = settings;
            _aiSuperResolutionService = aiSuperResolutionService ?? NoOpAiSuperResolutionService.Instance;
        }

        public async Task ExecuteAsync()
        {
            string path;

            using (var inputStream = _fileSystem.File.OpenRead(_file))
            {
                var winrtInputStream = inputStream.AsRandomAccessStream();
                var decoder = await BitmapDecoder.CreateAsync(winrtInputStream);

                // Determine encoder ID from decoder
                var encoderId = CodecHelper.GetEncoderIdForDecoder(decoder);
                if (encoderId == null || !CodecHelper.CanEncode(encoderId.Value))
                {
                    encoderId = CodecHelper.GetEncoderIdFromLegacyGuid(_settings.FallbackEncoder);
                }

                var encoderGuid = encoderId.Value;

                if (_settings.SelectedSize is AiSize)
                {
                    path = await ExecuteAiAsync(decoder, winrtInputStream, encoderGuid);
                }
                else
                {
                    var originalWidth = (int)decoder.PixelWidth;
                    var originalHeight = (int)decoder.PixelHeight;
                    bool shouldApplyJpegExifFixup = false;

                    var plan = CalculateTransformPlan(originalWidth, originalHeight, decoder.DpiX, decoder.DpiY);
                    path = GetDestinationPath(encoderGuid, plan.OutputWidth, plan.OutputHeight);
                    _fileSystem.Directory.CreateDirectory(_fileSystem.Path.GetDirectoryName(path));

                    using (var outputStream = _fileSystem.File.Open(path, FileMode.CreateNew, FileAccess.ReadWrite))
                    {
                        var winrtOutputStream = outputStream.AsRandomAccessStream();

                        var encodeStrategy = await EncodeToStreamAsync(
                            decoder,
                            winrtInputStream,
                            winrtOutputStream,
                            outputStream,
                            encoderGuid,
                            plan,
                            forceReencode: false,
                            async (encoder, isTranscode) =>
                            {
                                if (isTranscode)
                                {
                                    if (!plan.NoTransformNeeded)
                                    {
                                        encoder.BitmapTransform.ScaledWidth = plan.ScaledWidth;
                                        encoder.BitmapTransform.ScaledHeight = plan.ScaledHeight;
                                        encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;

                                        if (plan.CropBounds.HasValue)
                                        {
                                            encoder.BitmapTransform.Bounds = plan.CropBounds.Value;
                                        }
                                    }
                                }
                                else
                                {
                                    await EncodeFramesAsync(encoder, decoder, plan);
                                }
                            });

                        shouldApplyJpegExifFixup = ShouldApplyJpegExifFixup(encodeStrategy, encoderGuid, _settings.RemoveMetadata);
                    }

                    if (shouldApplyJpegExifFixup)
                    {
                        // JPEG is the one format where Explorer-facing GPS visibility depends on
                        // a coherent EXIF/GPS IFD layout, not just preserved raw tags. Rewriting
                        // EXIF here keeps the metadata in sync with the newly encoded pixels.
                        _ = JpegExifFixupHelper.TryRewriteExif(_fileSystem, _file, path, (uint)plan.OutputWidth, (uint)plan.OutputHeight);
                    }
                }
            }

            if (_settings.KeepDateModified)
            {
                _fileSystem.File.SetLastWriteTimeUtc(path, _fileSystem.File.GetLastWriteTimeUtc(_file));
            }

            if (_settings.Replace)
            {
                var backup = GetBackupPath();
                _fileSystem.File.Replace(path, _file, backup, ignoreMetadataErrors: true);
                FileSystem.DeleteFile(backup, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            }
        }

        private async Task<string> ExecuteAiAsync(BitmapDecoder decoder, IRandomAccessStream winrtInputStream, Guid encoderGuid)
        {
            try
            {
                using var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied);

                using var aiResult = _aiSuperResolutionService.ApplySuperResolution(
                    softwareBitmap,
                    _settings.AiSize.Scale,
                    _file);

                if (aiResult == null)
                {
                    throw new InvalidOperationException(ResourceLoaderInstance.GetString("Error_AiConversionFailed"));
                }

                var plan = new TransformPlan(
                    (int)decoder.PixelWidth,
                    (int)decoder.PixelHeight,
                    (uint)aiResult.PixelWidth,
                    (uint)aiResult.PixelHeight,
                    null,
                    false);

                var path = GetDestinationPath(encoderGuid, plan.OutputWidth, plan.OutputHeight);
                _fileSystem.Directory.CreateDirectory(_fileSystem.Path.GetDirectoryName(path));
                bool shouldApplyJpegExifFixup = false;

                using (var outputStream = _fileSystem.File.Open(path, FileMode.CreateNew, FileAccess.ReadWrite))
                {
                    var winrtOutputStream = outputStream.AsRandomAccessStream();

                    // AI output is already materialized as pixels, so we must re-encode rather than
                    // transcode. This also guarantees JPEG quality settings are applied.
                    var encodeStrategy = await EncodeToStreamAsync(
                        decoder,
                        winrtInputStream,
                        winrtOutputStream,
                        outputStream,
                        encoderGuid,
                        plan,
                        true,
                        (encoder, _) =>
                        {
                            encoder.SetSoftwareBitmap(aiResult);
                            return Task.CompletedTask;
                        });

                    shouldApplyJpegExifFixup = ShouldApplyJpegExifFixup(encodeStrategy, encoderGuid, _settings.RemoveMetadata);
                }

                if (shouldApplyJpegExifFixup)
                {
                    _ = JpegExifFixupHelper.TryRewriteExif(_fileSystem, _file, path, (uint)plan.OutputWidth, (uint)plan.OutputHeight);
                }

                return path;
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                var errorMessage = string.Format(CultureInfo.CurrentCulture, AiErrorFormat, ex.Message);
                throw new InvalidOperationException(errorMessage, ex);
            }
        }

        private async Task<EncodeStrategy> EncodeToStreamAsync(
            BitmapDecoder decoder,
            IRandomAccessStream inputStream,
            IRandomAccessStream outputStream,
            Stream rawOutputStream,
            Guid encoderGuid,
            TransformPlan plan,
            bool forceReencode,
            Func<BitmapEncoder, bool, Task> writeContent)
        {
            var decoderEncoderId = CodecHelper.GetEncoderIdForDecoder(decoder);
            var strategy = DetermineEncodeStrategy(
                encoderGuid,
                decoderEncoderId,
                plan.NoTransformNeeded,
                _settings.RemoveMetadata,
                forceReencode);

            if (strategy == EncodeStrategy.Transcode)
            {
                await TranscodeAsync(decoder, inputStream, outputStream, writeContent);
            }
            else
            {
                await ReencodeAsync(
                    decoder,
                    outputStream,
                    rawOutputStream,
                    encoderGuid,
                    plan,
                    writeContent);
            }

            return strategy;
        }

        private static bool ShouldApplyJpegExifFixup(EncodeStrategy strategy, Guid encoderGuid, bool removeMetadata)
            => strategy == EncodeStrategy.Reencode
                && !removeMetadata
                && encoderGuid == BitmapEncoder.JpegEncoderId;

        internal static EncodeStrategy DetermineEncodeStrategy(
            Guid encoderGuid,
            Guid? decoderEncoderId,
            bool noTransformNeeded,
            bool removeMetadata,
            bool forceReencode = false)
        {
            // Prefer transcoding when the source and destination codec match and we want to
            // preserve metadata, because that lets the platform carry the existing container
            // state forward with less manual reconstruction. Re-encode only when settings or
            // API constraints require rebuilding the image from decoded pixels.
            bool mustReencode = forceReencode || (encoderGuid == BitmapEncoder.JpegEncoderId && !noTransformNeeded);
            bool canTranscode = !mustReencode
                && !removeMetadata
                && decoderEncoderId.HasValue
                && decoderEncoderId.Value == encoderGuid;

            return canTranscode ? EncodeStrategy.Transcode : EncodeStrategy.Reencode;
        }

        /// <summary>
        /// Preferred path when the codec stays the same and we want to preserve metadata.
        /// WIC can keep most container state intact here, so this path minimizes manual
        /// metadata reconstruction and is less likely to discard descriptive metadata.
        /// The <paramref name="writeContent"/> callback receives isTranscode=true and should
        /// configure <see cref="BitmapEncoder.BitmapTransform"/> properties only.
        /// </summary>
        private static async Task TranscodeAsync(
            BitmapDecoder decoder,
            IRandomAccessStream inputStream,
            IRandomAccessStream outputStream,
            Func<BitmapEncoder, bool, Task> writeContent)
        {
            inputStream.Seek(0);
            var encoder = await BitmapEncoder.CreateForTranscodingAsync(outputStream, decoder);
            await writeContent(encoder, true);

            // Safety net: some JPEG files with large/unusual metadata blocks (e.g. 50+ KB
            // embedded thumbnails) lose EXIF properties during transcode — the WPF equivalent
            // threw InvalidOperationException on encoder.Metadata = decoder.Metadata for these.
            // Re-set known critical properties to ensure they survive.
            await CopyKnownMetadataAsync(decoder, encoder);

            await encoder.FlushAsync();
        }

        /// <summary>
        /// Re-encode path: creates a blank encoder and writes decoded pixels into a new
        /// container. This is required when encoder options (for example JPEG quality) must
        /// apply, when metadata should be stripped, when the format changes, or when the API
        /// only exposes pixel-writing operations on non-transcoding encoders.
        /// The <paramref name="writeContent"/> callback receives isTranscode=false and should
        /// call <see cref="EncodeFramesAsync"/> or <see cref="BitmapEncoder.SetSoftwareBitmap"/>.
        /// </summary>
        private async Task ReencodeAsync(
            BitmapDecoder decoder,
            IRandomAccessStream outputStream,
            Stream rawOutputStream,
            Guid encoderGuid,
            TransformPlan plan,
            Func<BitmapEncoder, bool, Task> writeContent)
        {
            bool? pngInterlace = encoderGuid == BitmapEncoder.PngEncoderId
                ? _settings.PngInterlaceOption switch
                {
                    PngInterlaceOption.On => true,
                    PngInterlaceOption.Off => false,
                    _ => null,
                }
                : null;

            if (!_settings.RemoveMetadata)
            {
                var usedWic = await WicMetadataCopier.TryReencodeWithMetadataAsync(
                    _file,
                    decoder,
                    rawOutputStream,
                    encoderGuid,
                    encoderGuid == BitmapEncoder.JpegEncoderId ? GetJpegQualityFraction() : null,
                    pngInterlace,
                    encoderGuid == BitmapEncoder.TiffEncoderId ? MapTiffCompression(_settings.TiffCompressOption) : null,
                    plan);

                if (usedWic)
                {
                    return;
                }
            }

            var encoder = await CreateFreshEncoderAsync(encoderGuid, outputStream);
            await writeContent(encoder, false);

            if (!_settings.RemoveMetadata)
            {
                await CopyAllMetadataToEncoderAsync(decoder, encoder);
            }

            await encoder.FlushAsync();
        }

        /// <summary>
        /// Reads all known metadata from <paramref name="decoder"/> and writes it to
        /// <paramref name="encoder"/> before the stream is flushed.  Writing metadata before
        /// <see cref="BitmapEncoder.FlushAsync"/> lets WIC allocate the necessary metadata block
        /// from scratch, avoiding the space constraints of <see cref="IWicFastMetadataEncoder"/>.
        /// Both WIC query paths and Windows property system names are queried so that low-level
        /// EXIF/GPS tags and higher-level descriptors (e.g. System.Comment) are all preserved.
        /// </summary>
        private async Task CopyAllMetadataToEncoderAsync(BitmapDecoder decoder, BitmapEncoder encoder)
        {
            // Write Shell property names first. The Windows property system handles
            // creation of GPS sub-IFD and other compound metadata structures in the encoder.
            // Writing Shell properties first establishes the metadata block layout.
            var shellMetadata = await ReadMetadataAsync(decoder, KnownMetadataProperties);
            if (shellMetadata != null && shellMetadata.Count > 0)
            {
                try
                {
                    await encoder.BitmapProperties.SetPropertiesAsync(shellMetadata);
                }
                catch
                {
                    // Some encoders or formats may not accept all Shell property names.
                }
            }

            // Overwrite with WIC query path values for raw EXIF accuracy.
            // This fallback path still cannot reliably recreate GPS sub-IFDs on a fresh WinRT
            // encoder, so the primary re-encode path uses the WIC COM helper instead.
            try
            {
                var wicProps = await decoder.BitmapProperties.GetPropertiesAsync(KnownWritableMetadataQueryPaths);
                if (wicProps.Count > 0)
                {
                    await encoder.BitmapProperties.SetPropertiesAsync(wicProps);
                }
            }
            catch
            {
                // Some formats (e.g. BMP) or files with unusual metadata may fail WIC path queries.
            }
        }

        /// <summary>
        /// Decodes each frame, applies the transform, and writes pixel data to the encoder.
        /// Uses GetPixelDataAsync + SetPixelData for explicit pixel format control — the
        /// SetSoftwareBitmap API can fail with ArgumentException for some decoder outputs.
        /// </summary>
        private static async Task EncodeFramesAsync(
            BitmapEncoder encoder,
            BitmapDecoder decoder,
            TransformPlan plan)
        {
            var transform = new BitmapTransform();
            if (!plan.NoTransformNeeded)
            {
                transform.ScaledWidth = plan.ScaledWidth;
                transform.ScaledHeight = plan.ScaledHeight;
                transform.InterpolationMode = BitmapInterpolationMode.Fant;

                if (plan.CropBounds.HasValue)
                {
                    transform.Bounds = plan.CropBounds.Value;
                }
            }
            else
            {
                transform.ScaledWidth = (uint)plan.OriginalWidth;
                transform.ScaledHeight = (uint)plan.OriginalHeight;
            }

            uint outWidth = plan.CropBounds?.Width ?? (plan.NoTransformNeeded ? (uint)plan.OriginalWidth : plan.ScaledWidth);
            uint outHeight = plan.CropBounds?.Height ?? (plan.NoTransformNeeded ? (uint)plan.OriginalHeight : plan.ScaledHeight);

            for (uint i = 0; i < decoder.FrameCount; i++)
            {
                if (i > 0)
                {
                    await encoder.GoToNextFrameAsync();
                }

                var frame = await decoder.GetFrameAsync(i);
                var pixelData = await frame.GetPixelDataAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied,
                    transform,
                    ExifOrientationMode.IgnoreExifOrientation,
                    ColorManagementMode.DoNotColorManage);

                encoder.SetPixelData(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied,
                    outWidth,
                    outHeight,
                    frame.DpiX,
                    frame.DpiY,
                    pixelData.DetachPixelData());
            }
        }

        /// <summary>
        /// Best-effort read of metadata properties from the decoder.
        /// Returns null if the format doesn't support metadata (e.g. BMP).
        /// </summary>
        private static async Task<BitmapPropertySet> ReadMetadataAsync(BitmapDecoder decoder, string[] propertyNames)
        {
            try
            {
                var props = await decoder.BitmapProperties.GetPropertiesAsync(propertyNames);
                if (props.Count > 0)
                {
                    var result = new BitmapPropertySet();
                    foreach (var prop in props)
                    {
                        result[prop.Key] = prop.Value;
                    }

                    return result;
                }
            }
            catch
            {
                // Some formats (e.g. BMP) don't support property queries.
            }

            return null;
        }

        /// <summary>
        /// Safety net for the transcode path: re-sets known EXIF properties that
        /// CreateForTranscodingAsync may silently drop for files with large or
        /// unusual metadata blocks (see TestMetadataIssue2447.jpg).
        /// </summary>
        private static async Task CopyKnownMetadataAsync(BitmapDecoder decoder, BitmapEncoder encoder)
        {
            var metadata = await ReadMetadataAsync(decoder, KnownMetadataProperties);
            if (metadata == null || metadata.Count == 0)
            {
                return;
            }

            try
            {
                await encoder.BitmapProperties.SetPropertiesAsync(metadata);
            }
            catch
            {
                // Some encoders don't support these properties on the transcode path.
            }
        }

        private TransformPlan CalculateTransformPlan(
            int originalWidth, int originalHeight, double dpiX, double dpiY)
        {
            // Convert from the chosen size unit to pixels, if necessary.
            double width = _settings.SelectedSize.GetPixelWidth(originalWidth, dpiX);
            double height = _settings.SelectedSize.GetPixelHeight(originalHeight, dpiY);

            // Swap target width/height dimensions if orientation correction is required.
            bool canSwapDimensions = _settings.IgnoreOrientation &&
                !_settings.SelectedSize.HasAuto &&
                _settings.SelectedSize.Unit != ResizeUnit.Percent;

            if (canSwapDimensions)
            {
                bool isInputLandscape = originalWidth > originalHeight;
                bool isInputPortrait = originalHeight > originalWidth;
                bool isTargetLandscape = width > height;
                bool isTargetPortrait = height > width;

                // Swap dimensions if there is a mismatch between input and target.
                if ((isInputLandscape && isTargetPortrait) ||
                    (isInputPortrait && isTargetLandscape))
                {
                    (width, height) = (height, width);
                }
            }

            double scaleX = width / originalWidth;
            double scaleY = height / originalHeight;

            // Normalize scales based on the chosen Fit/Fill mode.
            if (_settings.SelectedSize.Fit == ResizeFit.Fit)
            {
                scaleX = Math.Min(scaleX, scaleY);
                scaleY = scaleX;
            }
            else if (_settings.SelectedSize.Fit == ResizeFit.Fill)
            {
                scaleX = Math.Max(scaleX, scaleY);
                scaleY = scaleX;
            }

            // Handle Shrink Only mode.
            if (_settings.ShrinkOnly && _settings.SelectedSize.Unit != ResizeUnit.Percent)
            {
                if (scaleX > 1 || scaleY > 1)
                {
                    return new TransformPlan(originalWidth, originalHeight, (uint)originalWidth, (uint)originalHeight, null, true);
                }

                bool isFillCropRequired = _settings.SelectedSize.Fit == ResizeFit.Fill &&
                    (originalWidth > width || originalHeight > height);

                if (scaleX == 1 && scaleY == 1 && !isFillCropRequired)
                {
                    return new TransformPlan(originalWidth, originalHeight, (uint)originalWidth, (uint)originalHeight, null, true);
                }
            }

            // Calculate scaled dimensions
            uint scaledWidth = (uint)Math.Max(1, (int)Math.Round(originalWidth * scaleX));
            uint scaledHeight = (uint)Math.Max(1, (int)Math.Round(originalHeight * scaleY));

            // Apply the centered crop for Fill mode, if necessary.
            if (_settings.SelectedSize.Fit == ResizeFit.Fill
                && (scaledWidth > (uint)width || scaledHeight > (uint)height))
            {
                uint cropX = (uint)(((originalWidth * scaleX) - width) / 2);
                uint cropY = (uint)(((originalHeight * scaleY) - height) / 2);

                var cropBounds = new BitmapBounds
                {
                    X = cropX,
                    Y = cropY,
                    Width = (uint)width,
                    Height = (uint)height,
                };

                return new TransformPlan(originalWidth, originalHeight, scaledWidth, scaledHeight, cropBounds, false);
            }

            return new TransformPlan(originalWidth, originalHeight, scaledWidth, scaledHeight, null, false);
        }

        private async Task<BitmapEncoder> CreateFreshEncoderAsync(Guid encoderGuid, IRandomAccessStream outputStream)
        {
            var propertySet = GetEncoderPropertySet(encoderGuid);
            return propertySet != null
                ? await BitmapEncoder.CreateAsync(encoderGuid, outputStream, propertySet)
                : await BitmapEncoder.CreateAsync(encoderGuid, outputStream);
        }

        private float GetJpegQualityFraction()
            => (float)Math.Clamp(_settings.JpegQualityLevel, 1, 100) / 100f;

        private BitmapPropertySet GetEncoderPropertySet(Guid encoderGuid)
        {
            if (encoderGuid == BitmapEncoder.JpegEncoderId)
            {
                return new BitmapPropertySet
                {
                    { "ImageQuality", new BitmapTypedValue(GetJpegQualityFraction(), PropertyType.Single) },
                };
            }

            if (encoderGuid == BitmapEncoder.PngEncoderId)
            {
                // Only override when explicitly set; Default lets the WIC encoder decide.
                if (_settings.PngInterlaceOption == PngInterlaceOption.On)
                {
                    return new BitmapPropertySet
                    {
                        { "InterlaceOption", new BitmapTypedValue(true, PropertyType.Boolean) },
                    };
                }
                else if (_settings.PngInterlaceOption == PngInterlaceOption.Off)
                {
                    return new BitmapPropertySet
                    {
                        { "InterlaceOption", new BitmapTypedValue(false, PropertyType.Boolean) },
                    };
                }
            }

            if (encoderGuid == BitmapEncoder.TiffEncoderId)
            {
                var compressionMethod = MapTiffCompression(_settings.TiffCompressOption);
                if (compressionMethod.HasValue)
                {
                    return new BitmapPropertySet
                    {
                        { "TiffCompressionMethod", new BitmapTypedValue(compressionMethod.Value, PropertyType.UInt8) },
                    };
                }
            }

            return null;
        }

        private static byte? MapTiffCompression(TiffCompressOption option)
        {
            return option switch
            {
                TiffCompressOption.None => 1,
                TiffCompressOption.Ccitt3 => 2,
                TiffCompressOption.Ccitt4 => 3,
                TiffCompressOption.Lzw => 4,
                TiffCompressOption.Rle => 5,
                TiffCompressOption.Zip => 6,
                _ => null, // Default: let the encoder decide
            };
        }

        private string GetDestinationPath(Guid encoderGuid, int outputPixelWidth, int outputPixelHeight)
        {
            var directory = _destinationDirectory ?? _fileSystem.Path.GetDirectoryName(_file);
            var originalFileName = _fileSystem.Path.GetFileNameWithoutExtension(_file);

            var supportedExtensions = CodecHelper.GetSupportedExtensions(encoderGuid);
            var extension = _fileSystem.Path.GetExtension(_file);
            if (!supportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                extension = CodecHelper.GetDefaultExtension(encoderGuid);
            }

            string sizeName = _settings.SelectedSize is AiSize aiSize
                ? aiSize.ScaleDisplay
                : _settings.SelectedSize.Name;
            string sizeNameSanitized = sizeName
                .Replace('\\', '_')
                .Replace('/', '_');

            var selectedWidth = _settings.SelectedSize is AiSize ? outputPixelWidth : _settings.SelectedSize.Width;
            var selectedHeight = _settings.SelectedSize is AiSize ? outputPixelHeight : _settings.SelectedSize.Height;
            var fileName = string.Format(
                CultureInfo.CurrentCulture,
                _settings.FileNameFormat,
                originalFileName,
                sizeNameSanitized,
                selectedWidth,
                selectedHeight,
                outputPixelWidth,
                outputPixelHeight);

            fileName = fileName
                .Replace(':', '_')
                .Replace('*', '_')
                .Replace('?', '_')
                .Replace('"', '_')
                .Replace('<', '_')
                .Replace('>', '_')
                .Replace('|', '_');

            if (_avoidFilenames.Contains(fileName.ToUpperInvariant()))
            {
                fileName = fileName + "_";
            }

            var path = _fileSystem.Path.Combine(directory, fileName + extension);
            var uniquifier = 1;
            while (_fileSystem.File.Exists(path))
            {
                path = _fileSystem.Path.Combine(directory, fileName + " (" + uniquifier++ + ")" + extension);
            }

            return path;
        }

        private string GetBackupPath()
        {
            var directory = _fileSystem.Path.GetDirectoryName(_file);
            var fileName = _fileSystem.Path.GetFileNameWithoutExtension(_file);
            var extension = _fileSystem.Path.GetExtension(_file);

            var path = _fileSystem.Path.Combine(directory, fileName + ".bak" + extension);
            var uniquifier = 1;
            while (_fileSystem.File.Exists(path))
            {
                path = _fileSystem.Path.Combine(directory, fileName + " (" + uniquifier++ + ")" + ".bak" + extension);
            }

            return path;
        }
    }
}
