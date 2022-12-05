namespace WIC
{
    public enum WICBitmapInterpolationMode : int
    {
        WICBitmapInterpolationModeNearestNeighbor = 0x00000000,
        WICBitmapInterpolationModeLinear = 0x00000001,
        WICBitmapInterpolationModeCubic = 0x00000002,
        WICBitmapInterpolationModeFant = 0x00000003,
        /// <remarks>
        /// Supported beginning with Windows 10.
        /// </remarks>
        WICBitmapInterpolationModeHighQualityCubic = 0x00000004,
    }
}
