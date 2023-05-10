using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WIC
{
    [ComImport]
    [Guid(IID.IStream)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IStream : ISequentialStream
    {
        #region Members inherited from `ISequentialStream`

        new void Read(
            [Out, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.U1, SizeParamIndex = 1)] byte[] pv,
            [In] int cb,
            [Out] IntPtr pcbRead);

        new void Write(
            [In, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.U1, SizeParamIndex = 1)] byte[] pv,
            [In] int cb,
            [Out] IntPtr pcbWritten);

        #endregion

        void Seek(
            [In] long dlibMove,
            [In] STREAM_SEEK dwOrigin,
            [Out] IntPtr plibNewPosition);

        void SetSize(
            [In] long libNewSize);

        void CopyTo(
            [In] IStream pstm,
            [In] long cb,
            [Out] out long pcbRead,
            [Out] out long pcbWritten);

        void Commit(
            [In] STGC grfCommitFlags);

        void Revert();

        void LockRegion(
            [In] long libOffset,
            [In] long cb,
            [In] LOCKTYPE dwLockType);

        void UnlockRegion(
            [In] long libOffset,
            [In] long cb,
            [In] LOCKTYPE dwLockType);

        void Stat(
            [In, Out] ref STATSTG pstatstg,
            [In] STATFLAG grfStatFlag);

        IStream Clone();
    }
}
