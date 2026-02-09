// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using ManagedCommon;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.Shell;

namespace Microsoft.CmdPal.Ext.Apps.Helpers;

internal static class IconExtractor
{
    public static async Task<IRandomAccessStream?> GetIconStreamAsync(string path, int size)
    {
        var bitmap = GetIcon(path, size);
        if (bitmap == null)
        {
            return null;
        }

        var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetSoftwareBitmap(bitmap);
        await encoder.FlushAsync();

        stream.Seek(0);
        return stream;
    }

    public static unsafe SoftwareBitmap? GetIcon(string path, int size)
    {
        IShellItemImageFactory* factory = null;
        HBITMAP hBitmap = default;

        try
        {
            fixed (char* pPath = path)
            {
                var iid = IShellItemImageFactory.IID_Guid;
                var hr = PInvoke.SHCreateItemFromParsingName(
                    pPath,
                    null,
                    &iid,
                    (void**)&factory);

                if (hr.Failed || factory == null)
                {
                    return null;
                }
            }

            var requestedSize = new SIZE { cx = size, cy = size };
            var hr2 = factory->GetImage(
                requestedSize,
                SIIGBF.SIIGBF_ICONONLY | SIIGBF.SIIGBF_BIGGERSIZEOK | SIIGBF.SIIGBF_CROPTOSQUARE,
                &hBitmap);

            if (hr2.Failed || hBitmap.IsNull)
            {
                return null;
            }

            return CreateSoftwareBitmap(hBitmap, size);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to load icon from path='{path}',size={size}", ex);
            return null;
        }
        finally
        {
            if (!hBitmap.IsNull)
            {
                PInvoke.DeleteObject(hBitmap);
            }

            if (factory != null)
            {
                factory->Release();
            }
        }
    }

    private static unsafe SoftwareBitmap CreateSoftwareBitmap(HBITMAP hBitmap, int size)
    {
        var pixels = new byte[size * size * 4];

        var bmi = new BITMAPINFO
        {
            bmiHeader = new BITMAPINFOHEADER
            {
                biSize = (uint)sizeof(BITMAPINFOHEADER),
                biWidth = size,
                biHeight = -size,
                biPlanes = 1,
                biBitCount = 32,
                biCompression = 0,
            },
        };

        var hdc = PInvoke.GetDC(default);
        try
        {
            fixed (byte* pPixels = pixels)
            {
                _ = PInvoke.GetDIBits(
                    hdc,
                    hBitmap,
                    0,
                    (uint)size,
                    pPixels,
                    &bmi,
                    DIB_USAGE.DIB_RGB_COLORS);
            }
        }
        finally
        {
            _ = PInvoke.ReleaseDC(default, hdc);
        }

        var bitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, size, size, BitmapAlphaMode.Premultiplied);
        bitmap.CopyFromBuffer(pixels.AsBuffer());
        return bitmap;
    }
}
