using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace ColorPickerAlpha
{
    public class ColorPicker
    {
        //Interops with C++
        [DllImport("user32.dll")]
        static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [DllImport("gdi32.dll")]
        static extern uint GetPixel(IntPtr hdc, int nXPos, int nYPos);

        [DllImport("user32.dll")]
        internal static extern bool GetPhysicalCursorPos(ref CursorPoint lpPoint);
        [DllImport("user32.dll")]
        internal static extern bool SetProcessDPIAware(); //TODO: set correctly

        public struct CursorPoint
        {
            public int X;
            public int Y;
        }

        static public Color GetPixelColor(int x, int y)
        {
            IntPtr hdc = GetDC(IntPtr.Zero);
            uint pixel = GetPixel(hdc, x, y);
            ReleaseDC(IntPtr.Zero, hdc);
            Color color = Color.FromArgb(
                (int)(pixel & 0x000000FF),
                (int)(pixel & 0x0000FF00) >> 8,
                (int)(pixel & 0x00FF0000) >> 16);
            return color;
        }

        static public (int x, int y) GetPhysicalCursorCoords()
        {
            CursorPoint cursorPos = new CursorPoint();
            GetPhysicalCursorPos(ref cursorPos);

            return (cursorPos.X, cursorPos.Y);
        }
    }
}
