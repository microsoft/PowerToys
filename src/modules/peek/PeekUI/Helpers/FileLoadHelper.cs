using System;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace PeekUI.Helpers
{
    public class DimensionData
    {
        public Size Size { get; set; }
        public Rotation Rotation { get; set; }
    }

    public static class FileLoadHelper
    {
        public static Task<DimensionData> LoadDimensionsAsync(string filename)
        {
            return Task.Run(() =>
            {
                Size size = new Size(0, 0);
                try
                {
                    using (FileStream stream = File.OpenRead(filename))
                    {
                        string extension = Path.GetExtension(stream.Name);
                        if (FileTypeHelper.IsSupportedImage(extension))
                        {
                            using (System.Drawing.Image sourceImage = System.Drawing.Image.FromStream(stream, false, false))
                            {
                                var rotation = EvaluateRotationToApply(sourceImage);
                                if (rotation == Rotation.Rotate90 || rotation == Rotation.Rotate270)
                                {
                                    size = new Size(sourceImage.Height, sourceImage.Width);
                                }
                                else
                                {
                                    size = new Size(sourceImage.Width, sourceImage.Height);
                                }

                                return Task.FromResult(new DimensionData { Size = size, Rotation = rotation });
                            }
                        }
                        else
                        {
                            return Task.FromResult(new DimensionData { Size = size, Rotation = Rotation.Rotate0 });
                        }
                    }
                }
                catch (Exception)
                {
                    return Task.FromResult(new DimensionData { Size = size, Rotation = Rotation.Rotate0 });
                }
            });
        }

        public static async Task<BitmapSource> LoadThumbnailAsync(string filename, bool iconFallback)
        {
            var thumbnail = await Task.Run(() =>
            {
                var bitmapSource = ThumbnailHelper.GetThumbnail(filename, iconFallback);
                bitmapSource.Freeze();
                return bitmapSource;
            });

            return thumbnail;
        }

        public static Task<BitmapSource> LoadIconAsync(string filename)
        {
            return Task.Run(() =>
            {
                var bitmapSource = ThumbnailHelper.GetIcon(filename);
                bitmapSource.Freeze();
                return bitmapSource;
            });
        }

        public static Task<BitmapImage> LoadFullImageAsync(string filename, Rotation rotation)
        {
            return Task.Run(() =>
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(filename);
                bitmap.Rotation = rotation;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            });
        }

        private static Rotation EvaluateRotationToApply(System.Drawing.Image image)
        {
            PropertyItem? property = image.PropertyItems?.FirstOrDefault(p => p.Id == 274);

            if (property != null && property.Value != null && property.Value.Length > 0)
            {
                int orientation = property.Value[0];

                if (orientation == 6)
                {
                    return Rotation.Rotate90;
                }

                if (orientation == 3)
                {
                    return Rotation.Rotate180;
                }

                if (orientation == 8)
                {
                    return Rotation.Rotate270;
                }
            }

            return Rotation.Rotate0;
        }
    }
}