using System;
using System.Runtime.InteropServices;

namespace WIC
{
    [ComImport]
    [Guid(IID.IWICMetadataWriter)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IWICMetadataWriter : IWICMetadataReader
    {
        #region Members inherited from `IWICMetadataReader`

        new Guid GetMetadataFormat();

        new IWICMetadataHandlerInfo GetMetadataHandlerInfo();

        new int GetCount();

        new void GetValueByIndex(
            [In] int nIndex,
            [In, Out, MarshalAs(UnmanagedType.Struct)] ref PROPVARIANT pvarSchema,
            [In, Out, MarshalAs(UnmanagedType.Struct)] ref PROPVARIANT pvarId,
            [In, Out, MarshalAs(UnmanagedType.Struct)] ref PROPVARIANT pvarValue);

        new void GetValue(
            [In, MarshalAs(UnmanagedType.Struct)] PROPVARIANT pvarSchema,
            [In, MarshalAs(UnmanagedType.Struct)] PROPVARIANT pvarId,
            [In, Out, MarshalAs(UnmanagedType.Struct)] ref PROPVARIANT pvarValue);

        new IWICEnumMetadataItem GetEnumerator();

        #endregion

        void SetValue(
            [In, MarshalAs(UnmanagedType.Struct)] PROPVARIANT pvarSchema,
            [In, MarshalAs(UnmanagedType.Struct)] PROPVARIANT pvarId,
            [In, MarshalAs(UnmanagedType.Struct)] PROPVARIANT pvarValue);

        void SetValueByIndex(
            [In] int nIndex,
            [In, MarshalAs(UnmanagedType.Struct)] PROPVARIANT pvarSchema,
            [In, MarshalAs(UnmanagedType.Struct)] PROPVARIANT pvarId,
            [In, MarshalAs(UnmanagedType.Struct)] PROPVARIANT pvarValue);

        void RemoveValue(
            [In, MarshalAs(UnmanagedType.Struct)] PROPVARIANT pvarSchema,
            [In, MarshalAs(UnmanagedType.Struct)] PROPVARIANT pvarId);

        void RemoveValueByIndex(
            [In] int nIndex);
    }
}
