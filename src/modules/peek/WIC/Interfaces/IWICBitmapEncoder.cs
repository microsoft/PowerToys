using System;
using System.Runtime.InteropServices;

namespace WIC
{
    [ComImport]
    [Guid(IID.IWICBitmapEncoder)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IWICBitmapEncoder
    {
        void Initialize(
            [In] IStream pIStream,
            [In] WICBitmapEncoderCacheOption cacheOption);

        Guid GetContainerFormat();

        IWICBitmapEncoderInfo GetEncoderInfo();

        void SetColorContexts(
            [In] int cCount,
            [In, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.Interface, SizeParamIndex = 0)] IWICColorContext[] ppIColorContext);

        void SetPalette(
            [In] IWICPalette pIPalette);

        void SetThumbnail(
            [In] IWICBitmapSource pIThumbnail);

        void SetPreview(
            [In] IWICBitmapSource pIPreview);

        void CreateNewFrame(
            [Out] out IWICBitmapFrameEncode ppIFrameEncode,
            [In, Out] IPropertyBag2 ppIEncoderOptions);

        void Commit();

        IWICMetadataQueryWriter GetMetadataQueryWriter();
    }
}
