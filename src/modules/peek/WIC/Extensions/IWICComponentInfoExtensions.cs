using System.ComponentModel;

namespace WIC
{
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static class IWICComponentInfoExtensions
    {
        public static string GetAuthor(this IWICComponentInfo componentInfo)
        {
            FetchIntoBuffer<char> fetcher = componentInfo.GetAuthor;
            return fetcher.FetchString();
        }

        public static string GetFriendlyName(this IWICComponentInfo componentInfo)
        {
            FetchIntoBuffer<char> fetcher = componentInfo.GetFriendlyName;
            return fetcher.FetchString();
        }

        public static string GetVersion(this IWICComponentInfo componentInfo)
        {
            FetchIntoBuffer<char> fetcher = componentInfo.GetVersion;
            return fetcher.FetchString();
        }

        public static string GetSpecVersion(this IWICComponentInfo componentInfo)
        {
            FetchIntoBuffer<char> fetcher = componentInfo.GetSpecVersion;
            return fetcher.FetchString();
        }
    }
}
