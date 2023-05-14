using System;
using System.Runtime.InteropServices;

namespace WIC
{
    [ComImport]
    [Guid(IID.IWICBitmap)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IWICBitmap : IWICBitmapSource
    {
        #region Members inherited from `IWICBitmapSource`

        new void GetSize(
            [Out] out int puiWidth,
            [Out] out int puiHeight);

        new Guid GetPixelFormat();

        new void GetResolution(
            [Out] out double pDpiX,
            [Out] out double pDpiY);

        new void CopyPalette(
            [In] IWICPalette pIPalette);

        new void CopyPixels(
            [In] IntPtr prc, // WICRect*
            [In] int cbStride,
            [In] int cbBufferSize,
            [In, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.U1, SizeParamIndex = 2)] byte[] pbBuffer);

        #endregion

        IWICBitmapLock Lock(
            [In] IntPtr prcLock, // WICRect*
            [In] WICBitmapLockFlags flags);

        void SetPalette(
            [In] IWICPalette pIPalette);

        void SetResolution(
            [In] double dpiX,
            [In] double dpiY);
    }
}
