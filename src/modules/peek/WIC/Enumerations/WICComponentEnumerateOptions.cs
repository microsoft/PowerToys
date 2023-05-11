using System;

namespace WIC
{
    [Flags]
    public enum WICComponentEnumerateOptions : int
    {
        WICComponentEnumerateDefault = 0x00000000,
        WICComponentEnumerateRefresh = 0x00000001,
        WICComponentEnumerateBuiltInOnly = 0x20000000,
        WICComponentEnumerateUnsigned = 0x40000000,
        WICComponentEnumerateDisabled = unchecked((int)0x80000000U),
    }
}
