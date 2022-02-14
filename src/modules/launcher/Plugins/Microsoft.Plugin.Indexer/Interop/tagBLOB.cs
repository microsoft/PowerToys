
using System;
using System.Runtime.InteropServices;

namespace Microsoft.Search.Interop
{
    [ComConversionLoss]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct tagBLOB
    {
        public uint cbSize;
        [ComConversionLoss]
        public IntPtr pBlobData;
    }
}
