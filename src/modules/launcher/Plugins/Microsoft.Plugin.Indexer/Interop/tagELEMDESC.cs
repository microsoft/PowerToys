
using System.Runtime.InteropServices;

namespace Microsoft.Search.Interop
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct tagELEMDESC
    {
        public tagTYPEDESC tdesc;
        public tagPARAMDESC paramdesc;
    }
}
