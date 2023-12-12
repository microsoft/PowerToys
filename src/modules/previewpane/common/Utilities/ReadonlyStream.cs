// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
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
    public class ReadonlyStream : Stream
    {
        private IStream? _stream;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReadonlyStream"/> class.
        /// </summary>
        /// <param name="stream">A pointer to an <see cref="IStream" /> interface that represents the stream source.</param>
        public ReadonlyStream(IStream stream)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        }

        /// <summary>
        /// Gets a value indicating whether the current stream supports reading.
        /// </summary>
        public override bool CanRead
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the current stream supports seeking.
        /// </summary>
        public override bool CanSeek
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the current stream supports writing.
        /// </summary>
        public override bool CanWrite
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the length in bytes of the stream.
        /// </summary>
        public override long Length
        {
            get
            {
                CheckDisposed();
                System.Runtime.InteropServices.ComTypes.STATSTG stat;

                // Stat called with STATFLAG_NONAME. The pwcsName is not required more details https://learn.microsoft.com/windows/win32/api/wtypes/ne-wtypes-statflag
                _stream.Stat(out stat, 1); // STATFLAG_NONAME

                return stat.cbSize;
            }
        }

        /// <summary>
        /// Gets or Sets the position within the current.
        /// </summary>
        public override long Position
        {
            get
            {
                return Seek(0, SeekOrigin.Current);
            }

            set
            {
                Seek(value, SeekOrigin.Begin);
            }
        }

        /// <summary>
        /// Reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.
        /// </summary>
        /// <param name="buffer">An array of bytes. When this method returns, the buffer contains the specified byte array with the values between offset and (offset + count - 1) replaced by the bytes read from the current source.</param>
        /// <param name="offset">The zero-based byte offset in buffer at which to begin storing the data read from the current stream.</param>
        /// <param name="count">The maximum number of bytes to be read from the current stream.</param>
        /// <returns>The total number of bytes read into the buffer. This can be less than the number of bytes requested if that many bytes are not currently available, or zero if the end of the stream has been reached.</returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            CheckDisposed();

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer), "buffer is null");
            }

            if (offset < 0 || count < 0 || (offset + count) > buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(buffer), "Out of range for buffer");
            }

            byte[] localBuffer = buffer;

            if (offset > 0)
            {
                localBuffer = new byte[count];
            }

            IntPtr bytesReadPtr = Marshal.AllocCoTaskMem(sizeof(int));

            try
            {
                _stream.Read(localBuffer, count, bytesReadPtr);
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

        /// <summary>
        /// Sets the position within the current stream.
        /// </summary>
        /// <param name="offset">A byte offset relative to the origin parameter.</param>
        /// <param name="origin">A value of type System.IO.SeekOrigin indicating the reference point used to obtain the new position.</param>
        /// <returns>The new position within the current stream.</returns>
        public override long Seek(long offset, SeekOrigin origin)
        {
            CheckDisposed();
            int dwOrigin;

            // Maps the SeekOrigin with dworigin more details: https://learn.microsoft.com/windows/win32/api/objidl/ne-objidl-stream_seek
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
                    throw new ArgumentOutOfRangeException(nameof(origin));
            }

            IntPtr posPtr = Marshal.AllocCoTaskMem(sizeof(long));

            try
            {
                _stream.Seek(offset, dwOrigin, posPtr);
                return Marshal.ReadInt64(posPtr);
            }
            finally
            {
                Marshal.FreeCoTaskMem(posPtr);
            }
        }

        /// <summary>
        /// Clears all buffers for this stream and causes any buffered data to be written to the underlying device.
        /// </summary>
        /// <remarks>
        /// Not implemented current implementation supports only read.
        /// </remarks>
        public override void Flush()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///  Sets the length of the current stream.
        /// </summary>
        /// <param name="value">The desired length of the current stream in bytes.</param>
        /// /// <remarks>
        /// Not implemented current implementation supports only read.
        /// </remarks>
        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Writes a sequence of bytes to the current stream and advances the current position within this stream by the number of bytes written.
        /// </summary>
        /// <param name="buffer">An array of bytes. This method copies count bytes from buffer to the current stream.</param>
        /// <param name="offset">The zero-based byte offset in buffer at which to begin copying bytes to the current stream.</param>
        /// <param name="count">The number of bytes to be written to the current stream.</param>
        /// <remarks>
        /// Not implemented current implementation supports only read.
        /// </remarks>
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(true);

            if (_stream != null)
            {
                if (Marshal.IsComObject(_stream))
                {
                    Marshal.ReleaseComObject(_stream);
                }

                _stream = null;
            }
        }

        [MemberNotNull(nameof(_stream))]
        private void CheckDisposed()
        {
            ObjectDisposedException.ThrowIf(_stream == null, this);
        }
    }
}
