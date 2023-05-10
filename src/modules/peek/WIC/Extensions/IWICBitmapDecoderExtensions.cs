using System.Collections.Generic;
using System.ComponentModel;

namespace WIC
{
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static class IWICBitmapDecoderExtensions
    {
        public static IWICMetadataBlockReader AsMetadataBlockReader(this IWICBitmapDecoder bitmapDecoder)
        {
            return bitmapDecoder as IWICMetadataBlockReader;
        }

        public static IWICColorContext[] GetColorContexts(this IWICBitmapDecoder bitmapDecoder)
        {
            int length;
            bitmapDecoder.GetColorContexts(0, null, out length);
            var colorContexts = new IWICColorContext[length];
            if (length > 0)
            {
                bitmapDecoder.GetColorContexts(length, colorContexts, out length);
            }
            return colorContexts;
        }

        public static IEnumerable<IWICBitmapFrameDecode> GetFrames(this IWICBitmapDecoder bitmapDecoder)
        {
            for (int i = 0, n = bitmapDecoder.GetFrameCount(); i < n; ++i)
            {
                yield return bitmapDecoder.GetFrame(i);
            }
        }
    }
}
