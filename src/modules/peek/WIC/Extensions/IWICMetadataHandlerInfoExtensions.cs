using System;
using System.ComponentModel;

namespace WIC
{
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static class IWICMetadataHandlerInfoExtensions
    {
        public static Guid[] GetContainerFormats(this IWICMetadataHandlerInfo metadataHandlerInfo)
        {
            FetchIntoBuffer<Guid> fetcher = metadataHandlerInfo.GetContainerFormats;
            return fetcher.FetchArray();
        }

        public static string GetDeviceManufacturer(this IWICMetadataHandlerInfo metadataHandlerInfo)
        {
            FetchIntoBuffer<char> fetcher = metadataHandlerInfo.GetDeviceManufacturer;
            return fetcher.FetchString();
        }

        public static string GetDeviceModels(this IWICMetadataHandlerInfo metadataHandlerInfo)
        {
            FetchIntoBuffer<char> fetcher = metadataHandlerInfo.GetDeviceModels;
            return fetcher.FetchString();
        }
    }
}
