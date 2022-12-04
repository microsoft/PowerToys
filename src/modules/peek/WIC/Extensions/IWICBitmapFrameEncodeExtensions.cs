using System.ComponentModel;

namespace WIC
{
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static class IWICBitmapFrameEncodeExtensions
    {
        public static IWICMetadataBlockWriter AsMetadataBlockWriter(this IWICBitmapFrameEncode bitmapFrameEncode)
        {
            return bitmapFrameEncode as IWICMetadataBlockWriter;
        }

        public static void Initialize(this IWICBitmapFrameEncode bitmapFrameEncode, IPropertyBag2 pIEncoderOptions = null)
        {
            bitmapFrameEncode.Initialize(pIEncoderOptions);
        }

        public static void SetColorContexts(this IWICBitmapFrameEncode bitmapFrameEncode, IWICColorContext[] colorContexts)
        {
            bitmapFrameEncode.SetColorContexts(colorContexts.Length, colorContexts);
        }

        public static void SetResolution(this IWICBitmapFrameEncode bitmapFrameEncode, Resolution resolution)
        {
            bitmapFrameEncode.SetResolution(resolution.DpiX, resolution.DpiY);
        }

        public static void SetSize(this IWICBitmapFrameEncode bitmapFrameEncode, Size size)
        {
            bitmapFrameEncode.SetSize(size.Width, size.Height);
        }

        public static void WriteSource(this IWICBitmapFrameEncode bitmapFrameEncode, IWICBitmapSource pIBitmapSource, WICRect? prc = null)
        {
            using (var prcPtr = CoTaskMemPtr.From(prc))
            {
                bitmapFrameEncode.WriteSource(pIBitmapSource, prcPtr);
            }
        }

        public static void WritePixels(this IWICBitmapFrameEncode bitmapFrameEncode, int lineCount, int cbStride, byte[] pbPixels)
        {
            bitmapFrameEncode.WritePixels(lineCount, cbStride, pbPixels.Length, pbPixels);
        }
    }
}
