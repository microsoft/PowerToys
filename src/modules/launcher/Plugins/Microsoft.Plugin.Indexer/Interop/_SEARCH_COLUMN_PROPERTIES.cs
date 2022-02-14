
using System.Runtime.InteropServices;

namespace Microsoft.Search.Interop
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct _SEARCH_COLUMN_PROPERTIES
    {
        public tag_inner_PROPVARIANT Value;
        public uint lcid;
    }
}
