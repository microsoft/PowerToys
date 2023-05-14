using System;

namespace WIC
{
    [Flags]
    public enum StreamAccessMode : int
    {
        GENERIC_WRITE = 0x40000000,
        GENERIC_READ = unchecked((int)0x80000000U),
    }
}
