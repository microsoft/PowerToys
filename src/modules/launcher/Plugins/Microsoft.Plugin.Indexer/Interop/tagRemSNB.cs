
using System;
using System.Runtime.InteropServices;

namespace Microsoft.Search.Interop
{
    [ComConversionLoss]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct tagRemSNB
    {
        public uint ulCntStr;
        public uint ulCntChar;
        [ComConversionLoss]
        public IntPtr rgString;
    }
}
