
using System;
using System.Runtime.InteropServices;

namespace Microsoft.Plugin.Indexer.Interop
{
    [ComConversionLoss]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct _FLAGGED_WORD_BLOB
    {
        public uint fFlags;
        public uint clSize;
        [ComConversionLoss]
        public IntPtr asData;
    }
}
