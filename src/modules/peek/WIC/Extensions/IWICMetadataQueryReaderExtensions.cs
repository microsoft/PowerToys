using System.ComponentModel;
using System.Runtime.InteropServices;
using static WIC.PropVariantHelpers;

namespace WIC
{
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static class IWICMetadataQueryReaderExtensions
    {
        public static string GetLocation(this IWICMetadataQueryReader metadataQueryReader)
        {
            FetchIntoBuffer<char> fetcher = metadataQueryReader.GetLocation;
            return fetcher.FetchString();
        }

        public static bool TryGetMetadataByName<T>(this IWICMetadataQueryReader metadataQueryReader, string name, out T value)
        {
            var variant = new PROPVARIANT();
            try
            {
                metadataQueryReader.GetMetadataByName(name, ref variant);
                return TryDecode(ref variant, out value);
            }
            catch (COMException ex) when (ex.ErrorCode == HResult.WINCODEC_ERR_PROPERTYNOTFOUND)
            {
                value = default(T);
                return false;
            }
            finally
            {
                Dispose(ref variant);
            }
        }
    }
}
