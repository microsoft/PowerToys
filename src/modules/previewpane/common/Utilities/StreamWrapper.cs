// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Common.Utilities
{
    /// <summary>
    /// Wraps <see cref="IStream"/> interface into <see cref="Stream"/> Class.
    /// </summary>
    /// <remarks>
    /// Implements only read from the stream functionality.
    /// </remarks>
    public class StreamWrapper : Stream
    {
        private IStream stream;

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamWrapper"/> class.
        /// </summary>
        /// <param name="stream">A pointer to an <see cref="IStream" /> interface that represents the stream source.</param>
        public StreamWrapper(IStream stream)
        {
            this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
        }

        /// <inheritdoc/>
        public override bool CanRead
        {
            get
            {
                return true;
            }
        }

        /// <inheritdoc/>
        public override bool CanSeek
        {
            get
            {
                return true;
            }
        }

        /// <inheritdoc/>
        public override bool CanWrite
        {
            get
            {
                return false;
            }
        }

        /// <inheritdoc/>
        public override long Length
        {
            get
            {
                this.CheckDisposed();
                System.Runtime.InteropServices.ComTypes.STATSTG stat;
                this.stream.Stat(out stat, 1); // STATFLAG_NONAME

                return stat.cbSize;
            }
        }

        /// <inheritdoc/>
        public override long Position
        {
            get
            {
                return this.Seek(0, SeekOrigin.Current);
            }

            set
            {
                this.Seek(value, SeekOrigin.Begin);
            }
        }

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count)
        {
            this.CheckDisposed();

            if (offset < 0 || count < 0 || offset + count > buffer.Length)
            {
                throw new ArgumentOutOfRangeException();
            }

            byte[] localBuffer = buffer;

            if (offset > 0)
            {
                localBuffer = new byte[count];
            }

            IntPtr bytesReadPtr = Marshal.AllocCoTaskMem(sizeof(int));

            try
            {
                this.stream.Read(localBuffer, count, bytesReadPtr);
                int bytesRead = Marshal.ReadInt32(bytesReadPtr);

                if (offset > 0)
                {
                    Array.Copy(localBuffer, 0, buffer, offset, bytesRead);
                }

                return bytesRead;
            }
            finally
            {
                Marshal.FreeCoTaskMem(bytesReadPtr);
            }
        }

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin)
        {
            this.CheckDisposed();

            int dwOrigin;

            switch (origin)
            {
                case SeekOrigin.Begin:
                    dwOrigin = 0;   // STREAM_SEEK_SET
                    break;

                case SeekOrigin.Current:
                    dwOrigin = 1;   // STREAM_SEEK_CUR
                    break;

                case SeekOrigin.End:
                    dwOrigin = 2;   // STREAM_SEEK_END
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }

            IntPtr posPtr = Marshal.AllocCoTaskMem(sizeof(long));

            try
            {
                this.stream.Seek(offset, dwOrigin, posPtr);
                return Marshal.ReadInt64(posPtr);
            }
            finally
            {
                Marshal.FreeCoTaskMem(posPtr);
            }
        }

        /// <inheritdoc/>
        public override void Flush()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (this.stream != null)
            {
                Marshal.ReleaseComObject(this.stream);
                this.stream = null;
            }
        }

        private void CheckDisposed()
        {
            if (this.stream == null)
            {
                throw new ObjectDisposedException(nameof(StreamWrapper));
            }
        }
    }
}
