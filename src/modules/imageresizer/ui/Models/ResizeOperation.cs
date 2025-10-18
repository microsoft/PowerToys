#pragma warning disable IDE0073
// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/
#pragma warning restore IDE0073

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using ImageResizer.Extensions;
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
        private readonly IAiSuperResolutionService _aiSuperResolutionService;

        // Filenames to avoid according to https://learn.microsoft.com/windows/win32/fileio/naming-a-file#file-and-directory-names
        private static readonly string[] _avoidFilenames =
            {
                "CON", "PRN", "AUX", "NUL",
                "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
                "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
            };

        public ResizeOperation(string file, string destinationDirectory, Settings settings, IAiSuperResolutionService aiSuperResolutionService = null)
        {
            _file = file;
            _destinationDirectory = destinationDirectory;
            _settings = settings;
            _aiSuperResolutionService = aiSuperResolutionService ?? NoOpAiSuperResolutionService.Instance;
        }

        public void Execute()
        {
            string path;
            using (var inputStream = _fileSystem.File.OpenRead(_file))
            {
                var decoder = BitmapDecoder.Create(
                    inputStream,
                    BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.None);

                var containerFormat = decoder.CodecInfo.ContainerFormat;

                var encoder = CreateEncoder(containerFormat);

                if (decoder.Metadata != null)
                {
                    try
                    {
                        encoder.Metadata = decoder.Metadata;
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }

                if (decoder.Palette != null)
                {
                    encoder.Palette = decoder.Palette;
                }

                foreach (var originalFrame in decoder.Frames)
                {
                    var transformedBitmap = Transform(originalFrame);

                    // if the frame was not modified, we should not replace the metadata
                    if (transformedBitmap == originalFrame)
                    {
                        encoder.Frames.Add(originalFrame);
                    }
                    else
                    {
                        BitmapMetadata originalMetadata = (BitmapMetadata)originalFrame.Metadata;

#if DEBUG
                        Debug.WriteLine($"### Processing metadata of file {_file}");
                        originalMetadata.PrintsAllMetadataToDebugOutput();
#endif

                        var metadata = GetValidMetadata(originalMetadata, transformedBitmap, containerFormat);

                        if (_settings.RemoveMetadata && metadata != null)
                        {
                            // strip any metadata that doesn't affect rendering
                            var newMetadata = new BitmapMetadata(metadata.Format);

                            metadata.CopyMetadataPropertyTo(newMetadata, "System.Photo.Orientation");
                            metadata.CopyMetadataPropertyTo(newMetadata, "System.Image.ColorSpace");

                            metadata = newMetadata;
                        }

                        var frame = CreateBitmapFrame(transformedBitmap, metadata);

                        encoder.Frames.Add(frame);
                    }
                }

                path = GetDestinationPath(encoder);
                _fileSystem.Directory.CreateDirectory(_fileSystem.Path.GetDirectoryName(path));
                using (var outputStream = _fileSystem.File.Open(path, FileMode.CreateNew, FileAccess.Write))
                {
                    encoder.Save(outputStream);
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

        private BitmapEncoder CreateEncoder(Guid containerFormat)
        {
            var createdEncoder = BitmapEncoder.Create(containerFormat);
            if (!createdEncoder.CanEncode())
            {
                createdEncoder = BitmapEncoder.Create(_settings.FallbackEncoder);
            }

            ConfigureEncoder(createdEncoder);

            return createdEncoder;

            void ConfigureEncoder(BitmapEncoder encoder)
            {
                switch (encoder)
                {
                    case JpegBitmapEncoder jpegEncoder:
                        jpegEncoder.QualityLevel = MathHelpers.Clamp(_settings.JpegQualityLevel, 1, 100);
                        break;

                    case PngBitmapEncoder pngBitmapEncoder:
                        pngBitmapEncoder.Interlace = _settings.PngInterlaceOption;
                        break;

                    case TiffBitmapEncoder tiffEncoder:
                        tiffEncoder.Compression = _settings.TiffCompressOption;
                        break;
                }
            }
        }

        private BitmapSource Transform(BitmapSource source)
        {
            if (_settings.UseAiSuperResolution)
            {
                return TransformWithAi(source);
            }

            var originalWidth = source.PixelWidth;
            var originalHeight = source.PixelHeight;
            var width = _settings.SelectedSize.GetPixelWidth(originalWidth, source.DpiX);
            var height = _settings.SelectedSize.GetPixelHeight(originalHeight, source.DpiY);

            if (_settings.IgnoreOrientation
                && !_settings.SelectedSize.HasAuto
                && _settings.SelectedSize.Unit != ResizeUnit.Percent
                && originalWidth < originalHeight != (width < height))
            {
                var temp = width;
                width = height;
                height = temp;
            }

            var scaleX = width / originalWidth;
            var scaleY = height / originalHeight;

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

            if (_settings.ShrinkOnly
                && _settings.SelectedSize.Unit != ResizeUnit.Percent
                && (scaleX >= 1 || scaleY >= 1))
            {
                return source;
            }

            var scaledBitmap = new TransformedBitmap(source, new ScaleTransform(scaleX, scaleY));
            if (_settings.SelectedSize.Fit == ResizeFit.Fill
                && (scaledBitmap.PixelWidth > width
                || scaledBitmap.PixelHeight > height))
            {
                var x = (int)(((originalWidth * scaleX) - width) / 2);
                var y = (int)(((originalHeight * scaleY) - height) / 2);

                return new CroppedBitmap(scaledBitmap, new Int32Rect(x, y, (int)width, (int)height));
            }

            return scaledBitmap;
        }

        private BitmapSource TransformWithAi(BitmapSource source)
        {
            var scaleFactor = Math.Max(2, _settings.AiSuperResolutionScale);

            var context = new AiSuperResolutionContext(_file);
            var aiResult = _aiSuperResolutionService.ApplySuperResolution(source, scaleFactor, context) ?? source;

            // If the AI implementation already returned the desired scale, use it as-is.
            var expectedWidth = source.PixelWidth * (double)scaleFactor;
            var expectedHeight = source.PixelHeight * (double)scaleFactor;
            if (aiResult.PixelWidth >= expectedWidth && aiResult.PixelHeight >= expectedHeight)
            {
                return aiResult;
            }

            var scaleX = expectedWidth / aiResult.PixelWidth;
            var scaleY = expectedHeight / aiResult.PixelHeight;

            return new TransformedBitmap(aiResult, new ScaleTransform(scaleX, scaleY));
        }

        /// <summary>
        /// Checks original metadata by writing an image containing the given metadata into a memory stream.
        /// In case of errors, we try to rebuild the metadata object and check again.
        /// We return null if we were not able to get hold of valid metadata.
        /// </summary>
        private BitmapMetadata GetValidMetadata(BitmapMetadata originalMetadata, BitmapSource transformedBitmap, Guid containerFormat)
        {
            if (originalMetadata == null)
            {
                return null;
            }

            // Check if the original metadata is valid
            var frameWithOriginalMetadata = CreateBitmapFrame(transformedBitmap, originalMetadata);
            if (EnsureFrameIsValid(frameWithOriginalMetadata))
            {
                return originalMetadata;
            }

            // Original metadata was invalid. We try to rebuild the metadata object from the scratch and discard invalid metadata fields
            var recreatedMetadata = BuildMetadataFromTheScratch(originalMetadata);
            var frameWithRecreatedMetadata = CreateBitmapFrame(transformedBitmap, recreatedMetadata);
            if (EnsureFrameIsValid(frameWithRecreatedMetadata))
            {
                return recreatedMetadata;
            }

            // Seems like we have an invalid metadata object. ImageResizer will fail when trying to write the image to disk. We discard all metadata to be able to save the image.
            return null;

            // The safest way to check if the metadata object is valid is to call Save() on the encoder.
            // I tried other ways to check if metadata is valid (like calling Clone() on the metadata object) but this was not reliable resulting in a few github issues.
            bool EnsureFrameIsValid(BitmapFrame frameToBeChecked)
            {
                try
                {
                    var encoder = CreateEncoder(containerFormat);
                    encoder.Frames.Add(frameToBeChecked);
                    using (var testStream = new MemoryStream())
                    {
                        encoder.Save(testStream);
                    }

                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Read all metadata and build up metadata object from the scratch. Discard invalid (unreadable/unwritable) metadata.
        /// </summary>
        private static BitmapMetadata BuildMetadataFromTheScratch(BitmapMetadata originalMetadata)
        {
            try
            {
                var metadata = new BitmapMetadata(originalMetadata.Format);
                var listOfMetadata = originalMetadata.GetListOfMetadata();
                foreach (var (metadataPath, value) in listOfMetadata)
                {
                    if (value is BitmapMetadata bitmapMetadata)
                    {
                        var innerMetadata = new BitmapMetadata(bitmapMetadata.Format);
                        metadata.SetQuerySafe(metadataPath, innerMetadata);
                    }
                    else
                    {
                        metadata.SetQuerySafe(metadataPath, value);
                    }
                }

                return metadata;
            }
            catch (ArgumentException ex)
            {
                Debug.WriteLine(ex);

                return null;
            }
        }

        private static BitmapFrame CreateBitmapFrame(BitmapSource transformedBitmap, BitmapMetadata metadata)
        {
            return BitmapFrame.Create(
                transformedBitmap,
                thumbnail: null, /* should be null, see #15413 */
                metadata,
                colorContexts: null /* should be null, see #14866 */ );
        }

        private string GetDestinationPath(BitmapEncoder encoder)
        {
            var directory = _destinationDirectory ?? _fileSystem.Path.GetDirectoryName(_file);
            var originalFileName = _fileSystem.Path.GetFileNameWithoutExtension(_file);

            var supportedExtensions = encoder.CodecInfo.FileExtensions.Split(',');
            var extension = _fileSystem.Path.GetExtension(_file);
            if (!supportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                extension = supportedExtensions.FirstOrDefault();
            }

            // Remove directory characters from the size's name.
            string sizeNameSanitized = _settings.UseAiSuperResolution
                ? AiSuperResolutionFormatter.FormatScaleName(_settings.AiSuperResolutionScale)
                : _settings.SelectedSize.Name;
            sizeNameSanitized = sizeNameSanitized
                .Replace('\\', '_')
                .Replace('/', '_');

            // Using CurrentCulture since this is user facing
            var selectedWidth = _settings.UseAiSuperResolution ? encoder.Frames[0].PixelWidth : _settings.SelectedSize.Width;
            var selectedHeight = _settings.UseAiSuperResolution ? encoder.Frames[0].PixelHeight : _settings.SelectedSize.Height;
            var fileName = string.Format(
                CultureInfo.CurrentCulture,
                _settings.FileNameFormat,
                originalFileName,
                sizeNameSanitized,
                selectedWidth,
                selectedHeight,
                encoder.Frames[0].PixelWidth,
                encoder.Frames[0].PixelHeight);

            // Remove invalid characters from the final file name.
            fileName = fileName
                .Replace(':', '_')
                .Replace('*', '_')
                .Replace('?', '_')
                .Replace('"', '_')
                .Replace('<', '_')
                .Replace('>', '_')
                .Replace('|', '_');

            // Avoid creating not recommended filenames
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
