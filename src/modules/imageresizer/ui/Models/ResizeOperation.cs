#pragma warning disable IDE0073
// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/
#pragma warning restore IDE0073

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImageResizer.Properties;
using ImageResizer.Services;
using ImageResizer.Utilities;
using Microsoft.VisualBasic.FileIO;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

using FileSystem = Microsoft.VisualBasic.FileIO.FileSystem;

namespace ImageResizer.Models
{
    internal sealed class ResizeOperation
    {
        private readonly IFileSystem _fileSystem = new System.IO.Abstractions.FileSystem();

        private readonly string _file;
        private readonly string _destinationDirectory;
        private readonly Settings _settings;
        private readonly IAISuperResolutionService _aiSuperResolutionService;

        private static readonly Lazy<CompositeFormat> _aiErrorFormat = new Lazy<CompositeFormat>(
            () => CompositeFormat.Parse(ResourceLoaderInstance.ResourceLoader.GetString("Error_AiProcessingFailed")));

        private static readonly string[] _avoidFilenames =
            {
                "CON", "PRN", "AUX", "NUL",
                "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
                "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
            };

        // Map WPF legacy container format GUIDs to WinRT encoder IDs for backward compatibility
        private static readonly Dictionary<Guid, Guid> LegacyGuidToEncoderId = new()
        {
            // WPF container format GUIDs → WinRT encoder IDs
            [new Guid("0af1d87e-fcfe-4188-bdeb-a7906471cbe3")] = BitmapEncoder.BmpEncoderId,
            [new Guid("1f8a5601-7d4d-4cbd-9c82-1bc8d4eeb9a5")] = BitmapEncoder.GifEncoderId,
            [new Guid("19e4a5aa-5662-4fc5-a0c0-1758028e1057")] = BitmapEncoder.JpegEncoderId,
            [new Guid("1b7cfaf4-713f-473c-bbcd-6137425faeaf")] = BitmapEncoder.PngEncoderId,
            [new Guid("163bcc30-e2e9-4f0b-961d-a3e9fdb788a3")] = BitmapEncoder.TiffEncoderId,
            [new Guid("57a37caa-367a-4540-916b-f183c5093a4b")] = BitmapEncoder.JpegXREncoderId,
        };

        // Known extension to encoder ID mapping
        private static readonly Dictionary<string, Guid> ExtensionToEncoderId = new(StringComparer.OrdinalIgnoreCase)
        {
            [".bmp"] = BitmapEncoder.BmpEncoderId,
            [".dib"] = BitmapEncoder.BmpEncoderId,
            [".rle"] = BitmapEncoder.BmpEncoderId,
            [".gif"] = BitmapEncoder.GifEncoderId,
            [".jpg"] = BitmapEncoder.JpegEncoderId,
            [".jpeg"] = BitmapEncoder.JpegEncoderId,
            [".jpe"] = BitmapEncoder.JpegEncoderId,
            [".jfif"] = BitmapEncoder.JpegEncoderId,
            [".png"] = BitmapEncoder.PngEncoderId,
            [".tif"] = BitmapEncoder.TiffEncoderId,
            [".tiff"] = BitmapEncoder.TiffEncoderId,
            [".wdp"] = BitmapEncoder.JpegXREncoderId,
            [".jxr"] = BitmapEncoder.JpegXREncoderId,
            [".ico"] = BitmapEncoder.PngEncoderId, // Fallback for ICO
        };

        public ResizeOperation(string file, string destinationDirectory, Settings settings, IAISuperResolutionService aiSuperResolutionService = null)
        {
            _file = file;
            _destinationDirectory = destinationDirectory;
            _settings = settings;
            _aiSuperResolutionService = aiSuperResolutionService ?? NoOpAiSuperResolutionService.Instance;
        }

        public void Execute()
        {
            ExecuteAsync().GetAwaiter().GetResult();
        }

