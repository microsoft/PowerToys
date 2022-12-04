using System;

namespace WIC
{
    [Flags]
    public enum WICBitmapDecoderCapabilities : int
    {
        WICBitmapDecoderCapabilitySameEncoder = 0x00000001,
        WICBitmapDecoderCapabilityCanDecodeAllImages = 0x00000002,
        WICBitmapDecoderCapabilityCanDecodeSomeImages = 0x00000004,
        WICBitmapDecoderCapabilityCanEnumerateMetadata = 0x00000008,
        WICBitmapDecoderCapabilityCanDecodeThumbnail = 0x00000010,
    }
}
