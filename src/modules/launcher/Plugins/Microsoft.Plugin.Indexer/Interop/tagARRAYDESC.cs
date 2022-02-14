
using System;
using System.Runtime.InteropServices;

namespace Microsoft.Plugin.Indexer.Interop
{
    [ComConversionLoss]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct tagARRAYDESC
    {
        public tagTYPEDESC tdescElem;
        public ushort cDims;
        [ComConversionLoss]
        public IntPtr rgbounds;
    }
}
