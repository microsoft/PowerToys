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
using ImageResizer.Models.ResizeResults;
using ImageResizer.Properties;
using ImageResizer.Services;
using ImageResizer.Utilities;

namespace ImageResizer.Models
{
    internal class ResizeOperation
    {
        private readonly IFileSystem _fileSystem = new System.IO.Abstractions.FileSystem();
        private readonly IRecycleBinService _recycleBinService;

        private readonly string _file;
        private readonly string _destinationDirectory;
        private readonly Settings _settings;
        private readonly IAISuperResolutionService _aiSuperResolutionService;

        // Cache CompositeFormat for AI error message formatting (CA1863)
        private static CompositeFormat _aiErrorFormat;

        private static CompositeFormat AiErrorFormat =>
            _aiErrorFormat ??= CompositeFormat.Parse(ResourceLoaderInstance.GetString("Error_AiProcessingFailed"));

        private static readonly string[] RenderingMetadataProperties =
            [
                "System.Photo.Orientation",
                "System.Image.ColorSpace",
            ];

        // Filenames to avoid according to https://learn.microsoft.com/windows/win32/fileio/naming-a-file#file-and-directory-names
        private static readonly string[] _avoidFilenames =
            [
                "CON", "PRN", "AUX", "NUL",
                "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
                "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
            ];

        public ResizeOperation(string file, string destinationDirectory, Settings settings, IAISuperResolutionService aiSuperResolutionService = null)
            : this(file, destinationDirectory, settings, new WindowsRecycleBinService(), aiSuperResolutionService)
        {
        }

        public ResizeOperation(string file, string destinationDirectory, Settings settings, IRecycleBinService recycleBinService, IAISuperResolutionService aiSuperResolutionService = null)
        {
            _file = file;
            _destinationDirectory = destinationDirectory;
            _settings = settings;
            _recycleBinService = recycleBinService;
            _aiSuperResolutionService = aiSuperResolutionService ?? NoOpAiSuperResolutionService.Instance;
        }

        public async Task<ResizeResult> ExecuteAsync()
        {
            try
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

                        var (scaledWidth, scaledHeight, cropBounds, noTransformNeeded) =
                            CalculateDimensions(originalWidth, originalHeight, decoder.DpiX, decoder.DpiY);

                        var (outputWidth, outputHeight) = noTransformNeeded
                            ? (originalWidth, originalHeight)
                            : cropBounds.HasValue
                                ? ((int)cropBounds.Value.Width, (int)cropBounds.Value.Height)
                                : ((int)scaledWidth, (int)scaledHeight);

                        path = GetDestinationPath(encoderGuid, outputWidth, outputHeight);
                        _fileSystem.Directory.CreateDirectory(_fileSystem.Path.GetDirectoryName(path));

                        using (var outputStream = _fileSystem.File.Open(path, FileMode.CreateNew, FileAccess.ReadWrite))
                        {
                            var winrtOutputStream = outputStream.AsRandomAccessStream();
                            await EncodeToStreamAsync(
                                decoder,
                                winrtInputStream,
                                winrtOutputStream,
                                encoderGuid,
                                async (encoder, isTranscode) =>
                                {
                                    if (isTranscode)
                                    {
                                        if (!noTransformNeeded)
                                        {
                                            encoder.BitmapTransform.ScaledWidth = scaledWidth;
                                            encoder.BitmapTransform.ScaledHeight = scaledHeight;
                                            encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;

                                            if (cropBounds.HasValue)
                                            {
                                                encoder.BitmapTransform.Bounds = cropBounds.Value;
                                            }

                                            // Apply codec-specific properties (e.g., JPEG quality).
                                            // Must be set after transforms since re-encoding will occur.
                                            var encoderProps = GetEncoderPropertySet(encoderGuid);
                                            if (encoderProps != null)
                                            {
                                                await encoder.BitmapProperties.SetPropertiesAsync(encoderProps);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        await EncodeFramesAsync(
                                            encoder,
                                            decoder,
                                            scaledWidth,
                                            scaledHeight,
                                            cropBounds,
                                            noTransformNeeded,
                                            originalWidth,
                                            originalHeight);
                                    }
                                });
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

                    try
                    {
                        _fileSystem.File.Replace(path, _file, backup, ignoreMetadataErrors: true);
                    }
                    catch (Exception ex)
                    {
                        return new FileReplaceFailedResult(path, _file, backup, ex);
                    }

                    try
                    {
                        _recycleBinService.DeleteToRecycleBin(backup);
                    }
                    catch (Exception ex)
                    {
                        return new FileRecycleFailedResult(_file, backup, ex);
                    }
                }
            }
            catch (Exception ex)
            {
                return new ErrorResult(_file, ex);
            }

            return new SuccessResult(_file);
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

                var outputWidth = aiResult.PixelWidth;
                var outputHeight = aiResult.PixelHeight;

                var path = GetDestinationPath(encoderGuid, outputWidth, outputHeight);
                _fileSystem.Directory.CreateDirectory(_fileSystem.Path.GetDirectoryName(path));

                using (var outputStream = _fileSystem.File.Open(path, FileMode.CreateNew, FileAccess.ReadWrite))
                {
                    var winrtOutputStream = outputStream.AsRandomAccessStream();
                    await EncodeToStreamAsync(
                        decoder,
                        winrtInputStream,
                        winrtOutputStream,
                        encoderGuid,
                        (encoder, _) =>
                        {
                            encoder.SetSoftwareBitmap(aiResult);
                            return Task.CompletedTask;
                        });
                }

                return path;
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                var errorMessage = string.Format(CultureInfo.CurrentCulture, AiErrorFormat, ex.Message);
                throw new InvalidOperationException(errorMessage, ex);
            }
        }

