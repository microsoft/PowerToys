using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImageResizer.Test
{
    static class BitmapSourceExtensions
    {
        public static Color GetFirstPixel(this BitmapSource source)
        {
            var pixel = new byte[4];
            new FormatConvertedBitmap(
                    new CroppedBitmap(source, new Int32Rect(0, 0, 1, 1)),
                    PixelFormats.Bgra32,
                    destinationPalette: null,
                    alphaThreshold: 0)
                .CopyPixels(pixel, 4, 0);

            return Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]);
        }
    }
}
