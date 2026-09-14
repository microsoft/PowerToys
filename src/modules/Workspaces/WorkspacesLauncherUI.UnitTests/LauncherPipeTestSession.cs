// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesLauncherUI.IPC;

namespace WorkspacesLauncherUI.UnitTests
{
    internal sealed class LauncherPipeTestSession : IAsyncDisposable
    {
        internal static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

        private readonly Channel<LauncherMessage> _messages = Channel.CreateUnbounded<LauncherMessage>();
        private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _closedCount;

        internal LauncherPipeTestSession(
            int operationTimeoutMilliseconds = 2000,
            Func<int, LauncherProcessIdentity> openParent = null,
            int? launcherProcessId = null,
            Func<Stream, Stream> decorate = null)
        {
            var name = "PowerToys.Workspaces.UnitTests." + Guid.NewGuid().ToString("N");
            Server = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 4096, 4096);
            var client = CreateClient(name);
            Connection = new LauncherConnection(
                launcherProcessId ?? Environment.ProcessId,
                client,
                openParent ?? OpenObservedProcess,
                1000,
                operationTimeoutMilliseconds,
                500,
                decorate?.Invoke(client));
            Connection.Closed += () =>
            {
                Interlocked.Increment(ref _closedCount);
                _closed.TrySetResult();
            };
        }

        internal NamedPipeServerStream Server { get; }

        internal LauncherConnection Connection { get; }

        internal int ClosedCount => Volatile.Read(ref _closedCount);

        internal Task Closed => _closed.Task.WaitAsync(Bound);

        internal static NamedPipeClientStream CreateClient(string name)
        {
            return new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous, TokenImpersonationLevel.Identification);
        }

        internal static LauncherProcessIdentity.ProcessObservation ValidObservation(int processId)
        {
            // These are observations, never files created at or copied to product installation paths.
            return new LauncherProcessIdentity.ProcessObservation(
                (uint)processId,
                @"C:\IPC-test-observation\PowerToys.WorkspacesLauncherUI.exe",
                @"C:\IPC-test-observation\PowerToys.WorkspacesLauncher.exe",
                (1, 2, 3, 4),
                (1, 2, 3, 4));
        }

        internal static LauncherProcessIdentity OpenObservedProcess(int processId)
        {
            return LauncherProcessIdentity.Open(processId, _ => ValidObservation(processId));
        }

        internal async Task ConnectAsync()
        {
            using var timeout = new CancellationTokenSource(Bound);
            var accept = Server.WaitForConnectionAsync(timeout.Token);
            try
            {
                await Connection.ConnectAsync().WaitAsync(Bound);
                Assert.IsTrue(Connection.IsOpen, "The observed identity and actual pipe peer should authenticate.");
                await accept;
            }
            finally
            {
                await timeout.CancelAsync();
                try
                {
                    await accept;
                }
                catch (OperationCanceledException)
                {
                }
            }
        }

        internal void StartReceiving()
        {
            Connection.StartReceiving(message => _messages.Writer.TryWrite(message));
        }

        internal Task<LauncherMessage> NextMessageAsync()
        {
            return _messages.Reader.ReadAsync().AsTask().WaitAsync(Bound);
        }

        internal async Task WriteAsync(ReadOnlyMemory<byte> bytes)
        {
            using var timeout = new CancellationTokenSource(Bound);
            await Server.WriteAsync(bytes, timeout.Token);
        }

        internal async Task<byte[]> ReadFrameAsync()
        {
            using var timeout = new CancellationTokenSource(Bound);
            var header = new byte[sizeof(uint)];
            await Server.ReadExactlyAsync(header, timeout.Token);
            var length = BinaryPrimitives.ReadUInt32LittleEndian(header);
            Assert.IsTrue(length > 0 && length <= 4 * 1024 * 1024);
            var frame = new byte[sizeof(uint) + length];
            header.CopyTo(frame, 0);
            await Server.ReadExactlyAsync(frame.AsMemory(sizeof(uint)), timeout.Token);
            return frame;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await Connection.DisposeAsync().AsTask().WaitAsync(Bound);
            }
            finally
            {
                await Server.DisposeAsync();
            }
        }
    }
}
