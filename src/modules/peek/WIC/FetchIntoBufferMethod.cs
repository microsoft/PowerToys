namespace WIC
{
    internal delegate void FetchIntoBuffer<T>(int size, T[] buffer, out int length);

    internal static class FetchIntoBufferExtensions
    {
        internal static T[] FetchArray<T>(this FetchIntoBuffer<T> fetcher)
        {
            int length;
            fetcher.Invoke(0, null, out length);
            var buffer = new T[length];
            if (length > 0)
            {
                fetcher.Invoke(length, buffer, out length);
            }
            return buffer;
        }

        internal static string FetchString(this FetchIntoBuffer<char> fetcher)
        {
            var buffer = fetcher.FetchArray();
            int length = buffer.Length - 1;
            if (length > 0)
            {
                return new string(buffer, 0, length);
            }
            else if (length == 0)
            {
                return string.Empty;
            }
            else
            {
                return null;
            }
        }
    }
}
