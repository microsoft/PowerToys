
using System.Runtime.InteropServices;

namespace Microsoft.Search.Interop
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct tagSAFEARRAYBOUND
    {
        public uint cElements;
        public int lLbound;
    }
}
