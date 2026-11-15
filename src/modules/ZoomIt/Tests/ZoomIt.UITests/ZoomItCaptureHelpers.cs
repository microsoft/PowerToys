// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Graphics.Imaging;
using Windows.Media.Editing;

namespace Microsoft.PowerToys.ZoomIt.UITests;

internal static class ZoomItCaptureHelpers
{
    private delegate bool EnumWindowCallback(IntPtr window, IntPtr parameter);

    internal static bool HasRecordingFrameBorder(IntPtr window)
    {
        if (!GetClientRect(window, out var bounds))
        {
            return false;
        }

        var region = CreateRectRgn(0, 0, 0, 0);
        if (region == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            // SelectRectangle expands its 2-DIP hollow border to 4 DIP only after the first
            // captured frame. Its WDA_EXCLUDEFROMCAPTURE flag prevents a screenshot color oracle.
            var offset = (int)((3 * GetDpiForWindow(window)) / 96);
            return GetWindowRgn(window, region) != 0 &&
                PtInRegion(region, bounds.Right / 2, offset) &&
                PtInRegion(region, offset, bounds.Bottom / 2) &&
                !PtInRegion(region, bounds.Right / 2, bounds.Bottom / 2);
        }
        finally
        {
            DeleteObject(region);
        }
    }

    internal static string ReadDialogText(IntPtr dialog)
    {
        var captions = new List<string>();
        EnumChildWindows(
            dialog,
            (child, _) =>
            {
                var text = new StringBuilder(4096);
                if (GetWindowTextW(child, text, text.Capacity) > 0)
                {
                    captions.Add(text.ToString());
                }

                return true;
            },
            IntPtr.Zero);
        return string.Join(" | ", captions);
    }

    internal static async Task<Bitmap> DecodeVideoFrameAsync(MediaComposition composition, TimeSpan position)
    {
        // Zero leaves both thumbnail dimensions unspecified, preserving the source resolution.
        // Passing the expected size here would rescale the frame and hide dimension regressions.
        using var stream = await composition.GetThumbnailAsync(
            position,
            0,
            0,
            VideoFramePrecision.NearestFrame).AsTask().WaitAsync(TimeSpan.FromSeconds(45));
        var decoder = await BitmapDecoder.CreateAsync(stream).AsTask().WaitAsync(TimeSpan.FromSeconds(30));
        var provider = await decoder.GetPixelDataAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Ignore,
            new BitmapTransform(),
            ExifOrientationMode.IgnoreExifOrientation,
            ColorManagementMode.DoNotColorManage).AsTask().WaitAsync(TimeSpan.FromSeconds(30));
        var pixels = provider.DetachPixelData();
        var bitmap = new Bitmap((int)decoder.PixelWidth, (int)decoder.PixelHeight, PixelFormat.Format32bppArgb);
        var data = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            for (var y = 0; y < bitmap.Height; y++)
            {
                Marshal.Copy(pixels, y * bitmap.Width * 4, data.Scan0 + (y * data.Stride), bitmap.Width * 4);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return bitmap;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(IntPtr window, out NativeRect rectangle);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern int GetWindowRgn(IntPtr window, IntPtr region);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PtInRegion(IntPtr region, int x, int y);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr handle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumChildWindows(IntPtr parent, EnumWindowCallback callback, IntPtr parameter);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextW(IntPtr window, StringBuilder text, int maximumCount);
}
