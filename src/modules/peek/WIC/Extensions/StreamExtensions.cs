using System;
using System.ComponentModel;
using System.IO;

using Marshal = System.Runtime.InteropServices.Marshal;

namespace WIC
{
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static class StreamExtensions
    {
        public static IStream AsCOMStream(this Stream stream)
        {
            return new COMStream(stream);
        }

        private sealed class COMStream : IStream
        {
            public COMStream(Stream underlyingStream)
            {
                UnderlyingStream = underlyingStream;
            }

            private Stream UnderlyingStream { get; }

            public void Read(byte[] pv, int cb, IntPtr pcbRead)
            {
                int bytesRead = UnderlyingStream.Read(pv, 0, cb);
                if (pcbRead != IntPtr.Zero)
                {
                    Marshal.WriteInt32(pcbRead, 0, bytesRead);
                }
            }

            public void Write(byte[] pv, int cb, IntPtr pcbWritten)
            {
                var bytesWritten = Math.Min(pv.Length, cb);
                UnderlyingStream.Write(pv, 0, bytesWritten);
                if (pcbWritten != IntPtr.Zero)
                {
                    Marshal.WriteInt32(pcbWritten, 0, bytesWritten);
                }
            }

            public void Seek(long dlibMove, STREAM_SEEK dwOrigin, IntPtr plibNewPosition)
            {
                SeekOrigin seekOrigin;
                switch (dwOrigin)
                {
                    case STREAM_SEEK.STREAM_SEEK_SET:
                        seekOrigin = SeekOrigin.Begin;
                        break;
                    case STREAM_SEEK.STREAM_SEEK_CUR:
                        seekOrigin = SeekOrigin.Current;
                        break;
                    case STREAM_SEEK.STREAM_SEEK_END:
                        seekOrigin = SeekOrigin.End;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(dwOrigin));
                }
                long newPosition = UnderlyingStream.Seek(dlibMove, seekOrigin);
                if (plibNewPosition != IntPtr.Zero)
                {
                    Marshal.WriteInt64(plibNewPosition, 0, newPosition);
                }
            }

            public void SetSize(long libNewSize)
            {
                UnderlyingStream.SetLength(libNewSize);
            }

            void IStream.CopyTo(IStream pstm, long cb, out long pcbRead, out long pcbWritten)
            {
                throw new System.NotImplementedException();
            }

            void IStream.Commit(STGC grfCommitFlags)
            {
                throw new System.NotImplementedException();
            }

            void IStream.Revert()
            {
                throw new System.NotImplementedException();
            }

            void IStream.LockRegion(long libOffset, long cb, LOCKTYPE dwLockType)
            {
                throw new System.NotImplementedException();
            }

            void IStream.UnlockRegion(long libOffset, long cb, LOCKTYPE dwLockType)
            {
                throw new System.NotImplementedException();
            }

            public void Stat(ref STATSTG pstatstg, STATFLAG grfStatFlag)
            {
                pstatstg = new STATSTG();
                pstatstg.type = STGTY.STGTY_STREAM;
                pstatstg.cbSize = UnderlyingStream.Length;
                pstatstg.grfMode = 0;
                if (UnderlyingStream.CanRead && UnderlyingStream.CanWrite)
                {
                    pstatstg.grfMode |= STGM.STGM_READWRITE;
                }
                else if (UnderlyingStream.CanRead)
                {
                    pstatstg.grfMode |= STGM.STGM_READ;
                }
                else if (UnderlyingStream.CanWrite)
                {
                    pstatstg.grfMode |= STGM.STGM_WRITE;
                }
                else
                {
                    throw new ObjectDisposedException(null);
                }
            }

            IStream IStream.Clone()
            {
                throw new System.NotImplementedException();
            }
        }
    }
}
