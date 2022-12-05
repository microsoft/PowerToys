using System.Runtime.InteropServices;

namespace WIC
{
    [ComImport]
    [Guid(IID.IPropertyBag2)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPropertyBag2
    {
        void Read(
            [In] int cProperties,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] PROPBAG2[] pPropBag,
            [In] IErrorLog pErrLog,
            [Out, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.Struct, SizeParamIndex = 0)] object[] pvarValue,
            [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] int[] phrError
            );

        void Write(
            [In] int cProperties,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] PROPBAG2[] pPropBag,
            [In, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.Struct, SizeParamIndex = 0)] object[] pvarValue);

        int CountProperties();

        void GetPropertyInfo(
                [In] int iProperty,
                [In] int cProperties,
                [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] PROPBAG2[] pPropBag,
                [Out] out int pcProperties);

        void LoadObject(
                [In, MarshalAs(UnmanagedType.LPWStr)] string pstrName,
                [In] int dwHint,
                [In, MarshalAs(UnmanagedType.IUnknown)] object pUnkObject,
                [In] IErrorLog pErrLog);
    }
}
