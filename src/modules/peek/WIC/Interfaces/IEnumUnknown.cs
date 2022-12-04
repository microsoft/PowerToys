using System.Runtime.InteropServices;

namespace WIC
{
    [ComImport]
    [Guid(IID.IEnumUnknown)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IEnumUnknown
    {
        void Next(
            [In] int celt,
            [Out, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.Interface, SizeParamIndex = 0)] object[] rgelt,
            [Out] out int pceltFetched);

        void Skip(
            [In] int celt);

        void Reset();

        IEnumUnknown Clone();
    }
}
