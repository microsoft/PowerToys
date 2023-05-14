using System.ComponentModel;

namespace WIC
{
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static class IWICBitmapCodecInfoExtensions
    {
        public static string GetColorManagementVersion(this IWICBitmapCodecInfo bitmapCodecInfo)
        {
            FetchIntoBuffer<char> fetcher = bitmapCodecInfo.GetColorManagementVersion;
            return fetcher.FetchString();
        }

        public static string GetDeviceManufacturer(this IWICBitmapCodecInfo bitmapCodecInfo)
        {
            FetchIntoBuffer<char> fetcher = bitmapCodecInfo.GetDeviceManufacturer;
            return fetcher.FetchString();
        }

        public static string GetDeviceModels(this IWICBitmapCodecInfo bitmapCodecInfo)
        {
            FetchIntoBuffer<char> fetcher = bitmapCodecInfo.GetDeviceModels;
            return fetcher.FetchString();
        }

        public static string[] GetMimeTypes(this IWICBitmapCodecInfo bitmapCodecInfo)
        {
            FetchIntoBuffer<char> fetcher = bitmapCodecInfo.GetMimeTypes;
            return fetcher.FetchString().Split(',');
        }

        public static string[] GetFileExtensions(this IWICBitmapCodecInfo bitmapCodecInfo)
        {
            FetchIntoBuffer<char> fetcher = bitmapCodecInfo.GetFileExtensions;
            return fetcher.FetchString().Split(',');
        }
    }
}
