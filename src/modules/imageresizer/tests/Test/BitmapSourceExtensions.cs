#pragma warning disable IDE0073
// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/
#pragma warning restore IDE0073

using System;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace ImageResizer.Test
{
    internal static class BitmapSourceExtensions
    {
        public static async Task<(byte R, byte G, byte B, byte A)> GetFirstPixelAsync(this BitmapDecoder decoder)
        {
            using var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied);

            var buffer = new Windows.Storage.Streams.Buffer((uint)(softwareBitmap.PixelWidth * softwareBitmap.PixelHeight * 4));
            softwareBitmap.CopyToBuffer(buffer);

            using var reader = DataReader.FromBuffer(buffer);
            byte b = reader.ReadByte();
            byte g = reader.ReadByte();
            byte r = reader.ReadByte();
            byte a = reader.ReadByte();

            return (r, g, b, a);
        }
    }
}
