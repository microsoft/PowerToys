// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

namespace Microsoft.CmdPal.UI.Helpers;

internal static class DesktopScreenshotHelper
{
    private const int MaxCaptureWidth = 3840;
    private const int MaxCaptureHeight = 2160;
    private const ROP_CODE CaptureLayeredWindows = (ROP_CODE)0x40000000;

    public static Task<SoftwareBitmap?> CaptureAsync(RectInt32 bounds)
    {
        return Task.Run(() => Capture(bounds));
    }

    private static unsafe SoftwareBitmap? Capture(RectInt32 bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return null;
        }

        var captureScale = Math.Min(
            1,
            Math.Min(
                MaxCaptureWidth / (double)bounds.Width,
                MaxCaptureHeight / (double)bounds.Height));
        var captureWidth = Math.Max(1, (int)Math.Round(bounds.Width * captureScale));
        var captureHeight = Math.Max(1, (int)Math.Round(bounds.Height * captureScale));

        var screenDc = PInvoke.GetDC(HWND.Null);
        if (screenDc.IsNull)
        {
            return null;
        }

        HDC memoryDc = default;
        HBITMAP bitmap = default;
        HGDIOBJ previousObject = default;

        try
        {
            memoryDc = PInvoke.CreateCompatibleDC(screenDc);
            if (memoryDc.IsNull)
            {
                return null;
            }

            bitmap = PInvoke.CreateCompatibleBitmap(screenDc, captureWidth, captureHeight);
            if (bitmap.IsNull)
            {
                return null;
            }

            previousObject = PInvoke.SelectObject(memoryDc, bitmap);
            if (previousObject.IsNull)
            {
                return null;
            }

            if (captureScale < 1)
            {
                _ = PInvoke.SetStretchBltMode(memoryDc, STRETCH_BLT_MODE.HALFTONE);
                _ = PInvoke.SetBrushOrgEx(memoryDc, 0, 0, null);
            }

            var copied = PInvoke.StretchBlt(
                memoryDc,
                0,
                0,
                captureWidth,
                captureHeight,
                screenDc,
                bounds.X,
                bounds.Y,
                bounds.Width,
                bounds.Height,
                ROP_CODE.SRCCOPY | CaptureLayeredWindows);

            _ = PInvoke.SelectObject(memoryDc, previousObject);
            previousObject = default;

            if (!copied)
            {
                return null;
            }

            var pixels = new byte[checked(captureWidth * captureHeight * 4)];
            var bitmapInfo = new BITMAPINFO
            {
                bmiHeader = new BITMAPINFOHEADER
                {
                    biSize = (uint)sizeof(BITMAPINFOHEADER),
                    biWidth = captureWidth,
                    biHeight = -captureHeight,
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = 0,
                },
            };

            fixed (byte* pixelBuffer = pixels)
            {
                var copiedLines = PInvoke.GetDIBits(
                    screenDc,
                    bitmap,
                    0,
                    (uint)captureHeight,
                    pixelBuffer,
                    &bitmapInfo,
                    DIB_USAGE.DIB_RGB_COLORS);
                if (copiedLines == 0)
                {
                    return null;
                }
            }

            for (var i = 3; i < pixels.Length; i += 4)
            {
                pixels[i] = byte.MaxValue;
            }

            var softwareBitmap = new SoftwareBitmap(
                BitmapPixelFormat.Bgra8,
                captureWidth,
                captureHeight,
                BitmapAlphaMode.Premultiplied);
            softwareBitmap.CopyFromBuffer(pixels.AsBuffer());
            return softwareBitmap;
        }
        finally
        {
            if (!previousObject.IsNull && !memoryDc.IsNull)
            {
                _ = PInvoke.SelectObject(memoryDc, previousObject);
            }

            if (!bitmap.IsNull)
            {
                _ = PInvoke.DeleteObject(bitmap);
            }

            if (!memoryDc.IsNull)
            {
                _ = PInvoke.DeleteDC(memoryDc);
            }

            _ = PInvoke.ReleaseDC(HWND.Null, screenDc);
        }
    }
}
