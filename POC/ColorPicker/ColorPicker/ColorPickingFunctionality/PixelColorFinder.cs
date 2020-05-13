using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Drawing;


namespace ColorPicker.ColorPickingFunctionality
{
    class PixelColorFinder
    {
        private const uint CLR_INVALID = 0xFFFFFFFF;
        private const uint FIRST_BYTE_SET = 0x000000FF;
        private const uint SECOND_BYTE_SET = 0x0000FF00;
        private const uint THIRD_BYTE_SET = 0x00FF0000;

        /// <summary>
        /// Get the RGB value of a pixel.
        /// </summary>
        /// <param name="hDC">A handle to the device context.</param>
        /// <param name="xCoord">The x-coordinate, in logical units, of the pixel to be examined.</param>
        /// <param name="yCoord">The y-coordinate, in logical units, of the pixel to be examined.</param>
        /// <returns>
        /// A COLORREF int. The first byte is red, second is green and third is blue. On error it returns 0xFFFFFFFF.
        /// </returns>
        [DllImport("gdi32.dll")]
        private static extern uint GetPixel(IntPtr hDC, int xCoord, int yCoord);

        /// <summary>
        /// Get a handle to the current device context.
        /// </summary>
        /// <param name="hWnd"> A handle to the window whose DC is to be retrieved. If this value is NULL, GetDC retrieves the DC for the entire screen.</param>
        /// <returns>The handle if succesful, or NULL for failure.</returns>
        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        /// <summary>
        /// Release the current display context.
        /// </summary>
        /// <param name="hWnd">A handle to the window whose DC is to be released.</param>
        /// <param name="hDC">A handle to the DC to be released.</param>
        /// <returns>1 for successful release, 0 otherwise.</returns>
        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        public static void HandleMouseClick(int x, int y)
        {
            uint colorRef = GetPixelValue(x, y);

            int red = ParseRed(colorRef);
            int green = ParseGreen(colorRef);
            int blue = ParseBlue(colorRef);

            // TODO: integrate this with UI at a later date
            Debug.WriteLine("R: {0} G: {1} B: {2}", red, green, blue);
        }

        private static uint GetPixelValue(int xCoord, int yCoord)
        {
            IntPtr hDC = SafeGetWindowDC();
            uint pixelValue = SafeGetPixel(hDC, xCoord, yCoord);
            SafeReleaseWindowDC(hDC);
            return pixelValue;
        }

        private static IntPtr SafeGetWindowDC()
        {
            IntPtr hDC = GetDC(IntPtr.Zero);
            if (hDC == null)
            {
                throw new InternalSystemCallException("Failed to get hDC");
            }
            return hDC;
        }

        private static uint SafeGetPixel(IntPtr hDC, int x, int y)
        {
            uint pixelValue = GetPixel(hDC, x, y);
            if (pixelValue == CLR_INVALID)
            {
                throw new InternalSystemCallException("Failed to get pixel");
            }
            return pixelValue;
        }

        private static void SafeReleaseWindowDC(IntPtr hDC)
        {
            if (ReleaseDC(IntPtr.Zero, hDC) == 0)
            {
                throw new InternalSystemCallException("Failed to release hDC");
            }
        }

        private static int ParseRed(uint colorRef) => (int)(colorRef & FIRST_BYTE_SET);

        private static int ParseGreen(uint colorRef) => (int)(colorRef & SECOND_BYTE_SET) >> 8;

        private static int ParseBlue(uint colorRef) => (int)(colorRef & THIRD_BYTE_SET) >> 16;
    }
}
