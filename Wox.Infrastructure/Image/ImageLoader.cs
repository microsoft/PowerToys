using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
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
            _storage = new BinaryStorage<ConcurrentDictionary<string, int>> ("Image");
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
                    ImageCache.Usage.AsParallel().Where(i => !ImageCache.ContainsKey(i.Key)).ForAll(i =>
                    {
                        var img = Load(i.Key);
                        if (img != null)
                        {
                            ImageCache[i.Key] = img;
                        }
                    });
                });
                Log.Info($"|ImageLoader.Initialize|Number of preload images is <{ImageCache.Usage.Count}>");
            });
        }

        public static void Save()
        {
            ImageCache.Cleanup();
            _storage.Save(ImageCache.Usage);
        }
        
        public static ImageSource Load(string path, bool loadFullImage = false)
        {
            ImageSource image;
            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    return ImageCache[Constant.ErrorIcon];
                }
                if (ImageCache.ContainsKey(path))
                {
                    return ImageCache[path];
                }
                
                if (path.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    return new BitmapImage(new Uri(path));
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
                    image = WindowsThumbnailProvider.GetThumbnail(path, Constant.ThumbnailSize,
                        Constant.ThumbnailSize, ThumbnailOptions.IconOnly);
                }
                else if (File.Exists(path))
                {
                    var extension = Path.GetExtension(path).ToLower();
                    if (ImageExtensions.Contains(extension))
                    {
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
                        image = WindowsThumbnailProvider.GetThumbnail(path, Constant.ThumbnailSize,
                            Constant.ThumbnailSize, ThumbnailOptions.None);
                    }
                }
                else
                {
                    image = ImageCache[Constant.ErrorIcon];
                    path = Constant.ErrorIcon;
                }
                ImageCache[path] = image;
                image.Freeze();
            }
            catch (System.Exception e)
            {
                Log.Exception($"|ImageLoader.Load|Failed to get thumbnail for {path}", e);

                image = ImageCache[Constant.ErrorIcon];
                ImageCache[path] = image;
            }
            return image;
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
