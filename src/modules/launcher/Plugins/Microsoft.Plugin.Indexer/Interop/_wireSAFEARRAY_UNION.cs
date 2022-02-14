
using System.Runtime.InteropServices;

namespace Microsoft.Plugin.Indexer.Interop
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct _wireSAFEARRAY_UNION
    {
        public uint sfType;
        public __MIDL_IOleAutomationTypes_0001 u;
    }
}
