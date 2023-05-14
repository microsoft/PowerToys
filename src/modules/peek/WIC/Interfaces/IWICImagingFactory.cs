using System;
using System.Runtime.InteropServices;

namespace WIC
{
    [ComImport]
    [Guid(IID.IWICImagingFactory)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IWICImagingFactory
    {
        IWICBitmapDecoder CreateDecoderFromFilename(
            [In, MarshalAs(UnmanagedType.LPWStr)] string wzFilename,
            [In] IntPtr pguidVendor,
            [In] StreamAccessMode dwDesiredAccess,
            [In] WICDecodeOptions metadataOptions);

        IWICBitmapDecoder CreateDecoderFromStream(
            [In] IStream pIStream,
            [In] IntPtr pguidVendor,
            [In] WICDecodeOptions metadataOptions);

        IWICBitmapDecoder CreateDecoderFromFileHandle(
            [In] IntPtr hFile,
            [In] IntPtr pguidVendor,
            [In] WICDecodeOptions metadataOptions);

        IWICComponentInfo CreateComponentInfo(
            [In] Guid clsidComponent);

        IWICBitmapDecoder CreateDecoder(
            [In, MarshalAs(UnmanagedType.LPStruct)] Guid guidContainerFormat,
            [In] IntPtr pguidVendor);

        IWICBitmapEncoder CreateEncoder(
            [In, MarshalAs(UnmanagedType.LPStruct)] Guid guidContainerFormat,
            [In] IntPtr pguidVendor);

        IWICPalette CreatePalette();

        IWICFormatConverter CreateFormatConverter();

        IWICBitmapScaler CreateBitmapScaler();

        IWICBitmapClipper CreateBitmapClipper();

        IWICBitmapFlipRotator CreateBitmapFlipRotator();

        IWICStream CreateStream();

        IWICColorContext CreateColorContext();

        IWICColorTransform CreateColorTransformer();

        IWICBitmap CreateBitmap(
            [In] int uiWidth,
            [In] int uiHeight,
            [In, MarshalAs(UnmanagedType.LPStruct)] Guid pixelFormat,
            [In] WICBitmapCreateCacheOption option);

        IWICBitmap CreateBitmapFromSource(
            [In] IWICBitmapSource pIBitmapSource,
            [In] WICBitmapCreateCacheOption option);

        IWICBitmap CreateBitmapFromSourceRect(
            [In] IWICBitmapSource pIBitmapSource,
            [In] int x,
            [In] int y,
            [In] int width,
            [In] int height);

        IWICBitmap CreateBitmapFromMemory(
            [In] int uiWidth,
            [In] int uiHeight,
            [In, MarshalAs(UnmanagedType.LPStruct)] Guid pixelFormat,
            [In] int cbStride,
            [In] int cbBufferSize,
            [In, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.U1, SizeParamIndex = 4)] byte[] pbBuffer);

        IWICBitmap CreateBitmapFromHBITMAP(
            [In] IntPtr hBitmap,
            [In] IntPtr hPalette,
            [In] WICBitmapAlphaChannelOption options);

        IWICBitmap CreateBitmapFromHICON(
            [In] IntPtr hIcon);

        IEnumUnknown CreateComponentEnumerator(
            [In] WICComponentType componentTypes,
            [In] WICComponentEnumerateOptions options);

        IWICFastMetadataEncoder CreateFastMetadataEncoderFromDecoder(
            [In] IWICBitmapDecoder pIDecoder);

        IWICFastMetadataEncoder CreateFastMetadataEncoderFromFrameDecode(
            [In] IWICBitmapFrameDecode pIFrameDecoder);

        IWICMetadataQueryWriter CreateQueryWriter(
            [In, MarshalAs(UnmanagedType.LPStruct)] Guid guidMetadataFormat,
            [In] IntPtr pguidVendor);

        IWICMetadataQueryWriter CreateQueryWriterFromReader(
            [In] IWICMetadataQueryReader pIQueryReader,
            [In] IntPtr pguidVendor);
    }
}
