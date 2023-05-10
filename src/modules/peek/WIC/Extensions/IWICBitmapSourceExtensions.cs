using System.ComponentModel;

namespace WIC
{
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static class IWICBitmapSourceExtensions
    {
        public static void CopyPixels(this IWICBitmapSource bitmapSource, int cbStride, byte[] pbBuffer, WICRect? prc = null)
        {
            using (var prcPtr = CoTaskMemPtr.From(prc))
            {
                bitmapSource.CopyPixels(prcPtr, cbStride, pbBuffer.Length, pbBuffer);
            }
        }

        public static Size GetSize(this IWICBitmapSource bitmapSource)
        {
            int width, height;
            bitmapSource.GetSize(out width, out height);
            return new Size(width, height);
        }

        public static Resolution GetResolution(this IWICBitmapSource bitmapSource)
        {
            double dpiX, dpiY;
            bitmapSource.GetResolution(out dpiX, out dpiY);
            return new Resolution(dpiX, dpiY);
        }
    }
}
