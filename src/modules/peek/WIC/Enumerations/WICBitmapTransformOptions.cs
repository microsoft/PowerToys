using System;

namespace WIC
{
    [Flags]
    public enum WICBitmapTransformOptions : int
    {
        WICBitmapTransformRotate0 = 0x00000000,
        WICBitmapTransformRotate90 = 0x00000001,
        WICBitmapTransformRotate180 = 0x00000002,
        WICBitmapTransformRotate270 = 0x00000003,

        WICBitmapTransformFlipHorizontal = 0x00000008,

        WICBitmapTransformFlipVertical = 0x00000010,
    }
}
