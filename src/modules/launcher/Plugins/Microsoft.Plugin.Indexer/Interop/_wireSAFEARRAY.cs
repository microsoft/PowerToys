
using System;
using System.Runtime.InteropServices;

namespace Microsoft.Plugin.Indexer.Interop
{
    [ComConversionLoss]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct _wireSAFEARRAY
    {
        public ushort cDims;
        public ushort fFeatures;
        public uint cbElements;
        public uint cLocks;
        public _wireSAFEARRAY_UNION uArrayStructs;
        [ComConversionLoss]
        public IntPtr rgsabound;
    }
}
