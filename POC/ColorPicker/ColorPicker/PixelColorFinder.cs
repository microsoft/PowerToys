using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Input;

namespace ColorPicker.ColorPickingFunctionality
{
    static class PixelColorFinder
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        private const uint CLR_INVALID = 0xFFFFFFFF;
        private const uint FIRST_BYTE_SET = 0x000000FF;
        private const uint SECOND_BYTE_SET = 0x0000FF00;
        private const uint THIRD_BYTE_SET = 0x00FF0000;

        /// <summary>
        /// Retrieves the cursor's position, in screen coordinates.
        /// </summary>
        /// <param name="lpPoint">A pointer to a POINT structure that receives the screen coordinates of the cursor.</param>
        /// <returns>Returns nonzero if successful or zero otherwise. To get extended error information, call GetLastError.</returns>
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        /// <summary>
        /// Get the RGB value of a pixel.
        /// </summary>
        /// <param name="hDC">A handle to the device context.</param>
        /// <param name="xCoord">The x-coordinate, in logical units, of the pixel to be examined.</param>
        /// <param name="yCoord">The y-coordinate, in logical units, of the pixel to be examined.</param>
        /// <returns>A COLORREF int. The first byte is red, second is green and third is blue. On error it returns 0xFFFFFFFF</returns>
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

        public static Color GetColorUnderCursor()
        {
            IntPtr hDC = SafeGetWindowDC(IntPtr.Zero);

            POINT cursorPosition;
            SafeGetCursorPos(out cursorPosition);

            uint colorRef = SafeGetPixel(hDC, cursorPosition.x, cursorPosition.y);

            SafeReleaseWindowDC(hDC);

            byte red = ParseRed(colorRef);
            byte green = ParseGreen(colorRef);
            byte blue = ParseBlue(colorRef);

            return Color.FromRgb(red, green, blue);
        }

        private static void SafeGetCursorPos(out POINT lpPoint)
        {
            if (!GetCursorPos(out lpPoint))
            {
                throw new InternalSystemCallException("Failed to get cursor position");
            }
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

        private static IntPtr SafeGetWindowDC(IntPtr hWnd)
        {
            IntPtr hDC = GetDC(hWnd);
            if (hDC == null)
            {
                throw new InternalSystemCallException("Failed to get hDC");
            }
            return hDC;
        }

        private static void SafeReleaseWindowDC(IntPtr hDC)
        {
            if (ReleaseDC(IntPtr.Zero, hDC) == 0)
            {
                throw new InternalSystemCallException("Failed to release hDC");
            }
        }

        private static byte ParseRed(uint colorRef) => (byte)(colorRef & FIRST_BYTE_SET);

        private static byte ParseGreen(uint colorRef) => (byte)((colorRef & SECOND_BYTE_SET) >> 8);

        private static byte ParseBlue(uint colorRef) => (byte)((colorRef & THIRD_BYTE_SET) >> 16);
    }
}
