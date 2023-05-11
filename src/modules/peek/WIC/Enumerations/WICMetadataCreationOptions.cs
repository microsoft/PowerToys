using System;

namespace WIC
{
    [Flags]
    public enum WICMetadataCreationOptions : int
    {
        WICMetadataCreationDefault      = 0x00000000,
        WICMetadataCreationAllowUnknown = WICMetadataCreationDefault,
        WICMetadataCreationFailUnknown  = 0x00010000,
        WICMetadataCreationMask         = unchecked((int)0xFFFF0000),
    }
}
