
using System.Runtime.InteropServices;

namespace Microsoft.Plugin.Indexer.Interop
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct tagIDLDESC
    {
        [ComAliasName("Microsoft.Search.Interop.ULONG_PTR")]
        public uint dwReserved;
        public ushort wIDLFlags;
    }
}