        private async Task ExecuteAsync()
        {
            string path;
            using (var inputStream = _fileSystem.File.OpenRead(_file))
            {
                var winrtStream = inputStream.AsRandomAccessStream();
                var decoder = await BitmapDecoder.CreateAsync(winrtStream);

                var encoderId = GetEncoderIdFromDecoder(decoder);

                // Determine output path early
                var extension = _fileSystem.Path.GetExtension(_file);
                path = GetDestinationPath(extension, decoder);
                _fileSystem.Directory.CreateDirectory(_fileSystem.Path.GetDirectoryName(path));

                using (var outputFileStream = _fileSystem.File.Open(path, FileMode.CreateNew, FileAccess.Write))
                {
                    var outputStream = outputFileStream.AsRandomAccessStream();

                    if (_settings.SelectedSize is AiSize)
                    {
                        // AI super resolution path
                        await ProcessWithAiAsync(decoder, encoderId, outputStream);
                    }
                    else
                    {
                        // Standard resize path - use transcode to preserve metadata
                        await ProcessStandardResizeAsync(decoder, encoderId, outputStream);
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

        private async Task ProcessStandardResizeAsync(BitmapDecoder decoder, Guid encoderId, IRandomAccessStream outputStream)
        {
            int originalWidth = (int)decoder.PixelWidth;
            int originalHeight = (int)decoder.PixelHeight;

            double width = _settings.SelectedSize.GetPixelWidth(originalWidth, decoder.DpiX);
            double height = _settings.SelectedSize.GetPixelHeight(originalHeight, decoder.DpiY);

            bool canSwapDimensions = _settings.IgnoreOrientation &&
                !_settings.SelectedSize.HasAuto &&
                _settings.SelectedSize.Unit != ResizeUnit.Percent;

            if (canSwapDimensions)
            {
                bool isInputLandscape = originalWidth > originalHeight;
                bool isInputPortrait = originalHeight > originalWidth;
                bool isTargetLandscape = width > height;
                bool isTargetPortrait = height > width;

                if ((isInputLandscape && isTargetPortrait) ||
                    (isInputPortrait && isTargetLandscape))
                {
                    (width, height) = (height, width);
                }
            }

            double scaleX = width / originalWidth;
            double scaleY = height / originalHeight;

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

            if (_settings.ShrinkOnly && _settings.SelectedSize.Unit != ResizeUnit.Percent)
            {
                if (scaleX > 1 || scaleY > 1)
                {
                    // No resize needed - just transcode
                    await TranscodeWithoutResizeAsync(decoder, encoderId, outputStream);
                    return;
                }

                bool isFillCropRequired = _settings.SelectedSize.Fit == ResizeFit.Fill &&
                    (originalWidth > width || originalHeight > height);

                if (scaleX == 1 && scaleY == 1 && !isFillCropRequired)
                {
                    await TranscodeWithoutResizeAsync(decoder, encoderId, outputStream);
                    return;
                }
            }

            uint newWidth = (uint)Math.Max(1, Math.Round(originalWidth * scaleX));
            uint newHeight = (uint)Math.Max(1, Math.Round(originalHeight * scaleY));

            // Handle multi-frame (GIF) and single-frame images
            for (uint i = 0; i < decoder.FrameCount; i++)
            {
                var frame = await decoder.GetFrameAsync(i);
                var bitmap = await frame.GetSoftwareBitmapAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied);

                BitmapEncoder encoder;
                if (i == 0)
                {
                    encoder = await BitmapEncoder.CreateAsync(encoderId, outputStream, GetEncoderProperties(encoderId));
                }
                else
                {
                    // For multi-frame, we'd need to handle this differently
                    // WinRT doesn't support multi-frame encoding the same way
                    break; // Only process first frame for now
                }

                // Apply transform
                encoder.BitmapTransform.ScaledWidth = newWidth;
                encoder.BitmapTransform.ScaledHeight = newHeight;
                encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;

                // Apply crop for Fill mode
                if (_settings.SelectedSize.Fit == ResizeFit.Fill &&
                    (newWidth > (uint)width || newHeight > (uint)height))
                {
                    uint cropX = (uint)(((double)newWidth - width) / 2);
                    uint cropY = (uint)(((double)newHeight - height) / 2);
                    uint cropWidth = (uint)Math.Max(1, width);
                    uint cropHeight = (uint)Math.Max(1, height);

                    encoder.BitmapTransform.Bounds = new BitmapBounds
                    {
                        X = cropX,
                        Y = cropY,
                        Width = cropWidth,
                        Height = cropHeight,
                    };
                }

                encoder.SetSoftwareBitmap(bitmap);

                // Handle metadata removal
                if (_settings.RemoveMetadata)
                {
                    // WinRT BitmapEncoder doesn't expose metadata the same way.
                    // By not copying metadata, we effectively remove it.
                    // Orientation and color space are handled by the transform.
                }

                await encoder.FlushAsync();
            }
        }

        private async Task ProcessWithAiAsync(BitmapDecoder decoder, Guid encoderId, IRandomAccessStream outputStream)
        {
            try
            {
                var frame = await decoder.GetFrameAsync(0);
                var bitmap = await frame.GetSoftwareBitmapAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied);

                var result = _aiSuperResolutionService.ApplySuperResolution(
                    bitmap,
                    _settings.AiSize.Scale,
                    _file);

                if (result == null)
                {
                    throw new InvalidOperationException(ResourceLoaderInstance.ResourceLoader.GetString("Error_AiConversionFailed"));
                }

                var encoder = await BitmapEncoder.CreateAsync(encoderId, outputStream, GetEncoderProperties(encoderId));
                encoder.SetSoftwareBitmap(result);
                await encoder.FlushAsync();
            }
            catch (Exception ex)
            {
                var errorMessage = string.Format(CultureInfo.CurrentCulture, _aiErrorFormat.Value, ex.Message);
                throw new InvalidOperationException(errorMessage, ex);
            }
        }

        private async Task TranscodeWithoutResizeAsync(BitmapDecoder decoder, Guid encoderId, IRandomAccessStream outputStream)
        {
            var frame = await decoder.GetFrameAsync(0);
            var bitmap = await frame.GetSoftwareBitmapAsync();

            var encoder = await BitmapEncoder.CreateAsync(encoderId, outputStream, GetEncoderProperties(encoderId));
            encoder.SetSoftwareBitmap(bitmap);
            await encoder.FlushAsync();
        }

        private Guid GetEncoderIdFromDecoder(BitmapDecoder decoder)
        {
            var codecId = decoder.DecoderInformation?.CodecId ?? Guid.Empty;

            // Try direct WinRT decoder→encoder mapping
            if (codecId == BitmapDecoder.BmpDecoderId)
            {
                return BitmapEncoder.BmpEncoderId;
            }

            if (codecId == BitmapDecoder.GifDecoderId)
            {
                return BitmapEncoder.GifEncoderId;
            }

            if (codecId == BitmapDecoder.JpegDecoderId)
            {
                return BitmapEncoder.JpegEncoderId;
            }

            if (codecId == BitmapDecoder.PngDecoderId)
            {
                return BitmapEncoder.PngEncoderId;
            }

            if (codecId == BitmapDecoder.TiffDecoderId)
            {
                return BitmapEncoder.TiffEncoderId;
            }

            if (codecId == BitmapDecoder.JpegXRDecoderId)
            {
                return BitmapEncoder.JpegXREncoderId;
            }

            if (codecId == BitmapDecoder.IcoDecoderId)
            {
                return BitmapEncoder.PngEncoderId;
            }

            // Try file extension as fallback
            var ext = _fileSystem.Path.GetExtension(_file);
            if (!string.IsNullOrEmpty(ext) && ExtensionToEncoderId.TryGetValue(ext, out var extEncoderId))
            {
                return extEncoderId;
            }

            // Try legacy GUID mapping (from settings fallback encoder)
            if (LegacyGuidToEncoderId.TryGetValue(_settings.FallbackEncoder, out var legacyId))
            {
                return legacyId;
            }

            // Ultimate fallback
            return BitmapEncoder.PngEncoderId;
        }

        private BitmapPropertySet GetEncoderProperties(Guid encoderId)
        {
            var props = new BitmapPropertySet();

            if (encoderId == BitmapEncoder.JpegEncoderId)
            {
                // WPF: int 1-100; WinRT: float 0.0-1.0
                float quality = MathHelpers.Clamp(_settings.JpegQualityLevel, 1, 100) / 100f;
                props.Add("ImageQuality", new BitmapTypedValue(quality, Windows.Foundation.PropertyType.Single));
            }

            return props;
        }

        private string GetDestinationPath(string originalExtension, BitmapDecoder decoder)
        {
            var directory = _destinationDirectory ?? _fileSystem.Path.GetDirectoryName(_file);
            var originalFileName = _fileSystem.Path.GetFileNameWithoutExtension(_file);

            var extension = originalExtension;

            string sizeName = _settings.SelectedSize is AiSize aiSize
                ? aiSize.ScaleDisplay
                : _settings.SelectedSize.Name;
            string sizeNameSanitized = sizeName
                .Replace('\\', '_')
                .Replace('/', '_');

            var selectedWidth = _settings.SelectedSize is AiSize ? (double)decoder.PixelWidth : _settings.SelectedSize.Width;
            var selectedHeight = _settings.SelectedSize is AiSize ? (double)decoder.PixelHeight : _settings.SelectedSize.Height;
            var fileName = string.Format(
                CultureInfo.CurrentCulture,
                _settings.FileNameFormat,
                originalFileName,
                sizeNameSanitized,
                selectedWidth,
                selectedHeight,
                decoder.PixelWidth,
                decoder.PixelHeight);

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
