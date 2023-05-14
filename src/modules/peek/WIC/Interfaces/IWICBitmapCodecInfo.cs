using System;
using System.Runtime.InteropServices;

namespace WIC
{
    [ComImport]
    [Guid(IID.IWICBitmapCodecInfo)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IWICBitmapCodecInfo : IWICComponentInfo
    {
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

        Guid GetContainerFormat();

        void GetPixelFormats(
            [In] int cFormats,
            [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] Guid[] pguidPixelFormats,
            [Out] out int pcActual);

        void GetColorManagementVersion(
            [In] int cchColorManagementVersion,
            [In, Out, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.U2, SizeParamIndex = 0)] char[] wzColorManagementVersion,
            [Out] out int pcchActual);

        void GetDeviceManufacturer(
            [In] int cchDeviceManufacturer,
            [In, Out, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.U2, SizeParamIndex = 0)] char[] wzDeviceManufacturer,
            [Out] out int pcchActual);

        void GetDeviceModels(
            [In] int cchDeviceModels,
            [In, Out, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.U2, SizeParamIndex = 0)] char[] wzDeviceModels,
            [Out] out int pcchActual);

        void GetMimeTypes(
            [In] int cchMimeTypes,
            [In, Out, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.U2, SizeParamIndex = 0)] char[] wzMimeTypes,
            [Out] out int pcchActual);

        void GetFileExtensions(
            [In] int cchFileExtensions,
            [In, Out, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.U2, SizeParamIndex = 0)] char[] wzFileExtensions,
            [Out] out int pcchActual);

        bool DoesSupportAnimation();

        bool DoesSupportChromakey();

        bool DoesSupportLossless();

        bool DoesSupportMultiframe();

        bool MatchesMimeType(
            [In, MarshalAs(UnmanagedType.LPWStr)] string wzMimeType);
    }
}
