// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Abstractions;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ManagedCommon;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Wox.Infrastructure.Image
{
    public static class ImageLoader
    {
        private static readonly IFileSystem FileSystem = new FileSystem();
        private static readonly IPath Path = FileSystem.Path;
        private static readonly IFile File = FileSystem.File;
        private static readonly IDirectory Directory = FileSystem.Directory;

        private static readonly ImageCache ImageCache = new ImageCache();
        private static readonly ConcurrentDictionary<string, string> GuidToKey = new ConcurrentDictionary<string, string>();

        private static IImageHashGenerator _hashGenerator;

        public static string ErrorIconPath { get; set; }

        public static string DefaultIconPath { get; set; }

        private static readonly string[] ImageExtensions =
        {
            ".png",
            ".jpg",
            ".jpeg",
            ".gif",
            ".bmp",
            ".tiff",
            ".ico",
        };

        public static void Initialize(Theme theme)
        {
            _hashGenerator = new ImageHashGenerator();

            foreach (var icon in new[] { Constant.DefaultIcon, Constant.ErrorIcon, Constant.LightThemedDefaultIcon, Constant.LightThemedErrorIcon })
            {
                BitmapImage bmi = new BitmapImage();
                bmi.BeginInit();
                bmi.UriSource = new Uri(icon);
                bmi.CacheOption = BitmapCacheOption.OnLoad;
                bmi.EndInit();
                ImageSource img = bmi;
                img.Freeze();
                ImageCache[icon] = img;
            }

            UpdateIconPath(theme);
            Task.Run(() =>
            {
                Stopwatch.Normal("ImageLoader.Initialize - Preload images cost", () =>
                {
                    ImageCache.Usage.AsParallel().ForAll(x =>
                    {
                        Load(x.Key);
                    });
                });

                Log.Info($"Number of preload images is <{ImageCache.Usage.Count}>, Images Number: {ImageCache.CacheSize()}, Unique Items {ImageCache.UniqueImagesInCache()}", MethodBase.GetCurrentMethod().DeclaringType);
            });
        }

        public static void Save()
        {
            ImageCache.Cleanup();
            ImageCache.Save();
        }

        // Todo : Update it with icons specific to each theme.
        public static void UpdateIconPath(Theme theme)
        {
            if (theme == Theme.Light || theme == Theme.HighContrastWhite)
            {
                ErrorIconPath = Constant.LightThemedErrorIcon;
                DefaultIconPath = Constant.LightThemedDefaultIcon;
            }
            else
            {
                ErrorIconPath = Constant.ErrorIcon;
                DefaultIconPath = Constant.DefaultIcon;
            }
        }

        private class ImageResult
        {
            public ImageResult(ImageSource imageSource, ImageType imageType)
            {
                ImageSource = imageSource;
                ImageType = imageType;
            }

            public ImageType ImageType { get; }

            public ImageSource ImageSource { get; }
        }

        private enum ImageType
        {
            File,
            Folder,
            Data,
            ImageFile,
            Error,
            Cache,
        }

        private static ImageResult LoadInternal(string path, bool loadFullImage = false)
        {
            ImageSource image;
            ImageType type = ImageType.Error;
            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    return new ImageResult(ImageCache[ErrorIconPath], ImageType.Error);
                }

                if (ImageCache.ContainsKey(path))
                {
                    return new ImageResult(ImageCache[path], ImageType.Cache);
                }

                // Using OrdinalIgnoreCase since this is internal and used with paths
                if (path.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    var imageSource = new BitmapImage(new Uri(path));
                    imageSource.Freeze();
                    return new ImageResult(imageSource, ImageType.Data);
                }

                if (!Path.IsPathRooted(path))
                {
                    path = Path.Combine(Constant.ProgramDirectory, "Images", Path.GetFileName(path));
                }

                if (Directory.Exists(path))
                {
                    /* Directories can also have thumbnails instead of shell icons.
                     * Generating thumbnails for a bunch of folders while scrolling through
                     * results from Everything makes a big impact on performance and
                     * Wox responsibility.
                     * - Solution: just load the icon
                     */
                    type = ImageType.Folder;
                    image = WindowsThumbnailProvider.GetThumbnail(path, Constant.ThumbnailSize, Constant.ThumbnailSize, ThumbnailOptions.IconOnly);
                }
                else if (File.Exists(path))
                {
                    // Using InvariantCulture since this is internal
                    var extension = Path.GetExtension(path).ToLower(CultureInfo.InvariantCulture);
                    if (ImageExtensions.Contains(extension))
                    {
                        type = ImageType.ImageFile;
                        if (loadFullImage)
                        {
                            image = LoadFullImage(path);
                        }
                        else
                        {
                            /* Although the documentation for GetImage on MSDN indicates that
                             * if a thumbnail is available it will return one, this has proved to not
                             * be the case in many situations while testing.
                             * - Solution: explicitly pass the ThumbnailOnly flag
                             */
                            image = WindowsThumbnailProvider.GetThumbnail(path, Constant.ThumbnailSize, Constant.ThumbnailSize, ThumbnailOptions.ThumbnailOnly);
                        }
                    }
                    else if (extension == ".pdf" && WindowsThumbnailProvider.DoesPdfUseAcrobatAsProvider())
                    {
                        // The PDF thumbnail provider from Adobe Reader and Acrobat Pro lets crash PT Run with an Dispatcher exception. (https://github.com/microsoft/PowerToys/issues/18166)
                        // To not run into the crash, we only request the icon of PDF files if the PDF thumbnail handler is set to Adobe Reader/Acrobat Pro.
                        type = ImageType.File;
                        image = WindowsThumbnailProvider.GetThumbnail(path, Constant.ThumbnailSize, Constant.ThumbnailSize, ThumbnailOptions.IconOnly);
                    }
                    else
                    {
                        type = ImageType.File;
                        image = WindowsThumbnailProvider.GetThumbnail(path, Constant.ThumbnailSize, Constant.ThumbnailSize, ThumbnailOptions.RESIZETOFIT);
                    }
                }
                else
                {
                    image = ImageCache[ErrorIconPath];
                    path = ErrorIconPath;
                }

                if (type != ImageType.Error)
                {
                    image.Freeze();
                }
            }
            catch (System.Exception e)
            {
                Log.Exception($"Failed to get thumbnail for {path}", e, MethodBase.GetCurrentMethod().DeclaringType);
                type = ImageType.Error;
                image = ImageCache[ErrorIconPath];
                ImageCache[path] = image;
            }

            return new ImageResult(image, type);
        }

        private const bool _enableImageHash = true;

        public static ImageSource Load(string path, bool loadFullImage = false)
        {
            var imageResult = LoadInternal(path, loadFullImage);

            var img = imageResult.ImageSource;
            if (imageResult.ImageType != ImageType.Error && imageResult.ImageType != ImageType.Cache)
            {
                // we need to get image hash
                string hash = _enableImageHash ? _hashGenerator.GetHashFromImage(img) : null;

                if (hash != null)
                {
                    if (GuidToKey.TryGetValue(hash, out string key))
                    {
                        // image already exists
                        if (ImageCache.Usage.TryGetValue(path, out _))
                        {
                            img = ImageCache[key];
                        }
                    }
                    else
                    {
                        // new guid
                        GuidToKey[hash] = path;
                    }
                }

                // update cache
                ImageCache[path] = img;
            }

            return img;
        }

        private static BitmapImage LoadFullImage(string path)
        {
            BitmapImage image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path);
            image.EndInit();
            return image;
        }
    }
}
