
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.Search.Interop
{
    [Guid("0000000B-0000-0000-C000-000000000046")]
    [InterfaceType(1)]
    [ComImport]
    public interface IStorage
    {
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void CreateStream(
          [MarshalAs(UnmanagedType.LPWStr), In] string pwcsName,
          [In] uint grfMode,
          [In] uint reserved1,
          [In] uint reserved2,
          [MarshalAs(UnmanagedType.Interface)] out IStream ppstm);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void RemoteOpenStream(
          [MarshalAs(UnmanagedType.LPWStr), In] string pwcsName,
          [In] uint cbReserved1,
          [In] ref byte reserved1,
          [In] uint grfMode,
          [In] uint reserved2,
          [MarshalAs(UnmanagedType.Interface)] out IStream ppstm);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void CreateStorage(
          [MarshalAs(UnmanagedType.LPWStr), In] string pwcsName,
          [In] uint grfMode,
          [In] uint reserved1,
          [In] uint reserved2,
          [MarshalAs(UnmanagedType.Interface)] out IStorage ppstg);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void OpenStorage(
          [MarshalAs(UnmanagedType.LPWStr), In] string pwcsName,
          [MarshalAs(UnmanagedType.Interface), In] IStorage pstgPriority,
          [In] uint grfMode,
          [ComAliasName("Microsoft.Search.Interop.wireSNB"), In] ref tagRemSNB snbExclude,
          [In] uint reserved,
          [MarshalAs(UnmanagedType.Interface)] out IStorage ppstg);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void RemoteCopyTo(
          [In] uint ciidExclude,
          [In] ref Guid rgiidExclude,
          [ComAliasName("Microsoft.Search.Interop.wireSNB"), In] ref tagRemSNB snbExclude,
          [MarshalAs(UnmanagedType.Interface), In] IStorage pstgDest);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void MoveElementTo([MarshalAs(UnmanagedType.LPWStr), In] string pwcsName, [MarshalAs(UnmanagedType.Interface), In] IStorage pstgDest, [MarshalAs(UnmanagedType.LPWStr), In] string pwcsNewName, [In] uint grfFlags);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void Commit([In] uint grfCommitFlags);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void Revert();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void RemoteEnumElements(
          [In] uint reserved1,
          [In] uint cbReserved2,
          [In] ref byte reserved2,
          [In] uint reserved3,
          [MarshalAs(UnmanagedType.Interface)] out IEnumSTATSTG ppenum);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void DestroyElement([MarshalAs(UnmanagedType.LPWStr), In] string pwcsName);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void RenameElement([MarshalAs(UnmanagedType.LPWStr), In] string pwcsOldName, [MarshalAs(UnmanagedType.LPWStr), In] string pwcsNewName);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void SetElementTimes(
          [MarshalAs(UnmanagedType.LPWStr), In] string pwcsName,
          [In] ref _FILETIME pctime,
          [In] ref _FILETIME patime,
          [In] ref _FILETIME pmtime);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void SetClass([In] ref Guid clsid);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void SetStateBits([In] uint grfStateBits, [In] uint grfMask);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void Stat(out tagSTATSTG pstatstg, [In] uint grfStatFlag);
    }
}
