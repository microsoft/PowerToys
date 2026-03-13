#pragma warning disable IDE0073
// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/
#pragma warning restore IDE0073

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
        private readonly IFileSystem _fileSystem = new System.IO.Abstractions.FileSystem();

        private readonly string _file;
        private readonly string _destinationDirectory;
        private readonly Settings _settings;
        private readonly IAISuperResolutionService _aiSuperResolutionService;

        // Cache CompositeFormat for AI error message formatting (CA1863)
        private static CompositeFormat _aiErrorFormat;

        private static CompositeFormat AiErrorFormat
        {
            get
            {
                if (_aiErrorFormat == null)
                {
                    _aiErrorFormat = CompositeFormat.Parse(ResourceLoaderInstance.ResourceLoader.GetString("Error_AiProcessingFailed"));
                }

                return _aiErrorFormat;
            }
        }

        // Filenames to avoid according to https://learn.microsoft.com/windows/win32/fileio/naming-a-file#file-and-directory-names
        private static readonly string[] _avoidFilenames =
            {
                "CON", "PRN", "AUX", "NUL",
                "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
                "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
            };

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
                    // AI super resolution path
                    path = await ExecuteAiAsync(decoder, winrtInputStream, encoderGuid);
                }
                else
                {
                    // Standard resize path
                    var originalWidth = (int)decoder.PixelWidth;
                    var originalHeight = (int)decoder.PixelHeight;
                    var dpiX = decoder.DpiX;
                    var dpiY = decoder.DpiY;

                    var (scaledWidth, scaledHeight, cropBounds, noTransformNeeded) =
                        CalculateDimensions(originalWidth, originalHeight, dpiX, dpiY);

                    // For destination path, calculate final output dimensions
                    int outputWidth, outputHeight;
                    if (noTransformNeeded)
                    {
                        outputWidth = originalWidth;
                        outputHeight = originalHeight;
                    }
                    else if (cropBounds.HasValue)
                    {
                        outputWidth = (int)cropBounds.Value.Width;
                        outputHeight = (int)cropBounds.Value.Height;
                    }
                    else
                    {
                        outputWidth = (int)scaledWidth;
                        outputHeight = (int)scaledHeight;
                    }

                    path = GetDestinationPath(encoderGuid, outputWidth, outputHeight);
                    _fileSystem.Directory.CreateDirectory(_fileSystem.Path.GetDirectoryName(path));

                    using (var outputStream = _fileSystem.File.Open(path, FileMode.CreateNew, FileAccess.Write))
                    {
                        var winrtOutputStream = outputStream.AsRandomAccessStream();

                        if (!_settings.RemoveMetadata)
                        {
                            // Transcode path: preserves all metadata automatically
                            winrtInputStream.Seek(0);
                            var encoder = await BitmapEncoder.CreateForTranscodingAsync(winrtOutputStream, decoder);

                            if (!noTransformNeeded)
                            {
                                encoder.BitmapTransform.ScaledWidth = scaledWidth;
                                encoder.BitmapTransform.ScaledHeight = scaledHeight;
                                encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;

                                if (cropBounds.HasValue)
                                {
                                    encoder.BitmapTransform.Bounds = cropBounds.Value;
                                }
                            }

                            await ConfigureEncoderPropertiesAsync(encoder, encoderGuid);
                            await encoder.FlushAsync();
                        }
                        else
                        {
                            // Strip metadata path: fresh encoder = no old metadata
                            var propertySet = GetEncoderPropertySet(encoderGuid);
                            var encoder = propertySet != null
                                ? await BitmapEncoder.CreateAsync(encoderGuid, winrtOutputStream, propertySet)
                                : await BitmapEncoder.CreateAsync(encoderGuid, winrtOutputStream);

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

                            // Handle multi-frame images (e.g., GIF)
                            for (uint i = 0; i < decoder.FrameCount; i++)
                            {
                                if (i > 0)
                                {
                                    await encoder.GoToNextFrameAsync();
                                }

                                var frame = await decoder.GetFrameAsync(i);
                                var bitmap = await frame.GetSoftwareBitmapAsync(
                                    frame.BitmapPixelFormat,
                                    BitmapAlphaMode.Premultiplied,
                                    transform,
                                    ExifOrientationMode.IgnoreExifOrientation,
                                    ColorManagementMode.DoNotColorManage);

                                encoder.SetSoftwareBitmap(bitmap);
                            }

                            await encoder.FlushAsync();
                        }
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
                // Get the source bitmap for AI processing
                var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied);

                var aiResult = _aiSuperResolutionService.ApplySuperResolution(
                    softwareBitmap,
                    _settings.AiSize.Scale,
                    _file);

                if (aiResult == null)
                {
                    throw new InvalidOperationException(ResourceLoaderInstance.ResourceLoader.GetString("Error_AiConversionFailed"));
                }

                var outputWidth = aiResult.PixelWidth;
                var outputHeight = aiResult.PixelHeight;

                var path = GetDestinationPath(encoderGuid, outputWidth, outputHeight);
                _fileSystem.Directory.CreateDirectory(_fileSystem.Path.GetDirectoryName(path));

                using (var outputStream = _fileSystem.File.Open(path, FileMode.CreateNew, FileAccess.Write))
                {
                    var winrtOutputStream = outputStream.AsRandomAccessStream();

                    if (!_settings.RemoveMetadata)
                    {
                        // Transcode path: preserves metadata
                        winrtInputStream.Seek(0);
                        var encoder = await BitmapEncoder.CreateForTranscodingAsync(winrtOutputStream, decoder);
                        encoder.SetSoftwareBitmap(aiResult);
                        await ConfigureEncoderPropertiesAsync(encoder, encoderGuid);
                        await encoder.FlushAsync();
                    }
                    else
                    {
                        // Strip metadata path
                        var propertySet = GetEncoderPropertySet(encoderGuid);
                        var encoder = propertySet != null
                            ? await BitmapEncoder.CreateAsync(encoderGuid, winrtOutputStream, propertySet)
                            : await BitmapEncoder.CreateAsync(encoderGuid, winrtOutputStream);

                        encoder.SetSoftwareBitmap(aiResult);
                        await encoder.FlushAsync();
                    }
                }

                return path;
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                var errorMessage = string.Format(CultureInfo.CurrentCulture, AiErrorFormat, ex.Message);
                throw new InvalidOperationException(errorMessage, ex);
            }
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

        private async Task ConfigureEncoderPropertiesAsync(BitmapEncoder encoder, Guid encoderGuid)
        {
            if (encoderGuid == BitmapEncoder.JpegEncoderId)
            {
                await encoder.BitmapProperties.SetPropertiesAsync(new BitmapPropertySet
                {
                    { "ImageQuality", new BitmapTypedValue((float)MathHelpers.Clamp(_settings.JpegQualityLevel, 1, 100) / 100f, PropertyType.Single) },
                });
            }
        }

        private BitmapPropertySet GetEncoderPropertySet(Guid encoderGuid)
        {
            if (encoderGuid == BitmapEncoder.JpegEncoderId)
            {
                return new BitmapPropertySet
                {
                    { "ImageQuality", new BitmapTypedValue((float)MathHelpers.Clamp(_settings.JpegQualityLevel, 1, 100) / 100f, PropertyType.Single) },
                };
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
