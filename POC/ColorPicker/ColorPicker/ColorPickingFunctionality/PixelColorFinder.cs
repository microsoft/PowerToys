using System;
using System.Diagnostics;
using System.Runtime.InteropServices;


namespace ColorPicker.ColorPickingFunctionality
{
    class PixelColorFinder
    {
        [DllImport("gdi32.dll")]
        private static extern int GetPixel(IntPtr hDC, int x, int y);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        public static void HandleMouseClick(int x, int y)
        {
            int colorRef = GetPixelValue(x, y);

            int red = parseRed(colorRef);
            int green = parseGreen(colorRef);
            int blue = parseBlue(colorRef);

            // Below is temporary - need to integrate this with UI at a later date
            Debug.WriteLine("R: {0} G: {1} B: {2}", red, green, blue);
        }

        private static int GetPixelValue(int x, int y)
        {
            IntPtr hDC = safeGetWindowDC();
            int pixelValue = safeGetPixel(hDC, x, y);
            safeReleaseWindowDC(hDC);
            return pixelValue;
        }

        private static IntPtr safeGetWindowDC()
        {
            IntPtr hDC = GetDC(IntPtr.Zero);
            if (hDC == null)
            {
                throw new InternalSystemCallException("Failed to get hDC");
            }
            return hDC;
        }

        private static int safeGetPixel(IntPtr hDC, int x, int y)
        {
            int pixelValue = GetPixel(hDC, x, y);
            if((uint)pixelValue == 0xFFFFFFFF)
            {
                throw new InternalSystemCallException("Failed to get pixel");
            }
            return pixelValue;
        }

        private static void safeReleaseWindowDC(IntPtr hDC)
        {
            if (ReleaseDC(IntPtr.Zero, hDC) == 0)
            {
                throw new InternalSystemCallException("Failed to release hDC");
            }
        }

        private static int parseRed(int colorRef)
        {
            return colorRef & 0x000000FF;
        }

        private static int parseGreen(int colorRef)
        {
            return (colorRef & 0x0000FF00) >> 8;
        }

        private static int parseBlue(int colorRef)
        {
            return (colorRef & 0x00FF0000) >> 16;
        }
    }
}

