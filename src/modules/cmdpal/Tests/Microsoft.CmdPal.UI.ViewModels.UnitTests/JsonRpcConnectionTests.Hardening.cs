// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Services.JsonRpc;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

/// <summary>
/// Tests for the phase-2 transport hardening: partial-write framing, reader-exit terminal state,
/// notification decoupling, and disposal safety.
/// </summary>
public partial class JsonRpcConnectionTests
{
    [TestMethod]
    public async Task WriteFailure_ClosesConnection_AndFailsFutureSends()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var input = new Pipe();
        var host = new JsonRpcConnection(input.Reader.AsStream(), new ThrowingWriteStream(), errorStream: null, requestTimeout: TimeSpan.FromSeconds(30));

        var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.Disconnected += (_, _) => disconnected.TrySetResult();
        host.StartListening();

        try
        {
            await Assert.ThrowsExceptionAsync<JsonRpcException>(async () =>
                await host.SendNotificationAsync("boom", null, cts.Token));

            await disconnected.Task.WaitAsync(cts.Token);

            // A partial or failed frame makes the connection terminal: later sends fail fast.
            await Assert.ThrowsExceptionAsync<JsonRpcException>(async () =>
                await host.SendRequestAsync("later", null, cts.Token));
        }
        finally
        {
            host.Dispose();
            input.Writer.Complete();
        }
    }

    [TestMethod]
    public async Task CancellationDuringFrameEmission_DoesNotCorruptStream()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var input = new Pipe();
        var outputPipe = new Pipe();
        var gated = new GatedWriteStream(outputPipe.Writer.AsStream());
        var host = new JsonRpcConnection(input.Reader.AsStream(), gated, errorStream: null, requestTimeout: TimeSpan.FromSeconds(30));
        host.StartListening();

        using var callerCts = new CancellationTokenSource();
        try
        {
            var sendTask = host.SendNotificationAsync("emit", new JsonObject { ["k"] = "v" }, callerCts.Token);

            // Cancel the caller token after the header is written but before the body.
            await gated.AfterFirstWrite.WaitAsync(cts.Token);
            callerCts.Cancel();
            gated.ReleaseBody();

            // Emission is not cancellable once started, so the write completes.
            await sendTask.WaitAsync(cts.Token);

            // The whole, valid frame is intact on the wire.
            var (_, body) = await ReadFramedAsync(outputPipe.Reader.AsStream(), cts.Token);
            using var document = JsonDocument.Parse(body);
            Assert.AreEqual("emit", document.RootElement.GetProperty("method").GetString());
            Assert.AreEqual("v", document.RootElement.GetProperty("params").GetProperty("k").GetString());
        }
        finally
        {
            host.Dispose();
            input.Writer.Complete();
            outputPipe.Writer.Complete();
        }
    }

    [TestMethod]
    public async Task SendAfterReaderEof_FailsFast()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var harness = CreateHarness(TimeSpan.FromSeconds(30));

        var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Host.Disconnected += (_, _) => disconnected.TrySetResult();

        try
        {
            // Signal EOF to the host's read loop by completing the extension's output stream.
            harness.ExtensionWrites.Dispose();
            await disconnected.Task.WaitAsync(cts.Token);

            var stopwatch = Stopwatch.StartNew();
            await Assert.ThrowsExceptionAsync<JsonRpcException>(async () =>
                await harness.Host.SendRequestAsync("after-eof", null, cts.Token));
            stopwatch.Stop();

            Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(5), "A send after EOF must fail fast rather than wait for the request timeout.");
        }
        finally
        {
            harness.Host.Dispose();
        }
    }

    [TestMethod]
    public async Task PendingRequest_FailsPromptly_WhenReaderReachesEof()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var harness = CreateHarness(TimeSpan.FromSeconds(30));

        try
        {
            var requestTask = harness.Host.SendRequestAsync("inflight", null, cts.Token);

            // Ensure the request has been written before the reader reaches EOF.
            _ = await ReadFramedAsync(harness.ExtensionReads, cts.Token);

            harness.ExtensionWrites.Dispose();

            await Assert.ThrowsExceptionAsync<JsonRpcException>(async () => await requestTask.WaitAsync(cts.Token));
        }
        finally
        {
            harness.Host.Dispose();
        }
    }

    [TestMethod]
    public async Task SlowNotificationHandler_DoesNotBlockFrameReading()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var harness = CreateHarness();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            harness.Host.RegisterNotificationHandler("slow", _ => release.Task.Wait(cts.Token));
            harness.Host.RegisterRequestHandler("ping", (_, _) =>
                Task.FromResult<JsonNode?>(new JsonObject { ["pong"] = true }));

            await WriteFramedAsync(harness.ExtensionWrites, BuildNotification("slow", null), cts.Token);
            await WriteFramedAsync(harness.ExtensionWrites, BuildRequest(1, "ping", null), cts.Token);

            // The blocked notification handler runs on the consumer task, so the reader still
            // dispatches the inbound request and writes its response.
            var (_, body) = await ReadFramedAsync(harness.ExtensionReads, cts.Token);
            using var document = JsonDocument.Parse(body);
            Assert.AreEqual(1, document.RootElement.GetProperty("id").GetInt32());
            Assert.IsTrue(document.RootElement.GetProperty("result").GetProperty("pong").GetBoolean());
        }
        finally
        {
            release.TrySetResult();
            harness.Host.Dispose();
        }
    }

    [TestMethod]
    public async Task ReentrantNotificationHandler_SendingRequest_DoesNotDeadlock()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var harness = CreateHarness();
        var done = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            harness.Host.RegisterNotificationHandler("reenter", _ =>
            {
                try
                {
                    var response = harness.Host
                        .SendRequestAsync("callback", null, JsonRpcTestJsonContext.Default.TestPayload, cts.Token)
                        .GetAwaiter()
                        .GetResult();
                    done.TrySetResult(response?.Message);
                }
                catch (Exception ex)
                {
                    done.TrySetException(ex);
                }
            });

            await WriteFramedAsync(harness.ExtensionWrites, BuildNotification("reenter", null), cts.Token);

            // The handler synchronously sends a request; the reader (independent of the consumer)
            // must still be able to read and correlate that request's response.
            var (_, body) = await ReadFramedAsync(harness.ExtensionReads, cts.Token);
            using var document = JsonDocument.Parse(body);
            Assert.AreEqual("callback", document.RootElement.GetProperty("method").GetString());
            var id = document.RootElement.GetProperty("id").GetInt32();

            await RespondWithResultAsync(harness.ExtensionWrites, id, new JsonObject { ["message"] = "ok" }, cts.Token);

            var message = await done.Task.WaitAsync(cts.Token);
            Assert.AreEqual("ok", message);
        }
        finally
        {
            harness.Host.Dispose();
        }
    }

    [TestMethod]
    public async Task Dispose_DuringActiveWrite_DoesNotThrowObjectDisposed()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var input = new Pipe();
        var outputPipe = new Pipe();
        var gated = new GatedWriteStream(outputPipe.Writer.AsStream());
        var host = new JsonRpcConnection(input.Reader.AsStream(), gated, errorStream: null, requestTimeout: TimeSpan.FromSeconds(30));
        host.StartListening();

        try
        {
            var sendTask = host.SendNotificationAsync("during-dispose", null, cts.Token);
            await gated.AfterFirstWrite.WaitAsync(cts.Token);

            // Dispose concurrently while the writer is blocked mid-frame.
            var disposeTask = Task.Run(host.Dispose);

            // Let the body finish so the writer releases the lock and Dispose can drain.
            gated.ReleaseBody();

            await disposeTask.WaitAsync(cts.Token);

            Exception? sendException = null;
            try
            {
                await sendTask.WaitAsync(cts.Token);
            }
            catch (Exception ex)
            {
                sendException = ex;
            }

            Assert.IsFalse(sendException is ObjectDisposedException, "Dispose during an active write must not surface ObjectDisposedException.");

            // The frame that was mid-flight is intact, not a corrupt partial frame.
            var (_, body) = await ReadFramedAsync(outputPipe.Reader.AsStream(), cts.Token);
            using var document = JsonDocument.Parse(body);
            Assert.AreEqual("during-dispose", document.RootElement.GetProperty("method").GetString());
        }
        finally
        {
            host.Dispose();
            input.Writer.Complete();
            outputPipe.Writer.Complete();
        }
    }

    private sealed class ThrowingWriteStream : Stream
    {
        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => throw new IOException("Simulated write failure.");

        public override Task FlushAsync(CancellationToken cancellationToken) => throw new IOException("Simulated flush failure.");

        public override void Flush() => throw new IOException("Simulated flush failure.");

        public override void Write(byte[] buffer, int offset, int count) => throw new IOException("Simulated write failure.");

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
    }

    private sealed class GatedWriteStream : Stream
    {
        private readonly Stream _inner;
        private readonly TaskCompletionSource _afterFirstWrite = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _bodyGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _writeCount;

        public GatedWriteStream(Stream inner) => _inner = inner;

        public Task AfterFirstWrite => _afterFirstWrite.Task;

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public void ReleaseBody() => _bodyGate.TrySetResult();

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var index = Interlocked.Increment(ref _writeCount);
            if (index == 2)
            {
                // The body write blocks until the test releases it, simulating a slow mid-frame write.
                await _bodyGate.Task.ConfigureAwait(false);
            }

            await _inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);

            if (index == 1)
            {
                _afterFirstWrite.TrySetResult();
            }
        }

        public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);

        public override void Flush() => _inner.Flush();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
