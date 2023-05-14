using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WIC
{
    [ComImport]
    [Guid(IID.IWICBitmapDecoderInfo)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IWICBitmapDecoderInfo : IWICBitmapCodecInfo
    {
        #region Members inherited from `IWICBitmapCodecInfo`

        #region Members inherited from `IWICComponentInfo`

        new WICComponentType GetComponentType();

        new Guid GetCLSID();

        new WICComponentSigning GetSigningStatus();

        new void GetAuthor(
            [In] int cchAuthor,
            [In, Out, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.U2, SizeParamIndex = 0)] char[] wzAuthor,
            [Out] out int pcchActual);

        new Guid GetVendorGUID();

        new void GetVersion(
            [In] int cchVersion,
            [In, Out, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.U2, SizeParamIndex = 0)] char[] wzVersion,
            [Out] out int pcchActual);

        new void GetSpecVersion(
            [In] int cchSpecVersion,
            [In, Out, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.U2, SizeParamIndex = 0)] char[] wzSpecVersion,
            [Out] out int pcchActual);

        new void GetFriendlyName(
            [In] int cchFriendlyName,
            [In, Out, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.U2, SizeParamIndex = 0)] char[] wzFriendlyName,
            [Out] out int pcchActual);

        #endregion

        new Guid GetContainerFormat();

        new void GetPixelFormats(
            [In] int cFormats,
            [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] Guid[] pguidPixelFormats,
            [Out] out int pcActual);

        new void GetColorManagementVersion(
            [In] int cchColorManagementVersion,
            [In, Out, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.U2, SizeParamIndex = 0)] char[] wzColorManagementVersion,
            [Out] out int pcchActual);

        new void GetDeviceManufacturer(
            [In] int cchDeviceManufacturer,
            [In, Out, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.U2, SizeParamIndex = 0)] char[] wzDeviceManufacturer,
            [Out] out int pcchActual);

        new void GetDeviceModels(
            [In] int cchDeviceModels,
            [In, Out, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.U2, SizeParamIndex = 0)] char[] wzDeviceModels,
            [Out] out int pcchActual);

        new void GetMimeTypes(
            [In] int cchMimeTypes,
            [In, Out, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.U2, SizeParamIndex = 0)] char[] wzMimeTypes,
            [Out] out int pcchActual);

        new void GetFileExtensions(
            [In] int cchFileExtensions,
            [In, Out, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.U2, SizeParamIndex = 0)] char[] wzFileExtensions,
            [Out] out int pcchActual);

        new bool DoesSupportAnimation();

        new bool DoesSupportChromakey();

        new bool DoesSupportLossless();

        new bool DoesSupportMultiframe();

        new bool MatchesMimeType(
            [In, MarshalAs(UnmanagedType.LPWStr)] string wzMimeType);

        #endregion

        void GetPatterns(
            [In] int cbSizePatterns,
            [In] IntPtr pPatterns,
            [Out] out int pcPatterns,
            [Out] out int pcbPatternsActual);

        bool MatchesPattern(
            [In] IStream pIStream);

        IWICBitmapDecoder CreateInstance();
    }
}
