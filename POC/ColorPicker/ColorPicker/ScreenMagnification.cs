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

        [DllImport("User32.dll")]
        private static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("User32.dll")]
        private static extern void ReleaseDC(IntPtr hwnd, IntPtr dc);

        public static BitmapImage GetMagnificationImage(int x, int y, int width, int height)
        {
            IntPtr desktopDC = GetDC(IntPtr.Zero); // Get the full screen DC
            Graphics g = Graphics.FromHdc(desktopDC); // Get the full screen GFX device
            g.Dispose();
            ReleaseDC(IntPtr.Zero, desktopDC);



            Bitmap portionOf = CaptureScreenShot(x, y, width, height).Clone(new Rectangle(0, 0, width, height), PixelFormat.Format32bppRgb);

            return BitmapToImageSource(portionOf);
        }

        private static Bitmap CaptureScreenShot(int x, int y, int width, int height)
        {
            // get the bounding area of the screen containing (0,0)
            // remember in a multidisplay environment you don't know which display holds this point
            Rectangle bounds = Screen.GetBounds(Point.Empty);

            // create the bitmap to copy the screen shot to
            Bitmap bitmap = new Bitmap(width, height);

            // now copy the screen image to the graphics device from the bitmap
            using (Graphics gr = Graphics.FromImage(bitmap))
            {
                gr.CopyFromScreen(new Point(x, y), Point.Empty, new Size(width, height));
            }

            return bitmap;
        }

        private static BitmapImage BitmapToImageSource(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
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
