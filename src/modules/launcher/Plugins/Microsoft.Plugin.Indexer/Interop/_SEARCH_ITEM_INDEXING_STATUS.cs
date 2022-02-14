
using System.Runtime.InteropServices;

namespace Microsoft.Search.Interop
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct _SEARCH_ITEM_INDEXING_STATUS
    {
        public uint dwDocID;
        [MarshalAs(UnmanagedType.Error)]
        public int hrIndexingStatus;
    }
}
