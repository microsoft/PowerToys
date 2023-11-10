// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;

namespace Peek.FilePreviewer.Previewers.Helpers
{
    public unsafe class IStreamWrapper : IStream
    {
        public Stream Stream { get; }

        public IStreamWrapper(Stream stream) => Stream = stream;

        public HRESULT Read(void* pv, uint cb, [Optional] uint* pcbRead)
        {
            try
            {
                int read = Stream.Read(new Span<byte>(pv, (int)cb));
                if (pcbRead != null)
                {
                    *pcbRead = (uint)read;
                }

                return (HRESULT)0;
            }
            catch (Exception ex)
            {
                return (HRESULT)Marshal.GetHRForException(ex);
            }
        }

        public HRESULT Write(void* pv, uint cb, [Optional] uint* pcbWritten)
        {
            try
            {
                Stream.Write(new ReadOnlySpan<byte>(pv, (int)cb));
                if (pcbWritten != null)
                {
                    *pcbWritten = cb;
                }

                return (HRESULT)0;
            }
            catch (Exception ex)
            {
                return (HRESULT)Marshal.GetHRForException(ex);
            }
        }

        public void Seek(long dlibMove, STREAM_SEEK dwOrigin, [Optional] ulong* plibNewPosition)
        {
            long position = Stream.Seek(dlibMove, (SeekOrigin)dwOrigin);
            if (plibNewPosition != null)
            {
                *plibNewPosition = (ulong)position;
            }
        }

        public void SetSize(ulong libNewSize)
        {
            Stream.SetLength((long)libNewSize);
        }

        public void CopyTo(IStream pstm, ulong cb, [Optional] ulong* pcbRead, [Optional] ulong* pcbWritten)
        {
            throw new NotSupportedException();
        }

        public void Commit(STGC grfCommitFlags)
        {
            throw new NotSupportedException();
        }

        public void Revert()
        {
            throw new NotSupportedException();
        }

        public void LockRegion(ulong libOffset, ulong cb, uint dwLockType)
        {
            throw new NotSupportedException();
        }

        public void UnlockRegion(ulong libOffset, ulong cb, uint dwLockType)
        {
            throw new NotSupportedException();
        }

        public void Stat(STATSTG* pstatstg, uint grfStatFlag)
        {
            throw new NotSupportedException();
        }

        public void Clone(out IStream ppstm)
        {
            throw new NotSupportedException();
        }
    }
}
