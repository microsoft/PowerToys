
using System;
using System.Runtime.InteropServices;

namespace Microsoft.Search.Interop
{
    [ComConversionLoss]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct tagCLIPDATA
    {
        public uint cbSize;
        public int ulClipFmt;
        [ComConversionLoss]
        public IntPtr pClipData;
    }
}
