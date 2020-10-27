// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

namespace System.Windows.Media.Imaging
{
    internal static class BitmapEncoderExtensions
    {
        public static bool CanEncode(this BitmapEncoder encoder)
        {
            try
            {
                var temp = encoder.CodecInfo;
            }
            catch (NotSupportedException)
            {
                return false;
            }

            return true;
        }
    }
}
