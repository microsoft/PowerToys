
using System.Runtime.InteropServices;

namespace Microsoft.Plugin.Indexer.Interop
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct tagELEMDESC
    {
        public tagTYPEDESC tdesc;
        public tagPARAMDESC paramdesc;
    }
}
