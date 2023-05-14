using System;
using System.Runtime.InteropServices;

namespace WIC
{
    [ComImport]
    [Guid(IID.IWICComponentFactory)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IWICComponentFactory : IWICImagingFactory
    {
        #region Members inherited from `IWICImagingFactory`

        new IWICBitmapDecoder CreateDecoderFromFilename(
            [In, MarshalAs(UnmanagedType.LPWStr)] string wzFilename,
            [In] IntPtr pguidVendor,
            [In] StreamAccessMode dwDesiredAccess,
            [In] WICDecodeOptions metadataOptions);

        new IWICBitmapDecoder CreateDecoderFromStream(
            [In] IStream pIStream,
            [In] IntPtr pguidVendor,
            [In] WICDecodeOptions metadataOptions);

        new IWICBitmapDecoder CreateDecoderFromFileHandle(
            [In] IntPtr hFile,
            [In] IntPtr pguidVendor,
            [In] WICDecodeOptions metadataOptions);

        new IWICComponentInfo CreateComponentInfo(
            [In] Guid clsidComponent);

        new IWICBitmapDecoder CreateDecoder(
            [In, MarshalAs(UnmanagedType.LPStruct)] Guid guidContainerFormat,
            [In] IntPtr pguidVendor);

        new IWICBitmapEncoder CreateEncoder(
            [In, MarshalAs(UnmanagedType.LPStruct)] Guid guidContainerFormat,
            [In] IntPtr pguidVendor);

        new IWICPalette CreatePalette();

        new IWICFormatConverter CreateFormatConverter();

        new IWICBitmapScaler CreateBitmapScaler();

        new IWICBitmapClipper CreateBitmapClipper();

        new IWICBitmapFlipRotator CreateBitmapFlipRotator();

        new IWICStream CreateStream();

        new IWICColorContext CreateColorContext();

        new IWICColorTransform CreateColorTransformer();

        new IWICBitmap CreateBitmap(
            [In] int uiWidth,
            [In] int uiHeight,
            [In, MarshalAs(UnmanagedType.LPStruct)] Guid pixelFormat,
            [In] WICBitmapCreateCacheOption option);

        new IWICBitmap CreateBitmapFromSource(
            [In] IWICBitmapSource pIBitmapSource,
            [In] WICBitmapCreateCacheOption option);

        new IWICBitmap CreateBitmapFromSourceRect(
            [In] IWICBitmapSource pIBitmapSource,
            [In] int x,
            [In] int y,
            [In] int width,
            [In] int height);

        new IWICBitmap CreateBitmapFromMemory(
            [In] int uiWidth,
            [In] int uiHeight,
            [In, MarshalAs(UnmanagedType.LPStruct)] Guid pixelFormat,
            [In] int cbStride,
            [In] int cbBufferSize,
            [In, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.U1, SizeParamIndex = 4)] byte[] pbBuffer);

        new IWICBitmap CreateBitmapFromHBITMAP(
            [In] IntPtr hBitmap,
            [In] IntPtr hPalette,
            [In] WICBitmapAlphaChannelOption options);

        new IWICBitmap CreateBitmapFromHICON(
            [In] IntPtr hIcon);

        new IEnumUnknown CreateComponentEnumerator(
            [In] WICComponentType componentTypes,
            [In] WICComponentEnumerateOptions options);

        new IWICFastMetadataEncoder CreateFastMetadataEncoderFromDecoder(
            [In] IWICBitmapDecoder pIDecoder);

        new IWICFastMetadataEncoder CreateFastMetadataEncoderFromFrameDecode(
            [In] IWICBitmapFrameDecode pIFrameDecoder);

        new IWICMetadataQueryWriter CreateQueryWriter(
            [In, MarshalAs(UnmanagedType.LPStruct)] Guid guidMetadataFormat,
            [In] IntPtr pguidVendor);

        new IWICMetadataQueryWriter CreateQueryWriterFromReader(
            [In] IWICMetadataQueryReader pIQueryReader,
            [In] IntPtr pguidVendor);

        #endregion

        IWICMetadataReader CreateMetadataReader(
            [In, MarshalAs(UnmanagedType.LPStruct)] Guid guidContainerFormat,
            [In] IntPtr pguidVendor,
            [In] MetadataCreationAndPersistOptions dwOptions,
            [In] IStream pIStream);

        IWICMetadataReader CreateMetadataReaderFromContainer(
            [In, MarshalAs(UnmanagedType.LPStruct)] Guid guidContainerFormat,
            [In] IntPtr pguidVendor,
            [In] MetadataCreationAndPersistOptions dwOptions,
            [In] IStream pIStream);

        IWICMetadataWriter CreateMetadataWriter(
            [In, MarshalAs(UnmanagedType.LPStruct)] Guid guidContainerFormat,
            [In] IntPtr pguidVendor,
            [In] WICMetadataCreationOptions dwMetadataOptions);

        IWICMetadataWriter CreateMetadataWriterFromReader(
            [In] IWICMetadataReader pIReader,
            [In] IntPtr pguidVendor);

        IWICMetadataQueryReader CreateQueryReaderFromBlockReader(
            [In] IWICMetadataBlockReader pIBlockReader);

        IWICMetadataQueryWriter CreateQueryWriterFromBlockWriter(
            [In] IWICMetadataBlockWriter pIBlockWriter);

        IPropertyBag2 CreateEncoderPropertyBag(
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] PROPBAG2[] ppropOptions,
            [In] int cCount);
    };
}
