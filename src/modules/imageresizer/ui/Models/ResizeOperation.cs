// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

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
using ImageResizer.Properties;
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

        // Filenames to avoid according to https://docs.microsoft.com/en-us/windows/win32/fileio/naming-a-file#file-and-directory-names
        private static readonly string[] _avoidFilenames =
            {
                "CON", "PRN", "AUX", "NUL",
                "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
                "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
            };

        public ResizeOperation(string file, string destinationDirectory, Settings settings)
        {
            _file = file;
            _destinationDirectory = destinationDirectory;
            _settings = settings;
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

                var encoder = BitmapEncoder.Create(decoder.CodecInfo.ContainerFormat);
                if (!encoder.CanEncode())
                {
                    encoder = BitmapEncoder.Create(_settings.FallbackEncoder);
                }

                ConfigureEncoder(encoder);

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
                    BitmapMetadata metadata = (BitmapMetadata)originalFrame.Metadata;
                    if (metadata != null)
                    {
                        try
                        {
#if DEBUG
                            Debug.WriteLine($"### Processing metadata of file {_file}");
                            metadata.PrintsAllMetadataToDebugOutput();
#endif

                            // read all metadata and build up metadata object from the scratch. Discard invalid (unreadable/unwritable) metadata.
                            var newMetadata = new BitmapMetadata(metadata.Format);
                            var listOfMetadata = metadata.GetListOfMetadata();
                            foreach (var (metadataPath, value) in listOfMetadata)
                            {
                                if (value is BitmapMetadata bitmapMetadata)
                                {
                                    var innerMetadata = new BitmapMetadata(bitmapMetadata.Format);
                                    newMetadata.SetQuerySafe(metadataPath, innerMetadata);
                                }
                                else
                                {
                                    newMetadata.SetQuerySafe(metadataPath, value);
                                }
                            }

                            if (newMetadata.IsMetadataObjectValid())
                            {
                                metadata = newMetadata;
                            }
                            else
                            {
                                // Seems like we build an invalid metadata object. ImageResizer will fail when trying to write the image to disk. We discard all metadata to be able to save the image.
                                metadata = null;
                            }
                        }
                        catch (ArgumentException ex)
                        {
                            metadata = null;

                            Debug.WriteLine(ex);
                        }
                    }

                    if (_settings.RemoveMetadata && metadata != null)
                    {
                        // strip any metadata that doesn't affect rendering
                        var newMetadata = new BitmapMetadata(metadata.Format);

                        metadata.CopyMetadataPropertyTo(newMetadata, "System.Photo.Orientation");
                        metadata.CopyMetadataPropertyTo(newMetadata, "System.Image.ColorSpace");

                        metadata = newMetadata;
                    }

                    encoder.Frames.Add(
                        BitmapFrame.Create(
                            Transform(originalFrame),
                            thumbnail: null, /* should be null, see #15413 */
                            metadata,
                            colorContexts: null /* should be null, see #14866 */ ));
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

        private void ConfigureEncoder(BitmapEncoder encoder)
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

        private BitmapSource Transform(BitmapSource source)
        {
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
            string sizeNameSanitized = _settings.SelectedSize.Name;
            sizeNameSanitized = sizeNameSanitized
                .Replace('\\', '_')
                .Replace('/', '_');

            // Using CurrentCulture since this is user facing
            var fileName = string.Format(
                CultureInfo.CurrentCulture,
                _settings.FileNameFormat,
                originalFileName,
                sizeNameSanitized,
                _settings.SelectedSize.Width,
                _settings.SelectedSize.Height,
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
