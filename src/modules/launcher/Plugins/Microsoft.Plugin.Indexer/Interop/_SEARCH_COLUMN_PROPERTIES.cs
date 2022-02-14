
using System.Runtime.InteropServices;

namespace Microsoft.Plugin.Indexer.Interop
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct _SEARCH_COLUMN_PROPERTIES
    {
        public tag_inner_PROPVARIANT Value;
        public uint lcid;
    }
}
