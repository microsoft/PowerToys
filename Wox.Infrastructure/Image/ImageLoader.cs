using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wox.Infrastructure.Logger;
using Wox.Infrastructure.Storage;

namespace Wox.Infrastructure.Image
{
    public static class ImageLoader
    {
        private static readonly ConcurrentDictionary<string, ImageSource> ImageSources = new ConcurrentDictionary<string, ImageSource>();


        private static readonly string[] ImageExtions =
        {
            ".png",
            ".jpg",
            ".jpeg",
            ".gif",
            ".bmp",
            ".tiff",
            ".ico"
        };

        private static readonly ImageCache _cache;
        private static readonly BinaryStorage<ImageCache> _storage;

        static ImageLoader()
        {
            _storage = new BinaryStorage<ImageCache>();
            _cache = _storage.Load();
        }

        public static void Save()
        {
            _cache.Cleanup();
            _storage.Save();
        }

        private static ImageSource ShellIcon(string fileName)
        {
            try
            {
                // http://blogs.msdn.com/b/oldnewthing/archive/2011/01/27/10120844.aspx
                var shfi = new SHFILEINFO();
                var himl = SHGetFileInfo(
                    fileName,
                    FILE_ATTRIBUTE_NORMAL,
                    ref shfi,
                    (uint)Marshal.SizeOf(shfi),
                    SHGFI_SYSICONINDEX
                );

                if (himl != IntPtr.Zero)
                {
                    var hIcon = ImageList_GetIcon(himl, shfi.iIcon, ILD_NORMAL);
                    // http://stackoverflow.com/questions/1325625/how-do-i-display-a-windows-file-icon-in-wpf
                    var img = Imaging.CreateBitmapSourceFromHIcon(
                        hIcon,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions()
                    );
                    DestroyIcon(hIcon);
                    return img;
                }
                else
                {
                    return new BitmapImage(new Uri(Constant.ErrorIcon));
                }
            }
            catch (System.Exception e)
            {
                Log.Exception(e);
                return ImageSources[Constant.ErrorIcon];
            }
        }

        public static void PreloadImages()
        {
            foreach (var icon in new[] { Constant.DefaultIcon, Constant.ErrorIcon })
            {
                ImageSource img = new BitmapImage(new Uri(icon));
                img.Freeze();
                ImageSources[icon] = img;
            }
            Task.Run(() =>
            {
                Stopwatch.Debug("Preload images from cache", () =>
                {
                    _cache.TopUsedImages.AsParallel().Where(i => !ImageSources.ContainsKey(i.Key)).ForAll(i =>
                    {
                        var img = Load(i.Key);
                        if (img != null)
                        {
                            ImageSources[i.Key] = img;
                        }
                    });
                });
            });
            Log.Info($"Preload {_cache.TopUsedImages.Count} images from cache");
        }

        public static ImageSource Load(string path)
        {
            ImageSource image;
            if (string.IsNullOrEmpty(path))
            {
                image = ImageSources[Constant.ErrorIcon];
                _cache.Add(Constant.ErrorIcon);
            }
            else if (ImageSources.ContainsKey(path))
            {
                image = ImageSources[path];
                _cache.Add(path);
            }
            else
            {
                if (path.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    image = new BitmapImage(new Uri(path));
                }
                else if (Path.IsPathRooted(path))
                {
                    if (Directory.Exists(path))
                    {
                        image = ShellIcon(path);
                    }
                    else if (File.Exists(path))
                    {
                        var externsion = Path.GetExtension(path).ToLower();
                        if (ImageExtions.Contains(externsion))
                        {
                            image = new BitmapImage(new Uri(path));
                        }
                        else
                        {
                            image = ShellIcon(path);
                        }
                    }
                    else
                    {
                        image = ImageSources[Constant.ErrorIcon];
                        path = Constant.ErrorIcon;
                    }
                }
                else
                {
                    var defaultDirectoryPath = Path.Combine(Constant.ProgramDirectory, "Images", Path.GetFileName(path));
                    if (File.Exists(defaultDirectoryPath))
                    {
                        image = new BitmapImage(new Uri(defaultDirectoryPath));
                    }
                    else
                    {
                        image = ImageSources[Constant.ErrorIcon];
                        path = Constant.ErrorIcon;
                    }
                }
                ImageSources[path] = image;
                _cache.Add(path);
                image.Freeze();
            }
            return image;
        }

        private const int NAMESIZE = 80;
        private const int MAX_PATH = 256;
        private const uint SHGFI_SYSICONINDEX = 0x000004000; // get system icon index
        private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
        private const uint ILD_NORMAL = 0x00000000;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEINFO
        {
            readonly IntPtr hIcon;
            internal readonly int iIcon;
            readonly uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)] readonly string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = NAMESIZE)] readonly string szTypeName;
        }
        
        [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("User32.dll")]
        private static extern int DestroyIcon(IntPtr hIcon);

        [DllImport("comctl32.dll")]
        private static extern IntPtr ImageList_GetIcon(IntPtr himl, int i, uint flags);
    }
}
