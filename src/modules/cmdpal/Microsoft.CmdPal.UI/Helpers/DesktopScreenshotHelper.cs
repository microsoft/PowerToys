// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using WinRT;

namespace Microsoft.CmdPal.UI.Helpers;

internal static partial class DesktopScreenshotHelper
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
        SoftwareBitmap? softwareBitmap = null;

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

            softwareBitmap = new SoftwareBitmap(
                BitmapPixelFormat.Bgra8,
                captureWidth,
                captureHeight,
                BitmapAlphaMode.Premultiplied);
            using (var buffer = softwareBitmap.LockBuffer(BitmapBufferAccessMode.Write))
            {
                using var reference = buffer.CreateReference();
                GetBuffer(reference, out var data, out var capacity);
                var plane = buffer.GetPlaneDescription(0);
                var rowBytes = checked(captureWidth * 4);
                var byteCount = checked(rowBytes * captureHeight);

                // GetDIBits writes packed BGRA rows; reject incompatible buffer layouts.
                if (data == null
                    || plane.Width != captureWidth
                    || plane.Height != captureHeight
                    || plane.Stride != rowBytes
                    || plane.StartIndex < 0
                    || (long)plane.StartIndex + byteCount > capacity)
                {
                    return null;
                }

                var pixelBuffer = data + plane.StartIndex;
                var copiedLines = PInvoke.GetDIBits(
                    screenDc,
                    bitmap,
                    0,
                    (uint)captureHeight,
                    pixelBuffer,
                    &bitmapInfo,
                    DIB_USAGE.DIB_RGB_COLORS);
                if (copiedLines != captureHeight)
                {
                    return null;
                }

                for (var i = 3; i < byteCount; i += 4)
                {
                    pixelBuffer[i] = byte.MaxValue;
                }
            }

            var result = softwareBitmap;
            softwareBitmap = null;
            return result;
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
            softwareBitmap?.Dispose();
        }
    }

    private static unsafe void GetBuffer(IMemoryBufferReference reference, out byte* data, out uint capacity)
    {
        using var nativeReference = MarshalInspectable<IMemoryBufferReference>.CreateMarshaler(reference);
        var byteAccess = UniqueComInterfaceMarshaller<IMemoryBufferByteAccess>.ConvertToManaged((void*)nativeReference.ThisPtr)!;
        try
        {
            byteAccess.GetBuffer(out data, out capacity);
        }
        finally
        {
            ((ComObject)(object)byteAccess).FinalRelease();
        }
    }

    [GeneratedComInterface]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal unsafe partial interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }
}
