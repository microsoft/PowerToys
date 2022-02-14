
using System.Runtime.InteropServices;

namespace Microsoft.Plugin.Indexer.Interop
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct tag_inner_PROPVARIANT
    {
        public ushort vt;
        public byte wReserved1;
        public byte wReserved2;
        public uint wReserved3;
        public __MIDL___MIDL_itf_searchapi_0001_0129_0001 __MIDL____MIDL_itf_searchapi_0001_01290001;
    }
}
