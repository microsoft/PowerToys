
using System;
using System.Runtime.InteropServices;

namespace Microsoft.Search.Interop
{
    [ComConversionLoss]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct tagCAUH
    {
        public uint cElems;
        [ComConversionLoss]
        public IntPtr pElems;
    }
}
