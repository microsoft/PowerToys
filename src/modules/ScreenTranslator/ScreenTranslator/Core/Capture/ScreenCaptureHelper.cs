// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using ScreenTranslator.Core.Translation;
using ScreenTranslator.Helpers;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace ScreenTranslator.Core.Capture;

public static class ScreenCaptureHelper
{
    public static SoftwareBitmap? CaptureRegion(PhysicalRect physicalRect)
    {
        int x = (int)Math.Floor(physicalRect.X);
        int y = (int)Math.Floor(physicalRect.Y);
        int width = (int)Math.Ceiling(physicalRect.Width);
        int height = (int)Math.Ceiling(physicalRect.Height);

        if (width <= 0 || height <= 0)
        {
            return null;
        }

        IntPtr hdcScreen = OSInterop.GetDC(IntPtr.Zero);
        if (hdcScreen == IntPtr.Zero)
        {
            return null;
        }

        IntPtr hdcMem = OSInterop.CreateCompatibleDC(hdcScreen);
        if (hdcMem == IntPtr.Zero)
        {
            _ = OSInterop.ReleaseDC(IntPtr.Zero, hdcScreen);
            return null;
        }

        OSInterop.BITMAPINFO bmi = default;
        bmi.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(OSInterop.BITMAPINFOHEADER));
        bmi.bmiHeader.biWidth = width;
        bmi.bmiHeader.biHeight = -height; // top-down
        bmi.bmiHeader.biPlanes = 1;
        bmi.bmiHeader.biBitCount = 32;
        bmi.bmiHeader.biCompression = 0; // BI_RGB

        IntPtr hDIB = OSInterop.CreateDIBSection(hdcMem, ref bmi, 0, out IntPtr pBits, IntPtr.Zero, 0);
        if (hDIB == IntPtr.Zero || pBits == IntPtr.Zero)
        {
            _ = OSInterop.DeleteDC(hdcMem);
            _ = OSInterop.ReleaseDC(IntPtr.Zero, hdcScreen);
            return null;
        }

        IntPtr hOld = OSInterop.SelectObject(hdcMem, hDIB);

        // BitBlt with SRCCOPY | CAPTUREBLT
        _ = OSInterop.BitBlt(hdcMem, 0, 0, width, height, hdcScreen, x, y, 0x00CC0020 | 0x40000000);

        int byteCount = width * height * 4;
        byte[] pixelBytes = new byte[byteCount];
        Marshal.Copy(pBits, pixelBytes, 0, byteCount);

        _ = OSInterop.SelectObject(hdcMem, hOld);
        _ = OSInterop.DeleteObject(hDIB);
        _ = OSInterop.DeleteDC(hdcMem);
        _ = OSInterop.ReleaseDC(IntPtr.Zero, hdcScreen);

        SoftwareBitmap softwareBitmap = new(BitmapPixelFormat.Bgra8, width, height, BitmapAlphaMode.Premultiplied);
        using (DataWriter writer = new())
        {
            writer.WriteBytes(pixelBytes);
            IBuffer buffer = writer.DetachBuffer();
            softwareBitmap.CopyFromBuffer(buffer);
        }

        return softwareBitmap;
    }
}
