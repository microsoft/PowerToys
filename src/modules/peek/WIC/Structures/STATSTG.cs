using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WIC
{
    [StructLayout(LayoutKind.Sequential)]
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public struct STATSTG
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string pwcsName;
        public STGTY type;
        public long cbSize;
        public long mtime;
        public long ctime;
        public long atime;
        public STGM grfMode;
        public LOCKTYPE grfLocksSupported;
        public Guid clsid;
        public int grfStateBits;
    }
}
