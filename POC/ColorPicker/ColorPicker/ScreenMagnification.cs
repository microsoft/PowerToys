using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media.Imaging;

namespace ColorPicker
{
    static class ScreenMagnification
    {
        public static BitmapImage GetMagnificationImage(Point topLeft, int width, int height)
        {
            Bitmap screenSection = CaptureScreenShot(topLeft, width, height);
            return BitmapToImageSource(screenSection);
        }

        private static Bitmap CaptureScreenShot(Point topLeft, int width, int height)
        {
            Bitmap bitmap = new Bitmap(width, height);

            using (Graphics gr = Graphics.FromImage(bitmap))
            {
                gr.CopyFromScreen(topLeft, Point.Empty, new Size(width, height));
            }

            return bitmap;
        }

        // Source: https://stackoverflow.com/questions/94456/load-a-wpf-bitmapimage-from-a-system-drawing-bitmap
        private static BitmapImage BitmapToImageSource(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, ImageFormat.Bmp);
                memory.Position = 0;
                BitmapImage bitmapimage = new BitmapImage();
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();

                return bitmapimage;
            }
        }
    }
}
