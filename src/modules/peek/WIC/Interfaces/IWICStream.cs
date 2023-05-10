using System;
using System.Runtime.InteropServices;

namespace WIC
{
    [ComImport]
    [Guid(IID.IWICStream)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IWICStream : IStream
    {
        #region Members inherited from `IStream`

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

        new void Seek(
            [In] long dlibMove,
            [In] STREAM_SEEK dwOrigin,
            [Out] IntPtr plibNewPosition);

        new void SetSize(
            [In] long libNewSize);

        new void CopyTo(
            [In] IStream pstm,
            [In] long cb,
            [Out] out long pcbRead,
            [Out] out long pcbWritten);

        new void Commit(
            [In] STGC grfCommitFlags);

        new void Revert();

        new void LockRegion(
            [In] long libOffset,
            [In] long cb,
            [In] LOCKTYPE dwLockType);

        new void UnlockRegion(
            [In] long libOffset,
            [In] long cb,
            [In] LOCKTYPE dwLockType);

        new void Stat(
            [In, Out] ref STATSTG pstatstg,
            [In] STATFLAG grfStatFlag);

        new IStream Clone();

        #endregion

        void InitializeFromIStream(
            [In] IStream pIStream);

        void InitializeFromFilename(
            [In, MarshalAs(UnmanagedType.LPWStr)] string wzFileName,
            [In] StreamAccessMode dwDesiredAccess);

        void InitializeFromMemory(
            [In, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.U1, SizeParamIndex = 1)] byte[] pbBuffer,
            [In] int cbBufferSize);

        void InitializeFromIStreamRegion(
            [In] IStream pIStream,
            [In] long ulOffset,
            [In] long ulMaxSize);
    }
}
