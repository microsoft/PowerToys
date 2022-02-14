
using System;
using System.Runtime.InteropServices;

namespace Microsoft.Search.Interop
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct _tagpropertykey
    {
        public Guid fmtid;
        public uint pid;
    }
}
