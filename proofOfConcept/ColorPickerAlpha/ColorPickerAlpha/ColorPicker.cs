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
        internal static extern bool GetPhysicalCursorPos(ref Point lpPoint);
        [DllImport("user32.dll")]
        internal static extern bool SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT value); 

        public enum DPI_AWARENESS_CONTEXT
        {
            DPI_AWARENESS_CONTEXT_UNAWARE,             
            DPI_AWARENESS_CONTEXT_SYSTEM_AWARE,         
            DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE,    
            DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2, 
            DPI_AWARENESS_CONTEXT_UNAWARE_GDISCALED   
        }

        static public Color GetPixelColor(int x, int y)
        {
            DPI_AWARENESS_CONTEXT dpitype = DPI_AWARENESS_CONTEXT.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE;
            if (SetProcessDpiAwarenessContext(dpitype))
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
            else
            {
                throw new System.InvalidOperationException("Set process dpi aware failed");
            }
        }

        static public (int x, int y) GetPhysicalCursorCoords()
        {
            Point cursorPnt = new Point();
            GetPhysicalCursorPos(ref cursorPnt);

            return (cursorPnt.X, cursorPnt.Y);
        }

    }
}
