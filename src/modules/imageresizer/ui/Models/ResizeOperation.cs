// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageResizer.Properties;
using ImageResizer.Utilities;
using Microsoft.VisualBasic.FileIO;

namespace ImageResizer.Models
{
    internal class ResizeOperation
    {
        private readonly string _file;
        private readonly string _destinationDirectory;
        private readonly Settings _settings;

        public ResizeOperation(string file, string destinationDirectory, Settings settings)
        {
            _file = file;
            _destinationDirectory = destinationDirectory;
            _settings = settings;
        }

        public void Execute()
        {
            string path;
            using (var inputStream = File.OpenRead(_file))
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
                    encoder.Frames.Add(
                        BitmapFrame.Create(
                            Transform(originalFrame),
                            thumbnail: null,
                            (BitmapMetadata)originalFrame.Metadata, // TODO: Add an option to strip any metadata that doesn't affect rendering (issue #3)
                            colorContexts: null));
                }

                path = GetDestinationPath(encoder);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (var outputStream = File.Open(path, FileMode.CreateNew, FileAccess.Write))
                {
                    encoder.Save(outputStream);
                }
            }

            if (_settings.KeepDateModified)
            {
                File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(_file));
            }

            if (_settings.Replace)
            {
                var backup = GetBackupPath();
                File.Replace(path, _file, backup, ignoreMetadataErrors: true);
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
            var directory = _destinationDirectory ?? Path.GetDirectoryName(_file);
            var originalFileName = Path.GetFileNameWithoutExtension(_file);

            var supportedExtensions = encoder.CodecInfo.FileExtensions.Split(',');
            var extension = Path.GetExtension(_file);
            if (!supportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                extension = supportedExtensions.FirstOrDefault();
            }

            var fileName = string.Format(
                _settings.FileNameFormat,
                originalFileName,
                _settings.SelectedSize.Name,
                _settings.SelectedSize.Width,
                _settings.SelectedSize.Height,
                encoder.Frames[0].PixelWidth,
                encoder.Frames[0].PixelHeight);
            var path = Path.Combine(directory, fileName + extension);
            var uniquifier = 1;
            while (File.Exists(path))
            {
                path = Path.Combine(directory, fileName + " (" + uniquifier++ + ")" + extension);
            }

            return path;
        }

        private string GetBackupPath()
        {
            var directory = Path.GetDirectoryName(_file);
            var fileName = Path.GetFileNameWithoutExtension(_file);
            var extension = Path.GetExtension(_file);

            var path = Path.Combine(directory, fileName + ".bak" + extension);
            var uniquifier = 1;
            while (File.Exists(path))
            {
                path = Path.Combine(directory, fileName + " (" + uniquifier++ + ")" + ".bak" + extension);
            }

            return path;
        }
    }
}
