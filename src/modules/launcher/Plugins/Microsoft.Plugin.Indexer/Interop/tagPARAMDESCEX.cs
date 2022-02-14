
using System.Runtime.InteropServices;

namespace Microsoft.Plugin.Indexer.Interop
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct tagPARAMDESCEX
    {
        public uint cBytes;
        [MarshalAs(UnmanagedType.Struct)]
        public object varDefaultValue;
    }
}
