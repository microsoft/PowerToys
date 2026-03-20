#pragma warning disable IDE0073
// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/
#pragma warning restore IDE0073

using System;
using System.Collections.Generic;
using Windows.Graphics.Imaging;

namespace ImageResizer.Extensions
{
    internal static class BitmapEncoderExtensions
    {
        private static readonly HashSet<Guid> SupportedEncoderIds = new HashSet<Guid>
        {
            BitmapEncoder.BmpEncoderId,
            BitmapEncoder.GifEncoderId,
            BitmapEncoder.JpegEncoderId,
            BitmapEncoder.PngEncoderId,
            BitmapEncoder.TiffEncoderId,
            BitmapEncoder.JpegXREncoderId,
        };

        public static bool CanEncode(Guid encoderId)
        {
            return SupportedEncoderIds.Contains(encoderId);
        }
    }
}
