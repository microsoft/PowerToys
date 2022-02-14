
using System;
using System.Runtime.InteropServices;

namespace Microsoft.Search.Interop
{
    [ComConversionLoss]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct tagPARAMDESC
    {
        [ComConversionLoss]
        public IntPtr pparamdescex;
        public ushort wParamFlags;
    }
}
