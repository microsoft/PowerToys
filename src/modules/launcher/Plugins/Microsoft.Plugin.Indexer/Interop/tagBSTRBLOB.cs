
using System;
using System.Runtime.InteropServices;

namespace Microsoft.Plugin.Indexer.Interop
{
    [ComConversionLoss]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct tagBSTRBLOB
    {
        public uint cbSize;
        [ComConversionLoss]
        public IntPtr pData;
    }
}
