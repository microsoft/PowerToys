using System;
using System.Runtime.InteropServices;

namespace WIC
{
    [StructLayout(LayoutKind.Sequential)]
    public struct PROPBAG2
    {
        public PROPBAG2_Type dwType;
        public VARTYPE vt;
        public short cfType;
        public int dwHint;
        public IntPtr pstrName;
        public Guid clsid;
    }

    public enum PROPBAG2_Type : int
    {
        PROPBAG2_TYPE_UNDEFINED = 0,
        PROPBAG2_TYPE_DATA = 1,
        PROPBAG2_TYPE_URL = 2,
        PROPBAG2_TYPE_OBJECT = 3,
        PROPBAG2_TYPE_STREAM = 4,
        PROPBAG2_TYPE_STORAGE = 5,
        PROPBAG2_TYPE_MONIKER = 6
    }
}
