// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>
/// Captures the full desktop (including the mouse cursor) to a PNG. Used only by the pipeline path
/// of <see cref="UITestBase"/>, which fires <see cref="TimerCallback"/> on a one-second timer so a
/// failed CI run carries a frame-by-frame trail. Ported from the legacy harness — winappcli has no
/// equivalent full-desktop capture, so this stays native (GDI).
/// </summary>
internal static class ScreenCapture
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("gdi32.dll")]
    private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorInfo(out CURSORINFO pci);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DrawIconEx(IntPtr hdc, int x, int y, IntPtr hIcon, int cx, int cy, int istepIfAniCur, IntPtr hbrFlickerFreeDraw, int diFlags);

    private const int CURSORSHOWING = 0x00000001;
    private const int DESKTOPHORZRES = 118;
    private const int DESKTOPVERTRES = 117;
    private const int DINORMAL = 0x0003;

    /// <summary>A point with X and Y coordinates (Win32 <c>POINT</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    /// <summary>Cursor state/handle/position (Win32 <c>CURSORINFO</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CURSORINFO
    {
        public int CbSize;
        public int Flags;
        public IntPtr HCursor;
        public POINT PTScreenPos;
    }

    /// <summary>
    /// Timer callback: capture one screenshot into the directory passed as <paramref name="state"/>.
    /// Tolerant — a capture failure must never crash the timer thread.
    /// </summary>
    public static void TimerCallback(object? state)
    {
        try
        {
            if (state is string directory)
            {
                CaptureScreenshot(directory);
            }
        }
        catch
        {
            // Best-effort capture; swallow so the timer keeps firing.
        }
    }

    private static void CaptureScreenshot(string directory)
    {
        var filePath = Path.Combine(directory, $"screenshot_{DateTime.Now:yyyyMMdd_HHmmssfff}.png");
        CaptureScreenWithMouse(filePath);
    }

    private static void CaptureScreenWithMouse(string filePath)
    {
        var hdc = GetDC(IntPtr.Zero);
        var screenWidth = GetDeviceCaps(hdc, DESKTOPHORZRES);
        var screenHeight = GetDeviceCaps(hdc, DESKTOPVERTRES);
        ReleaseDC(IntPtr.Zero, hdc);

        var bounds = new Rectangle(0, 0, screenWidth, screenHeight);
        using var bitmap = new Bitmap(bounds.Width, bounds.Height);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);

            var cursorInfo = default(CURSORINFO);
            cursorInfo.CbSize = Marshal.SizeOf<CURSORINFO>();
            if (GetCursorInfo(out cursorInfo) && cursorInfo.Flags == CURSORSHOWING)
            {
                var hdcDest = g.GetHdc();
                DrawIconEx(hdcDest, cursorInfo.PTScreenPos.X, cursorInfo.PTScreenPos.Y, cursorInfo.HCursor, 0, 0, 0, IntPtr.Zero, DINORMAL);
                g.ReleaseHdc(hdcDest);
            }
        }

        bitmap.Save(filePath, ImageFormat.Png);
    }
}
