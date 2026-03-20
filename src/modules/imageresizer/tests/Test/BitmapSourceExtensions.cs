#pragma warning disable IDE0073
// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/
#pragma warning restore IDE0073

using Windows.Graphics.Imaging;

namespace ImageResizer.Test
{
    internal static class BitmapDecoderExtensions
    {
        public static (byte R, byte G, byte B, byte A) GetFirstPixel(this BitmapDecoder decoder)
        {
            var frame = decoder.GetFrameAsync(0).AsTask().GetAwaiter().GetResult();
            var bitmap = frame.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied)
                .AsTask().GetAwaiter().GetResult();

            using var buffer = bitmap.LockBuffer(BitmapBufferAccessMode.Read);
            using var reference = buffer.CreateReference();
            unsafe
            {
                ((Windows.Foundation.IMemoryBufferByteAccess)reference).GetBuffer(out byte* dataInBytes, out uint capacity);
                byte b = dataInBytes[0];
                byte g = dataInBytes[1];
                byte r = dataInBytes[2];
                byte a = dataInBytes[3];
                return (r, g, b, a);
            }
        }
    }

    // COM interface for buffer access
    [System.Runtime.InteropServices.ComImport]
    [System.Runtime.InteropServices.Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [System.Runtime.InteropServices.InterfaceType(System.Runtime.InteropServices.ComInterfaceType.InterfaceIsIUnknown)]
    unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }
}

namespace Windows.Foundation
{
    // Extension interface for IMemoryBufferReference
    [System.Runtime.InteropServices.ComImport]
    [System.Runtime.InteropServices.Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [System.Runtime.InteropServices.InterfaceType(System.Runtime.InteropServices.ComInterfaceType.InterfaceIsIUnknown)]
    unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }
}
