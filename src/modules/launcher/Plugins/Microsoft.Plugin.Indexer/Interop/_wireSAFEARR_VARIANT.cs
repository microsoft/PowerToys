
using System;
using System.Runtime.InteropServices;

namespace Microsoft.Search.Interop
{
    [ComConversionLoss]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct _wireSAFEARR_VARIANT
    {
        public uint Size;
        [ComConversionLoss]
        public IntPtr aVariant;
    }
}
