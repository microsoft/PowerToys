// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace WorkspacesLauncherUI.UnitTests
{
    // Scheduling gates decorate a real connected pipe; they never synthesize reads, writes, or authentication.
    internal sealed class ControlledPipeStream : Stream
    {
        private readonly Stream _inner;
        private int _writeCalls;
        private int _readCalls;

        internal ControlledPipeStream(Stream inner)
        {
            _inner = inner;
        }

        internal int GateWriteCall { get; init; }

        internal int GateReadCall { get; init; }

        internal bool DelayCancellation { get; init; }

        internal int BodyPrefixLength { get; init; }

        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource Canceled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource Unwind { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal Channel<int> Reads { get; } = Channel.CreateUnbounded<int>();

        public override bool CanRead => _inner.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => _inner.CanWrite;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _writeCalls) == GateWriteCall)
            {
                await _inner.WriteAsync(buffer[..BodyPrefixLength], cancellationToken);
                Entered.TrySetResult();
                try
                {
                    await Release.Task.WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    Canceled.TrySetResult();
                    if (DelayCancellation)
                    {
                        await Unwind.Task.WaitAsync(LauncherPipeTestSession.Bound, CancellationToken.None);
                    }

                    throw;
                }

                buffer = buffer[BodyPrefixLength..];
            }

            await _inner.WriteAsync(buffer, cancellationToken);
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var count = await _inner.ReadAsync(buffer, cancellationToken);
            Reads.Writer.TryWrite(count);
            if (Interlocked.Increment(ref _readCalls) == GateReadCall)
            {
                Entered.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }

            return count;
        }

        public override void Flush() => _inner.Flush();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
