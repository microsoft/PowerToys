// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace PowerOCR.Models;

/// <summary>
/// A <see cref="Stream"/> that wraps another stream. The major feature of <see cref="WrappingStream"/> is that it does not dispose the
/// underlying stream when it is disposed; this is useful when using classes such as <see cref="BinaryReader"/> and
/// <see cref="System.Security.Cryptography.CryptoStream"/> that take ownership of the stream passed to their constructors.
/// </summary>
public class WrappingStream : Stream
{
    private Stream? _streamBase;

    /// <summary>
    /// Initializes a new instance of the <see cref="WrappingStream"/> class.
    /// </summary>
    /// <param name="streamBase">The wrapped stream.</param>
    public WrappingStream(Stream streamBase)
    {
        _streamBase = streamBase ?? throw new ArgumentNullException(nameof(streamBase));
    }

    /// <summary>
    /// Gets a value indicating whether the current stream supports reading.
    /// </summary>
    /// <returns><c>true</c> if the stream supports reading; otherwise, <c>false</c>.</returns>
    public override bool CanRead => _streamBase != null && _streamBase.CanRead;

    /// <summary>
    /// Gets a value indicating whether the current stream supports seeking.
    /// </summary>
    /// <returns><c>true</c> if the stream supports seeking; otherwise, <c>false</c>.</returns>
    public override bool CanSeek => _streamBase != null && _streamBase.CanSeek;

    /// <summary>
    /// Gets a value indicating whether the current stream supports writing.
    /// </summary>
    /// <returns><c>true</c> if the stream supports writing; otherwise, <c>false</c>.</returns>
    public override bool CanWrite => _streamBase != null && _streamBase.CanWrite;

    /// <summary>
    /// Gets the length in bytes of the stream.
    /// </summary>
    public override long Length
    {
        get
        {
            ThrowIfDisposed();
            return _streamBase is not null ? _streamBase.Length : 0;
        }
    }

    /// <summary>
    /// Gets or sets the position within the current stream.
    /// </summary>
    public override long Position
    {
        get
        {
            ThrowIfDisposed();
            return _streamBase is not null ? _streamBase.Position : 0;
        }

        set
        {
            ThrowIfDisposed();
            if (_streamBase is not null)
            {
                _streamBase.Position = value;
            }
        }
    }

    /// <summary>
    /// Begins an asynchronous read operation.
    /// </summary>
    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
    {
        ThrowIfDisposed();

        return _streamBase is not null && callback is not null && state is not null
            ? _streamBase.BeginRead(buffer, offset, count, callback, state)
            : new NullAsyncResult();
    }

    /// <summary>
    /// Begins an asynchronous write operation.
    /// </summary>
    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
    {
        ThrowIfDisposed();

        return _streamBase is not null && callback is not null && state is not null
            ? _streamBase.BeginWrite(buffer, offset, count, callback, state)
            : new NullAsyncResult();
    }

    /// <summary>
    /// Waits for the pending asynchronous read to complete.
    /// </summary>
    public override int EndRead(IAsyncResult asyncResult)
    {
        ThrowIfDisposed();

        return _streamBase is not null ? _streamBase.EndRead(asyncResult) : 0;
    }

    /// <summary>
    /// Ends an asynchronous write operation.
    /// </summary>
    public override void EndWrite(IAsyncResult asyncResult)
    {
        ThrowIfDisposed();

        _streamBase?.EndWrite(asyncResult);
    }

    /// <summary>
    /// Clears all buffers for this stream and causes any buffered data to be written to the underlying device.
    /// </summary>
    public override void Flush()
    {
        ThrowIfDisposed();

        _streamBase?.Flush();
    }

    /// <summary>
    /// Reads a sequence of bytes from the current stream and advances the position
    /// within the stream by the number of bytes read.
    /// </summary>
    public override int Read(byte[] buffer, int offset, int count)
    {
        ThrowIfDisposed();

        return _streamBase is not null ? _streamBase.Read(buffer, offset, count) : 0;
    }

    /// <summary>
    /// Reads a byte from the stream and advances the position within the stream by one byte, or returns -1 if at the end of the stream.
    /// </summary>
    public override int ReadByte()
    {
        ThrowIfDisposed();

        return _streamBase is not null ? _streamBase.ReadByte() : 0;
    }

    /// <summary>
    /// Sets the position within the current stream.
    /// </summary>
    /// <param name="offset">A byte offset relative to the <paramref name="origin"/> parameter.</param>
    /// <param name="origin">A value of type see System.IO.SeekOrigin indicating the reference point used to obtain the new position.</param>
    /// <returns>The new position within the current stream.</returns>
    public override long Seek(long offset, SeekOrigin origin)
    {
        ThrowIfDisposed();

        return _streamBase is not null ? _streamBase.Seek(offset, origin) : 0;
    }

    /// <summary>
    /// Sets the length of the current stream.
    /// </summary>
    /// <param name="value">The desired length of the current stream in bytes.</param>
    public override void SetLength(long value)
    {
        ThrowIfDisposed();

        _streamBase?.SetLength(value);
    }

    /// <summary>
    /// Writes a sequence of bytes to the current stream and advances the current position
    /// within this stream by the number of bytes written.
    /// </summary>
    public override void Write(byte[] buffer, int offset, int count)
    {
        ThrowIfDisposed();

        _streamBase?.Write(buffer, offset, count);
    }

    /// <summary>
    /// Writes a byte to the current position in the stream and advances the position within the stream by one byte.
    /// </summary>
    public override void WriteByte(byte value)
    {
        ThrowIfDisposed();

        _streamBase?.WriteByte(value);
    }

    /// <summary>
    /// Gets the wrapped stream.
    /// </summary>
    /// <value>The wrapped stream.</value>
    protected Stream? WrappedStream => _streamBase;

    /// <summary>
    /// Releases the unmanaged resources used by the <see cref="WrappingStream"/> and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected override void Dispose(bool disposing)
    {
        // doesn't close the base stream, but just prevents access to it through this WrappingStream
        if (disposing)
        {
            _streamBase = null;
        }

        base.Dispose(disposing);
    }

    private void ThrowIfDisposed()
    {
        // throws an ObjectDisposedException if this object has been disposed
        ObjectDisposedException.ThrowIf(_streamBase == null, this);
    }
}
