
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.CustomMarshalers;

namespace Microsoft.Search.Interop
{
    [ComConversionLoss]
    [Guid("00020402-0000-0000-C000-000000000046")]
    [InterfaceType(1)]
    [ComImport]
    public interface ITypeLib
    {
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void RemoteGetTypeInfoCount(out uint pcTInfo);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void GetTypeInfo([In] uint index, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(TypeToTypeInfoMarshaler))] out Type ppTInfo);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void GetTypeInfoType([In] uint index, out tagTYPEKIND pTKind);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void GetTypeInfoOfGuid([In] ref Guid guid, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(TypeToTypeInfoMarshaler))] out Type ppTInfo);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void RemoteGetLibAttr([Out] IntPtr ppTLibAttr, [ComAliasName("Microsoft.Search.Interop.DWORD")] out uint pDummy);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void GetTypeComp([MarshalAs(UnmanagedType.Interface)] out ITypeComp ppTComp);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void RemoteGetDocumentation(
          [In] int index,
          [In] uint refPtrFlags,
          [MarshalAs(UnmanagedType.BStr)] out string pbstrName,
          [MarshalAs(UnmanagedType.BStr)] out string pBstrDocString,
          out uint pdwHelpContext,
          [MarshalAs(UnmanagedType.BStr)] out string pBstrHelpFile);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void RemoteIsName([MarshalAs(UnmanagedType.LPWStr), In] string szNameBuf, [In] uint lHashVal, out int pfName, [MarshalAs(UnmanagedType.BStr)] out string pBstrLibName);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void RemoteFindName(
          [MarshalAs(UnmanagedType.LPWStr), In] string szNameBuf,
          [In] uint lHashVal,
          [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(TypeToTypeInfoMarshaler))] out Type ppTInfo,
          out int rgMemId,
          [In, Out] ref ushort pcFound,
          [MarshalAs(UnmanagedType.BStr)] out string pBstrLibName);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void LocalReleaseTLibAttr();
    }
}
namespace System.Runtime.InteropServices.CustomMarshalers
{
    internal class TypeToTypeInfoMarshaler : ICustomMarshaler
    {
        private static readonly TypeToTypeInfoMarshaler s_typeToTypeInfoMarshaler = new TypeToTypeInfoMarshaler();

        public static ICustomMarshaler GetInstance(string cookie) => s_typeToTypeInfoMarshaler;

        private TypeToTypeInfoMarshaler()
        {
        }

        public void CleanUpManagedData(object ManagedObj)
        {
        }

        public void CleanUpNativeData(IntPtr pNativeData)
        {
        }

        public int GetNativeDataSize()
        {
            return -1;
        }

        public IntPtr MarshalManagedToNative(object ManagedObj)
        {
            throw new PlatformNotSupportedException();
        }

        public object MarshalNativeToManaged(IntPtr pNativeData)
        {
            throw new PlatformNotSupportedException();
        }
    }
}