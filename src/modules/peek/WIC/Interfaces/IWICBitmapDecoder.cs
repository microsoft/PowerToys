using System;
using System.Runtime.InteropServices;

namespace WIC
{
    [ComImport]
    [Guid(IID.IWICBitmapDecoder)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IWICBitmapDecoder
    {
        WICBitmapDecoderCapabilities QueryCapability(
            [In] IStream pIStream);

        void Initialize(
            [In] IStream pIStream,
            [In] WICDecodeOptions cacheOptions);

        Guid GetContainerFormat();

        IWICBitmapDecoderInfo GetDecoderInfo();

        void CopyPalette(
            [In] IWICPalette pIPalette);

        IWICMetadataQueryReader GetMetadataQueryReader();

        IWICBitmapSource GetPreview();

        void GetColorContexts(
            [In] int cCount,
            [In, Out, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.Interface, SizeParamIndex = 0)] IWICColorContext[] ppIColorContexts,
            [Out] out int pcActualCount);

        IWICBitmapSource GetThumbnail();

        int GetFrameCount();

        IWICBitmapFrameDecode GetFrame(
            [In] int index);
    }
}
