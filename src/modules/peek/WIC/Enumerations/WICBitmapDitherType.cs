namespace WIC
{
    public enum WICBitmapDitherType : int
    {
        WICBitmapDitherTypeNone = 0x00000000,
        WICBitmapDitherTypeSolid = 0x00000000,

        WICBitmapDitherTypeOrdered4x4 = 0x00000001,

        WICBitmapDitherTypeOrdered8x8 = 0x00000002,
        WICBitmapDitherTypeOrdered16x16 = 0x00000003,
        WICBitmapDitherTypeSpiral4x4 = 0x00000004,
        WICBitmapDitherTypeSpiral8x8 = 0x00000005,
        WICBitmapDitherTypeDualSpiral4x4 = 0x00000006,
        WICBitmapDitherTypeDualSpiral8x8 = 0x00000007,

        WICBitmapDitherTypeErrorDiffusion = 0x00000008,
    }
}
