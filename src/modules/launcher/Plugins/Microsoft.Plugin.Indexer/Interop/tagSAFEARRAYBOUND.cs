
using System.Runtime.InteropServices;

namespace Microsoft.Plugin.Indexer.Interop
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct tagSAFEARRAYBOUND
    {
        public uint cElements;
        public int lLbound;
    }
}