        private async Task EncodeToStreamAsync(
            BitmapDecoder decoder,
            IRandomAccessStream inputStream,
            IRandomAccessStream outputStream,
            Guid encoderGuid,
            Func<BitmapEncoder, bool, Task> writeContent)
        {
            var decoderEncoderId = CodecHelper.GetEncoderIdForDecoder(decoder);
            bool canTranscode = !_settings.RemoveMetadata
                && decoderEncoderId.HasValue
                && decoderEncoderId.Value == encoderGuid;

            if (canTranscode)
            {
                await TranscodeAsync(decoder, inputStream, outputStream, writeContent);
            }
            else
            {
                await FreshEncodeAsync(decoder, outputStream, encoderGuid, writeContent);
            }
        }

        /// <summary>
        /// Transcode path: re-encodes pixels via BitmapTransform while preserving all metadata.
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

            // Safety net: some JPEG files with large/unusual metadata blocks (e.g. 54 KB
            // embedded thumbnails) lose EXIF properties during transcode — the WPF equivalent
            // threw InvalidOperationException on encoder.Metadata = decoder.Metadata for these.
            // Re-set known critical properties to ensure they survive.
            await CopyKnownMetadataAsync(decoder, encoder);

            await encoder.FlushAsync();
        }

        /// <summary>
        /// Fresh encoder path: creates a blank encoder and manually writes pixel data.
        /// Used when metadata must be stripped (RemoveMetadata) or format doesn't match (ICO→PNG).
        /// The <paramref name="writeContent"/> callback receives isTranscode=false and should
        /// call <see cref="EncodeFramesAsync"/> or <see cref="BitmapEncoder.SetSoftwareBitmap"/>.
        /// </summary>
        private async Task FreshEncodeAsync(
            BitmapDecoder decoder,
            IRandomAccessStream outputStream,
            Guid encoderGuid,
            Func<BitmapEncoder, bool, Task> writeContent)
        {
            // Read rendering-critical metadata before encoding so we can restore it on
            // the blank encoder. Only needed for RemoveMetadata; format-mismatch files
            // (e.g. ICO) rarely carry meaningful EXIF data.
            BitmapPropertySet renderingMetadata = null;
            if (_settings.RemoveMetadata)
            {
                renderingMetadata = await ReadMetadataAsync(decoder, RenderingMetadataProperties);
            }

            var encoder = await CreateFreshEncoderAsync(encoderGuid, outputStream);
            await writeContent(encoder, false);

            if (renderingMetadata != null)
            {
                await WriteMetadataAsync(encoder, renderingMetadata);
            }

            await encoder.FlushAsync();
        }

        /// <summary>
        /// Decodes each frame, applies the transform, and writes pixel data to the encoder.
        /// Uses GetPixelDataAsync + SetPixelData for explicit pixel format control — the
        /// SetSoftwareBitmap API can fail with ArgumentException for some decoder outputs.
        /// </summary>
        private static async Task EncodeFramesAsync(
            BitmapEncoder encoder,
            BitmapDecoder decoder,
            uint scaledWidth,
            uint scaledHeight,
            BitmapBounds? cropBounds,
            bool noTransformNeeded,
            int originalWidth,
            int originalHeight)
        {
            var transform = new BitmapTransform();
            if (!noTransformNeeded)
            {
                transform.ScaledWidth = scaledWidth;
                transform.ScaledHeight = scaledHeight;
                transform.InterpolationMode = BitmapInterpolationMode.Fant;

                if (cropBounds.HasValue)
                {
                    transform.Bounds = cropBounds.Value;
                }
            }
            else
            {
                transform.ScaledWidth = (uint)originalWidth;
                transform.ScaledHeight = (uint)originalHeight;
            }

            uint outWidth = cropBounds?.Width ?? (noTransformNeeded ? (uint)originalWidth : scaledWidth);
            uint outHeight = cropBounds?.Height ?? (noTransformNeeded ? (uint)originalHeight : scaledHeight);

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

        private static readonly string[] KnownMetadataProperties =
        [
            "System.Photo.DateTaken",
            "System.Photo.CameraModel",
            "System.Photo.CameraManufacturer",
            "System.Photo.Orientation",
            "System.Image.ColorSpace",
            "System.Comment",
        ];

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
        /// Best-effort write of metadata properties to the encoder.
        /// </summary>
        private static async Task WriteMetadataAsync(BitmapEncoder encoder, BitmapPropertySet metadata)
        {
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
                // Some encoders don't support these properties (e.g. BMP).
            }
        }

        /// <summary>
        /// Safety net for the transcode path: re-sets known EXIF properties that
        /// CreateForTranscodingAsync may silently drop for files with large or
        /// unusual metadata blocks (see TestMetadataIssue2447.jpg).
        /// </summary>
        private static async Task CopyKnownMetadataAsync(BitmapDecoder decoder, BitmapEncoder encoder)
        {
            var metadata = await ReadMetadataAsync(decoder, KnownMetadataProperties);
            await WriteMetadataAsync(encoder, metadata);
        }

        private (uint ScaledWidth, uint ScaledHeight, BitmapBounds? CropBounds, bool NoTransformNeeded) CalculateDimensions(
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
                    return ((uint)originalWidth, (uint)originalHeight, null, true);
                }

                bool isFillCropRequired = _settings.SelectedSize.Fit == ResizeFit.Fill &&
                    (originalWidth > width || originalHeight > height);

                if (scaleX == 1 && scaleY == 1 && !isFillCropRequired)
                {
                    return ((uint)originalWidth, (uint)originalHeight, null, true);
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

                return (scaledWidth, scaledHeight, cropBounds, false);
            }

            return (scaledWidth, scaledHeight, null, false);
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
