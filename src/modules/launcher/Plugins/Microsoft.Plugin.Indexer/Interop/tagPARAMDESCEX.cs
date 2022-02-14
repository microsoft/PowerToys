
using System.Runtime.InteropServices;

namespace Microsoft.Search.Interop
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct tagPARAMDESCEX
    {
        public uint cBytes;
        [MarshalAs(UnmanagedType.Struct)]
        public object varDefaultValue;
    }
}
