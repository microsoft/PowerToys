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
            var stopwatch = Stopwatch.StartNew();
            var disposeTask = Task.Run(host.Dispose);

            // Let the body write proceed so the writer can observe the disposal and release the lock.
            gated.ReleaseBody();

            await disposeTask.WaitAsync(cts.Token);
            stopwatch.Stop();

            Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(10), "Dispose during an active write must not block on the write indefinitely.");

            Exception? sendException = null;
            try
            {
                await sendTask.WaitAsync(cts.Token);
            }
            catch (Exception ex)
            {
                sendException = ex;
            }

            // Disposal abandons the in-flight frame and tears the connection down, so the send either
            // completes or fails with a transport error, but must never surface ObjectDisposedException.
            Assert.IsFalse(sendException is ObjectDisposedException, "Dispose during an active write must not surface ObjectDisposedException.");
            Assert.IsTrue(sendException is null or JsonRpcException, $"An abandoned write must fail with a transport error, not '{sendException?.GetType().Name}'.");
        }
        finally
        {
            host.Dispose();
            input.Writer.Complete();
            outputPipe.Writer.Complete();
        }
    }

    [TestMethod]
    public async Task WriteThatNeverDrains_TimesOut_AndClosesConnection()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var input = new Pipe();
        var stuck = new BlockingWriteStream();
        var host = new JsonRpcConnection(
            input.Reader.AsStream(),
            stuck,
            errorStream: null,
            requestTimeout: TimeSpan.FromSeconds(30),
            writeTimeout: TimeSpan.FromMilliseconds(500));

        var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.Disconnected += (_, _) => disconnected.TrySetResult();
        host.StartListening();

        try
        {
            var stopwatch = Stopwatch.StartNew();

            // The child never drains stdin, so the frame write blocks. It must be abandoned at the
            // write timeout instead of hanging forever.
            await Assert.ThrowsExceptionAsync<JsonRpcException>(async () =>
                await host.SendNotificationAsync("stuck", null, cts.Token));
            stopwatch.Stop();

            Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(10), "A write that never drains must be abandoned at the write timeout, not hang.");

            // The stalled write tears the connection down so the owner can terminate the child.
            await disconnected.Task.WaitAsync(cts.Token);
        }
        finally
        {
            host.Dispose();
            input.Writer.Complete();
        }
    }

    [TestMethod]
    public async Task Dispose_WhileWriteNeverDrains_CompletesAndFailsTheWrite()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var input = new Pipe();
        var stuck = new BlockingWriteStream();

        // A long write timeout guarantees it is disposal, not the timeout, that unblocks the write.
        var host = new JsonRpcConnection(
            input.Reader.AsStream(),
            stuck,
            errorStream: null,
            requestTimeout: TimeSpan.FromSeconds(30),
            writeTimeout: TimeSpan.FromSeconds(30));
        host.StartListening();

        var sendTask = host.SendNotificationAsync("stuck", null, cts.Token);

        try
        {
            // Wait until the frame write has begun and is blocked on the never-draining stream.
            await stuck.WriteStarted.WaitAsync(cts.Token);

            var stopwatch = Stopwatch.StartNew();
            var disposeTask = Task.Run(host.Dispose);
            await disposeTask.WaitAsync(cts.Token);
            stopwatch.Stop();

            Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(10), "Dispose must abandon a stuck write rather than block on it indefinitely.");

            // Disposal cancels the in-flight write, which fails rather than hanging forever.
            await Assert.ThrowsExceptionAsync<JsonRpcException>(async () => await sendTask.WaitAsync(cts.Token));
        }
        finally
        {
            input.Writer.Complete();
        }
    }

    [TestMethod]
    public void TruncateForLog_BoundsOversizedPayloads()
    {
        var oversized = new string('x', 5_000_000);
        var truncated = JsonRpcConnection.TruncateForLog(oversized);

        Assert.IsTrue(truncated.Length < oversized.Length, "An oversized payload must be shortened before logging.");
        Assert.IsTrue(truncated.Length <= JsonRpcConnection.MaxLoggedBodyChars + 128, "The truncated payload must stay near the logging cap.");
        StringAssert.Contains(truncated, "truncated");

        const string small = "{\"jsonrpc\":\"2.0\"}";
        Assert.AreEqual(small, JsonRpcConnection.TruncateForLog(small), "A payload under the cap must be logged verbatim.");
    }

    [TestMethod]
    public async Task OversizedMalformedBody_IsTruncatedInLogPath_AndRaisesError()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var harness = CreateHarness();

        var errorRaised = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Host.Error += (_, e) => errorRaised.TrySetResult(e.Exception);

        try
        {
            // A large, validly framed but unparseable body. It exercises the truncated log path and
            // must be reported through the Error event without hanging.
            var malformed = "{" + new string('a', 2_000_000);
            await WriteFramedAsync(harness.ExtensionWrites, malformed, cts.Token);

            var exception = await errorRaised.Task.WaitAsync(cts.Token);
            Assert.IsInstanceOfType(exception, typeof(JsonException));
        }
        finally
        {
            harness.Host.Dispose();
        }
    }

    [TestMethod]
    public async Task InboundRequests_AreConcurrencyBounded_AndDisposeCompletes()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var harness = CreateHarness(TimeSpan.FromSeconds(30));

        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var saturated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = 0;
        var maxObserved = 0;

        harness.Host.RegisterRequestHandler("block", async (_, token) =>
        {
            var current = Interlocked.Increment(ref running);

            int observed;
            do
            {
                observed = Volatile.Read(ref maxObserved);
            }
            while (current > observed && Interlocked.CompareExchange(ref maxObserved, current, observed) != observed);

            if (current >= JsonRpcConnection.InboundRequestWorkerCount)
            {
                saturated.TrySetResult();
            }

            try
            {
                await release.Task.WaitAsync(token).ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Decrement(ref running);
            }

            return new JsonObject { ["ok"] = true };
        });

        try
        {
            // Flood the host with far more inbound requests than there are workers.
            var total = JsonRpcConnection.InboundRequestWorkerCount * 2;
            for (var i = 0; i < total; i++)
            {
                await WriteFramedAsync(harness.ExtensionWrites, BuildRequest(i + 1, "block", null), cts.Token);
            }

            // Wait until every worker is busy, then give any excess a chance to (wrongly) start.
            await saturated.Task.WaitAsync(cts.Token);
            await Task.Delay(250, cts.Token);

            Assert.IsTrue(
                Volatile.Read(ref maxObserved) <= JsonRpcConnection.InboundRequestWorkerCount,
                $"Inbound request concurrency {Volatile.Read(ref maxObserved)} exceeded the bound of {JsonRpcConnection.InboundRequestWorkerCount}.");
        }
        finally
        {
            // Release the handlers so the workers drain, then confirm disposal completes cleanly.
            release.TrySetResult();
            harness.Host.Dispose();
        }
    }

    [TestMethod]
    public async Task InFlightHandler_AwaitingResponse_CompletesWhileRequestQueueSaturated()
    {
        // Regression for the reader-admission deadlock: a handler that is awaiting an RPC response must
        // still complete when the inbound request queue is saturated, because the reader routes response
        // frames without ever blocking on work-queue admission.
        using var cts = new CancellationTokenSource(TestTimeout);
        var harness = CreateHarness(TimeSpan.FromSeconds(30));

        var blockGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // "reflect" issues an outbound request and awaits the peer's response before returning. Its
        // completion depends on the reader continuing to route response frames.
        harness.Host.RegisterRequestHandler("reflect", async (_, token) =>
        {
            var response = await harness.Host.SendRequestAsync("callback", null, token).ConfigureAwait(false);
            return new JsonObject { ["ok"] = response.Error is null };
        });

        // "block" never returns until released, so it pins a worker and helps saturate the queue.
        harness.Host.RegisterRequestHandler("block", async (_, token) =>
        {
            await blockGate.Task.WaitAsync(token).ConfigureAwait(false);
            return new JsonObject { ["ok"] = true };
        });

        try
        {
            // Start the in-flight handler and wait until it has issued its outbound request.
            await WriteFramedAsync(harness.ExtensionWrites, BuildRequest(1, "reflect", null), cts.Token);

            var callbackId = -1;
            while (true)
            {
                var (_, body) = await ReadFramedAsync(harness.ExtensionReads, cts.Token);
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("method", out var method) &&
                    string.Equals(method.GetString(), "callback", StringComparison.Ordinal))
                {
                    callbackId = doc.RootElement.GetProperty("id").GetInt32();
                    break;
                }
            }

            // Saturate the inbound request path far beyond the worker count and queue capacity. A reader
            // that blocked on admission (the pre-fix behavior) could no longer route the response below.
            for (var i = 0; i < 600; i++)
            {
                await WriteFramedAsync(harness.ExtensionWrites, BuildRequest(1000 + i, "block", null), cts.Token);
            }

            // Answer the in-flight handler's outbound request. The reader must route this response even
            // though the request queue is saturated; otherwise "reflect" never completes (deadlock).
            await RespondWithResultAsync(harness.ExtensionWrites, callbackId, new JsonObject { ["pong"] = true }, cts.Token);

            using var reflectResponse = await ReadResponseWithIdAsync(harness.ExtensionReads, 1, cts.Token);
            Assert.IsTrue(
                reflectResponse.RootElement.GetProperty("result").GetProperty("ok").GetBoolean(),
                "The in-flight handler did not observe its correlated response while the queue was saturated.");
        }
        finally
        {
            blockGate.TrySetResult();
            harness.Host.Dispose();
        }
    }

    [TestMethod]
    public async Task InboundRequest_ExceedingByteBudget_IsRejectedWithServerBusy()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var toHost = new Pipe();
        var fromHost = new Pipe();

        // A tiny request byte budget so the first inbound request's frame already exceeds it and the
        // reader rejects it rather than admitting a payload that would blow the aggregate budget.
        var host = new JsonRpcConnection(
            toHost.Reader.AsStream(),
            fromHost.Writer.AsStream(),
            errorStream: null,
            requestTimeout: TimeSpan.FromSeconds(30),
            writeTimeout: null,
            maxQueuedNotificationBytes: null,
            maxQueuedRequestBytes: 4);

        var handlerInvoked = false;
        host.RegisterRequestHandler("work", (_, _) =>
        {
            handlerInvoked = true;
            return Task.FromResult<JsonNode?>(new JsonObject { ["ok"] = true });
        });
        host.StartListening();

        var extensionReads = fromHost.Reader.AsStream();
        var extensionWrites = toHost.Writer.AsStream();

        try
        {
            await WriteFramedAsync(extensionWrites, BuildRequest(7, "work", null), cts.Token);

            using var response = await ReadResponseWithIdAsync(extensionReads, 7, cts.Token);
            Assert.AreEqual(
                JsonRpcError.ServerBusy,
                response.RootElement.GetProperty("error").GetProperty("code").GetInt32(),
                "A request exceeding the aggregate byte budget must be rejected with a server-busy error.");
            Assert.IsFalse(handlerInvoked, "The handler must not run for a request rejected by the byte budget.");
        }
        finally
        {
            host.Dispose();
        }
    }

    [TestMethod]
    public async Task InboundNotification_ExceedingByteBudget_IsDropped()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var toHost = new Pipe();
        var fromHost = new Pipe();

        // A tiny notification byte budget so the notification's frame exceeds it and is dropped.
        var host = new JsonRpcConnection(
            toHost.Reader.AsStream(),
            fromHost.Writer.AsStream(),
            errorStream: null,
            requestTimeout: TimeSpan.FromSeconds(30),
            writeTimeout: null,
            maxQueuedNotificationBytes: 4,
            maxQueuedRequestBytes: null);

        var notificationHandled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // The handler must be registered so the notification reaches the byte-budget drop path rather
        // than being ignored as unrecognized.
        host.RegisterNotificationHandler("note", _ => notificationHandled.TrySetResult());
        host.RegisterRequestHandler("ping", (_, _) => Task.FromResult<JsonNode?>(new JsonObject { ["ok"] = true }));
        host.StartListening();

        var extensionReads = fromHost.Reader.AsStream();
        var extensionWrites = toHost.Writer.AsStream();

        try
        {
            await WriteFramedAsync(extensionWrites, BuildNotification("note", new JsonObject { ["payload"] = "abcdefghij" }), cts.Token);

            // Frames are processed in order, so once the ping response arrives the notification ahead of
            // it has already been dropped by the reader.
            await WriteFramedAsync(extensionWrites, BuildRequest(1, "ping", null), cts.Token);
            using var pong = await ReadResponseWithIdAsync(extensionReads, 1, cts.Token);
            Assert.IsTrue(pong.RootElement.GetProperty("result").GetProperty("ok").GetBoolean());

            Assert.IsTrue(
                host.DroppedNotificationCount >= 1,
                "A notification exceeding the aggregate byte budget must be dropped.");
            Assert.IsFalse(
                notificationHandled.Task.IsCompleted,
                "A dropped notification must never reach its handler.");
        }
        finally
        {
            host.Dispose();
        }
    }

    [TestMethod]
    public async Task Dispose_WithManyBlockedWorkers_CompletesWithinSingleAggregateDeadline()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var harness = CreateHarness(TimeSpan.FromSeconds(30));

        var started = 0;
        var allStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var neverRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // The handler deliberately ignores its cancellation token and blocks forever, forcing disposal
        // to fall back on its drain deadline for every worker at once.
        harness.Host.RegisterRequestHandler("hang", async (_, _) =>
        {
            if (Interlocked.Increment(ref started) >= JsonRpcConnection.InboundRequestWorkerCount)
            {
                allStarted.TrySetResult();
            }

            await neverRelease.Task.ConfigureAwait(false);
            return new JsonObject();
        });

        try
        {
            for (var i = 0; i < JsonRpcConnection.InboundRequestWorkerCount; i++)
            {
                await WriteFramedAsync(harness.ExtensionWrites, BuildRequest(i + 1, "hang", null), cts.Token);
            }

            await allStarted.Task.WaitAsync(cts.Token);

            var stopwatch = Stopwatch.StartNew();
            harness.Host.Dispose();
            stopwatch.Stop();

            // A single aggregate drain budget governs disposal, so even with every worker stuck the wait
            // is bounded by one deadline rather than one timeout per worker. Generous headroom keeps the
            // assertion stable on busy CI while still failing the per-worker (~32 second) regression.
            Assert.IsTrue(
                stopwatch.Elapsed < TimeSpan.FromSeconds(10),
                $"Dispose took {stopwatch.Elapsed.TotalSeconds:F1}s; the aggregate drain deadline was not applied.");
        }
        finally
        {
            neverRelease.TrySetResult();
        }
    }

    private static async Task<JsonDocument> ReadResponseWithIdAsync(Stream stream, int id, CancellationToken cancellationToken)
    {
        while (true)
        {
            var (_, body) = await ReadFramedAsync(stream, cancellationToken);
            var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            // A response carries an id but no method. Skip outbound requests/notifications and responses
            // correlated to other ids (for example server-busy rejections for the saturating flood).
            if (!root.TryGetProperty("method", out _) &&
                root.TryGetProperty("id", out var idElement) &&
                idElement.ValueKind == JsonValueKind.Number &&
                idElement.GetInt32() == id)
            {
                return document;
            }

            document.Dispose();
        }
    }

    private sealed class BlockingWriteStream : Stream
    {
        private readonly TaskCompletionSource _writeStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task WriteStarted => _writeStarted.Task;

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            _writeStarted.TrySetResult();

            // Never accept the bytes, but honor cancellation the way a real cancellable stream does,
            // so the write timeout and disposal can abandon the write.
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }

        public override Task FlushAsync(CancellationToken cancellationToken) => Task.Delay(Timeout.Infinite, cancellationToken);

        public override void Flush()
        {
        }

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
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
