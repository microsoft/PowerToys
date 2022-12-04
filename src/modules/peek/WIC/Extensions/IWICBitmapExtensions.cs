using System.ComponentModel;

namespace WIC
{
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static class IWICBitmapExtensions
    {
        public static IWICBitmapLock Lock(this IWICBitmap bitmap, WICBitmapLockFlags flags, WICRect? prcLock = null)
        {
            using (var prcLockPtr = CoTaskMemPtr.From(prcLock))
            {
                return bitmap.Lock(prcLockPtr, flags);
            }
        }

        public static void SetResolution(this IWICBitmap bitmap, Resolution resolution)
        {
            bitmap.SetResolution(resolution.DpiX, resolution.DpiY);
        }
    }
}
