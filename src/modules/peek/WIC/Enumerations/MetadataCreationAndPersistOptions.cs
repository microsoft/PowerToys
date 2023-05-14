using System;

namespace WIC
{
    [Flags]
    public enum MetadataCreationAndPersistOptions : int
    {
        WICMetadataCreationDefault = 0x00000000,
        WICMetadataCreationAllowUnknown = WICMetadataCreationDefault,
        WICMetadataCreationFailUnknown = 0x00010000,

        WICPersistOptionDefault = 0x00000000,
        WICPersistOptionLittleEndian = 0x00000000,
        WICPersistOptionBigEndian = 0x00000001,
        WICPersistOptionStrictFormat = 0x00000002,
        WICPersistOptionNoCacheStream = 0x00000004,
        WICPersistOptionPreferUTF8 = 0x00000008,
    }
}
