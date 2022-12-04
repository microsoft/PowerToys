using System.ComponentModel;

namespace WIC
{
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static class IWICBitmapEncoderExtensions
    {
        public static IWICMetadataBlockWriter AsMetadataBlockWriter(this IWICBitmapEncoder bitmapEncoder)
        {
            return bitmapEncoder as IWICMetadataBlockWriter;
        }

        public static IWICBitmapFrameEncode CreateNewFrame(this IWICBitmapEncoder bitmapEncoder, IPropertyBag2 ppIEncoderOptions = null)
        {
            IWICBitmapFrameEncode ppIFrameEncode;
            bitmapEncoder.CreateNewFrame(out ppIFrameEncode, ppIEncoderOptions);
            return ppIFrameEncode;
        }
    }
}
