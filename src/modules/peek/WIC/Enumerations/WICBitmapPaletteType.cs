namespace WIC
{
    public enum WICBitmapPaletteType : int
    {
        WICBitmapPaletteTypeCustom = 0x00000000,
        WICBitmapPaletteTypeMedianCut = 0x00000001,
        WICBitmapPaletteTypeFixedBW = 0x00000002,
        WICBitmapPaletteTypeFixedHalftone8 = 0x00000003,
        WICBitmapPaletteTypeFixedHalftone27 = 0x00000004,
        WICBitmapPaletteTypeFixedHalftone64 = 0x00000005,
        WICBitmapPaletteTypeFixedHalftone125 = 0x00000006,
        WICBitmapPaletteTypeFixedHalftone216 = 0x00000007,
        WICBitmapPaletteTypeFixedWebPalette = WICBitmapPaletteTypeFixedHalftone216,
        WICBitmapPaletteTypeFixedHalftone252 = 0x00000008,
        WICBitmapPaletteTypeFixedHalftone256 = 0x00000009,
        WICBitmapPaletteTypeFixedGray4 = 0x0000000A,
        WICBitmapPaletteTypeFixedGray16 = 0x0000000B,
        WICBitmapPaletteTypeFixedGray256 = 0x0000000C,
    }
}
