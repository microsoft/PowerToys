using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wox.Infrastructure.Logger;
using Wox.Infrastructure.Storage;

namespace Wox.Infrastructure.Image
{
    public static class ImageLoader
    {
        private static readonly ImageCache ImageCache = new ImageCache();
        private static BinaryStorage<ConcurrentDictionary<string, int>> _storage;
        private static readonly ConcurrentDictionary<string, string> GuidToKey = new ConcurrentDictionary<string, string>();
        private static IImageHashGenerator _hashGenerator;


        private static readonly string[] ImageExtensions =
        {
            ".png",
            ".jpg",
            ".jpeg",
            ".gif",
            ".bmp",
            ".tiff",
            ".ico"
        };


        public static void Initialize()
        {
            _storage = new BinaryStorage<ConcurrentDictionary<string, int>>("Image");
            _hashGenerator = new ImageHashGenerator();
            ImageCache.Usage = _storage.TryLoad(new ConcurrentDictionary<string, int>());

            foreach (var icon in new[] { Constant.DefaultIcon, Constant.ErrorIcon })
            {
                ImageSource img = new BitmapImage(new Uri(icon));
                img.Freeze();
                ImageCache[icon] = img;
            }
            Task.Run(() =>
            {
                Stopwatch.Normal("|ImageLoader.Initialize|Preload images cost", () =>
                {
                    ImageCache.Usage.AsParallel().Where(i => !ImageCache.ContainsKey(i.Key)).ForAll(x =>
                    {
                        Load(x.Key);
                    });
                });
                Log.Info($"|ImageLoader.Initialize|Number of preload images is <{ImageCache.Usage.Count}>, Images Number: {ImageCache.CacheSize()}, Unique Items {ImageCache.UniqueImagesInCache()}");
            });
        }

        public static void Save()
        {
            ImageCache.Cleanup();
            _storage.Save(ImageCache.Usage);
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
            Cache
        }

        private static ImageResult LoadInternal(string path, bool loadFullImage = false)
        {
            ImageSource image;
            ImageType type = ImageType.Error;
            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    return new ImageResult(ImageCache[Constant.ErrorIcon], ImageType.Error);
                }
                if (ImageCache.ContainsKey(path))
                {
                    return new ImageResult(ImageCache[path], ImageType.Cache);
                }

                if (path.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    return new ImageResult(new BitmapImage(new Uri(path)), ImageType.Data);
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
                    image = WindowsThumbnailProvider.GetThumbnail(path, Constant.ThumbnailSize,
                        Constant.ThumbnailSize, ThumbnailOptions.IconOnly);

                }
                else if (File.Exists(path))
                {
                    var extension = Path.GetExtension(path).ToLower();
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
                            image = WindowsThumbnailProvider.GetThumbnail(path, Constant.ThumbnailSize,
                                Constant.ThumbnailSize, ThumbnailOptions.ThumbnailOnly);
                        }
                    }
                    else
                    {
                        type = ImageType.File;
                        image = WindowsThumbnailProvider.GetThumbnail(path, Constant.ThumbnailSize,
                            Constant.ThumbnailSize, ThumbnailOptions.None);
                    }
                }
                else
                {
                    image = ImageCache[Constant.ErrorIcon];
                    path = Constant.ErrorIcon;
                }

                if (type != ImageType.Error)
                {
                    image.Freeze();
                }
            }
            catch (System.Exception e)
            {
                Log.Exception($"|ImageLoader.Load|Failed to get thumbnail for {path}", e);
                type = ImageType.Error;
                image = ImageCache[Constant.ErrorIcon];
                ImageCache[path] = image;
            }
            return new ImageResult(image, type);
        }

        private static bool EnableImageHash = true;

        public static ImageSource Load(string path, bool loadFullImage = false)
        {
            // return LoadInternal(path, loadFullImage).ImageSource;
            var imageResult = LoadInternal(path, loadFullImage);

            var img = imageResult.ImageSource;
            if (imageResult.ImageType != ImageType.Error && imageResult.ImageType != ImageType.Cache)
            { // we need to get image hash
                string hash = EnableImageHash ? _hashGenerator.GetHashFromImage(img) : null;
                if (hash != null)
                { 
                    if (GuidToKey.TryGetValue(hash, out string key))
                    { // image already exists
                        img = ImageCache[key];
                    }
                    else
                    { // new guid
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
