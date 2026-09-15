// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesLauncherUI.IPC;

namespace WorkspacesLauncherUI.UnitTests
{
    [TestClass]
    public sealed class LauncherConnectionTests
    {
        [TestMethod]
        public async Task SendsNothingBeforeAuthenticationAndUsesOnePersistentDuplexByteStream()
        {
            await using var session = new LauncherPipeTestSession();
            Assert.IsFalse(await session.Connection.SendReadyAsync());
            Assert.IsFalse(await session.Connection.SendResponseAsync(LauncherProtocolTestData.RequestId, true, default));
            Assert.ThrowsExactly<InvalidOperationException>(() => session.StartReceiving());
            await session.ConnectAsync();
            session.StartReceiving();
            Assert.ThrowsExactly<InvalidOperationException>(() => session.StartReceiving());

            var sends = new (string Type, string Choice, Func<Task<bool>> Send)[]
            {
                ("ready", null, session.Connection.SendReadyAsync),
                ("cancel", null, session.Connection.SendCancelAsync),
                ("warning-shown", null, () => session.Connection.SendWarningShownAsync(LauncherProtocolTestData.RequestId, default)),
                ("heartbeat", null, () => session.Connection.SendHeartbeatAsync(LauncherProtocolTestData.RequestId, default)),
                ("elevation-response", "run", () => session.Connection.SendResponseAsync(LauncherProtocolTestData.RequestId, true, default)),
                ("elevation-response", "skip", () => session.Connection.SendResponseAsync(LauncherProtocolTestData.RequestId, false, default)),
            };

            foreach (var send in sends)
            {
                Assert.IsTrue(await send.Send().WaitAsync(LauncherPipeTestSession.Bound));
                CollectionAssert.AreEqual(
                    LauncherProtocolTestData.Frame(LauncherProtocolTestData.ExpectedSend(send.Type, send.Choice)),
                    await session.ReadFrameAsync());
                await session.WriteAsync(LauncherProtocolTestData.Frame(LauncherProtocolTestData.Warning));
                Assert.AreEqual("elevation-warning", (await session.NextMessageAsync()).Type);
            }

            await session.WriteAsync(LauncherProtocolTestData.Frame(LauncherProtocolTestData.Shutdown));
            Assert.AreEqual("shutdown", (await session.NextMessageAsync()).Type);
            await session.Closed;
            Assert.IsFalse(session.Connection.IsOpen);
            Assert.IsFalse(session.Connection.HasFailed);
            Assert.IsFalse(await session.Connection.SendCancelAsync());
            Assert.AreEqual(1, session.ClosedCount);
        }

        [TestMethod]
        public async Task ReassemblesSplitHeaderAndBodyThenReadsCoalescedFrames()
        {
            ControlledPipeStream stream = null;
            await using var session = new LauncherPipeTestSession(decorate: pipe => stream = new ControlledPipeStream(pipe));
            await session.ConnectAsync();
            session.StartReceiving();
            var frame = LauncherProtocolTestData.Frame(LauncherProtocolTestData.Warning);
            foreach (var piece in new[] { frame[..1], frame[1..3], frame[3..4], frame[4..9], frame[9..] })
            {
                await session.WriteAsync(piece);
                Assert.AreEqual(piece.Length, await stream.Reads.Reader.ReadAsync().AsTask().WaitAsync(LauncherPipeTestSession.Bound));
            }

            Assert.AreEqual(LauncherProtocolTestData.RequestId, (await session.NextMessageAsync()).Warning.RequestId);
            await session.WriteAsync(LauncherProtocolTestData.Frame(LauncherProtocolTestData.Dismiss)
                .Concat(LauncherProtocolTestData.Frame(LauncherProtocolTestData.Shutdown)).ToArray());
            Assert.AreEqual("dismiss-warning", (await session.NextMessageAsync()).Type);
            Assert.AreEqual("shutdown", (await session.NextMessageAsync()).Type);
            await session.Closed;
            Assert.IsFalse(session.Connection.HasFailed);
        }

        [DataTestMethod]
        [DataRow(0L)]
        [DataRow(4194305L)]
        [DataRow(4294967295L)]
        public async Task RejectsOutOfRangeUnsignedLengthsWithoutWaitingForBody(long length)
        {
            await using var session = new LauncherPipeTestSession();
            await session.ConnectAsync();
            session.StartReceiving();
            var header = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(header, (uint)length);
            await session.WriteAsync(header);
            await AssertFailedAsync(session);
        }

        [TestMethod]
        public async Task AcceptsExactlyFourMiBBody()
        {
            await using var session = new LauncherPipeTestSession(operationTimeoutMilliseconds: 5000);
            await session.ConnectAsync();
            session.StartReceiving();
            var json = LauncherProtocolTestData.Shutdown.PadRight(4 * 1024 * 1024);
            await session.WriteAsync(LauncherProtocolTestData.Frame(json));
            Assert.AreEqual("shutdown", (await session.NextMessageAsync()).Type);
            await session.Closed;
            Assert.IsFalse(session.Connection.HasFailed);
        }

        [DataTestMethod]
        [DataRow(1)]
        [DataRow(3)]
        [DataRow(4)]
        [DataRow(8)]
        public async Task PartialHeaderOrBodyDeadlineClosesFailClosed(int bytesToSend)
        {
            await using var session = new LauncherPipeTestSession(operationTimeoutMilliseconds: 150);
            await session.ConnectAsync();
            session.StartReceiving();
            var frame = LauncherProtocolTestData.Frame(LauncherProtocolTestData.Warning);
            await session.WriteAsync(frame.AsMemory(0, bytesToSend));
            await AssertFailedAsync(session);
        }

        [TestMethod]
        public async Task IdleReadHasNoFrameDeadlineButDisposeCancelsIt()
        {
            await using var session = new LauncherPipeTestSession(operationTimeoutMilliseconds: 100);
            await session.ConnectAsync();
            session.StartReceiving();
            await Task.Delay(350);
            Assert.IsTrue(session.Connection.IsOpen);
            await session.WriteAsync(LauncherProtocolTestData.Frame(LauncherProtocolTestData.Warning));
            Assert.AreEqual("elevation-warning", (await session.NextMessageAsync()).Type);
            await Task.Delay(350);
            Assert.IsTrue(session.Connection.IsOpen, "Finishing a frame must restore the unbounded idle read.");
            Assert.IsFalse(session.Connection.HasFailed);
            await session.Connection.DisposeAsync().AsTask().WaitAsync(LauncherPipeTestSession.Bound);
            await session.Connection.DisposeAsync().AsTask().WaitAsync(LauncherPipeTestSession.Bound);
            Assert.AreEqual(1, session.ClosedCount);
            Assert.IsFalse(session.Connection.HasFailed);
        }

        [DataTestMethod]
        [DataRow("disconnect")]
        [DataRow("invalid-utf8")]
        [DataRow("invalid-json")]
        [DataRow("minimum-length")]
        [DataRow("unknown-type")]
        [DataRow("wrong-process")]
        public async Task InvalidIncomingBytesOrDisconnectFailClosed(string scenario)
        {
            await using var session = new LauncherPipeTestSession();
            await session.ConnectAsync();
            session.StartReceiving();
            if (scenario == "disconnect")
            {
                session.Server.Disconnect();
            }
            else
            {
                var body = scenario switch
                {
                    "invalid-utf8" => new byte[] { 0xff },
                    "invalid-json" => Encoding.UTF8.GetBytes("{bad"),
                    "minimum-length" => new byte[] { (byte)'0' },
                    "wrong-process" => Encoding.UTF8.GetBytes(LauncherProtocolTestData.LaunchStatus(0)),
                    _ => Encoding.UTF8.GetBytes("""{"protocolVersion":1,"type":"unknown"}"""),
                };
                await session.WriteAsync(LauncherProtocolTestData.Frame(body));
            }

            await AssertFailedAsync(session);
        }

        [TestMethod]
        public async Task PreCanceledSendDoesNotWriteOrPoisonAuthenticatedStream()
        {
            await using var session = new LauncherPipeTestSession();
            await session.ConnectAsync();
            using var cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();
            Assert.IsFalse(await session.Connection.SendResponseAsync(LauncherProtocolTestData.RequestId, true, cancellation.Token));
            Assert.IsTrue(await session.Connection.SendReadyAsync());
            CollectionAssert.AreEqual(LauncherProtocolTestData.Frame(LauncherProtocolTestData.ExpectedSend("ready")), await session.ReadFrameAsync());
            Assert.IsTrue(session.Connection.IsOpen);
            Assert.IsFalse(session.Connection.HasFailed);
        }

        [TestMethod]
        public async Task QueuedCanceledSendDoesNotWriteAndConcurrentFramesStaySerialized()
        {
            ControlledPipeStream stream = null;
            await using var session = new LauncherPipeTestSession(decorate: pipe => stream = new ControlledPipeStream(pipe) { GateWriteCall = 2 });
            await session.ConnectAsync();
            var first = session.Connection.SendReadyAsync();
            try
            {
                await stream.Entered.Task.WaitAsync(LauncherPipeTestSession.Bound);
                using var cancellation = new CancellationTokenSource();
                var canceled = session.Connection.SendResponseAsync(LauncherProtocolTestData.RequestId, true, cancellation.Token);
                var others = Enumerable.Range(0, 12).Select(_ => session.Connection.SendHeartbeatAsync(LauncherProtocolTestData.RequestId, default)).ToArray();
                await cancellation.CancelAsync();
                Assert.IsFalse(await canceled.WaitAsync(LauncherPipeTestSession.Bound));
                stream.Release.TrySetResult();
                Assert.IsTrue(await first.WaitAsync(LauncherPipeTestSession.Bound));
                CollectionAssert.AreEqual(LauncherProtocolTestData.Frame(LauncherProtocolTestData.ExpectedSend("ready")), await session.ReadFrameAsync());
                foreach (var send in others)
                {
                    Assert.IsTrue(await send.WaitAsync(LauncherPipeTestSession.Bound));
                    CollectionAssert.AreEqual(LauncherProtocolTestData.Frame(LauncherProtocolTestData.ExpectedSend("heartbeat")), await session.ReadFrameAsync());
                }

                Assert.IsTrue(session.Connection.IsOpen);
                Assert.IsFalse(session.Connection.HasFailed);
            }
            finally
            {
                stream.Release.TrySetResult();
            }
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task CancellationOrDeadlineAfterPartialWritePoisonsStream(bool deadline)
        {
            ControlledPipeStream stream = null;
            await using var session = new LauncherPipeTestSession(
                operationTimeoutMilliseconds: deadline ? 300 : 2000,
                decorate: pipe => stream = new ControlledPipeStream(pipe) { GateWriteCall = 2, BodyPrefixLength = 3 });
            await session.ConnectAsync();
            using var cancellation = new CancellationTokenSource();
            var send = session.Connection.SendResponseAsync(LauncherProtocolTestData.RequestId, true, cancellation.Token);
            try
            {
                await stream.Entered.Task.WaitAsync(LauncherPipeTestSession.Bound);
                var expected = LauncherProtocolTestData.Frame(LauncherProtocolTestData.ExpectedSend("elevation-response", "run"));
                var prefix = new byte[7];
                using var timeout = new CancellationTokenSource(LauncherPipeTestSession.Bound);
                await session.Server.ReadExactlyAsync(prefix, timeout.Token);
                CollectionAssert.AreEqual(expected[..7], prefix);
                if (!deadline)
                {
                    await cancellation.CancelAsync();
                }

                Assert.IsFalse(await send.WaitAsync(LauncherPipeTestSession.Bound));
                await AssertFailedAsync(session);
                Assert.AreEqual(0, await session.Server.ReadAsync(new byte[1], timeout.Token));
            }
            finally
            {
                stream.Release.TrySetResult();
            }
        }

        [TestMethod]
        public async Task CloseDuringInflightAndQueuedSendsDrainsBeforeRepeatSafeDispose()
        {
            ControlledPipeStream stream = null;
            await using var session = new LauncherPipeTestSession(
                decorate: pipe => stream = new ControlledPipeStream(pipe) { GateWriteCall = 2, DelayCancellation = true });
            await session.ConnectAsync();
            var inflight = session.Connection.SendReadyAsync();
            try
            {
                await stream.Entered.Task.WaitAsync(LauncherPipeTestSession.Bound);
                using var timeout = new CancellationTokenSource(LauncherPipeTestSession.Bound);
                var header = new byte[4];
                await session.Server.ReadExactlyAsync(header, timeout.Token);
                CollectionAssert.AreEqual(LauncherProtocolTestData.Frame(LauncherProtocolTestData.ExpectedSend("ready"))[..4], header);
                var queued = session.Connection.SendCancelAsync();
                session.Connection.Close();
                var disposal = session.Connection.DisposeAsync().AsTask();
                var repeated = session.Connection.DisposeAsync().AsTask();
                await stream.Canceled.Task.WaitAsync(LauncherPipeTestSession.Bound);
                Assert.IsFalse(disposal.IsCompleted, "Dispose must retain resources until the in-flight send unwinds.");
                Assert.IsFalse(repeated.IsCompleted);
                Assert.IsFalse(await session.Connection.SendReadyAsync());
                stream.Unwind.TrySetResult();
                Assert.IsFalse(await inflight.WaitAsync(LauncherPipeTestSession.Bound));
                Assert.IsFalse(await queued.WaitAsync(LauncherPipeTestSession.Bound));
                await Task.WhenAll(disposal, repeated).WaitAsync(LauncherPipeTestSession.Bound);
                Assert.AreEqual(0, await session.Server.ReadAsync(new byte[1], timeout.Token));
                Assert.AreEqual(1, session.ClosedCount);
                Assert.IsFalse(session.Connection.HasFailed, "Deliberate Close wins over subsequent canceled-send failures.");
            }
            finally
            {
                stream.Unwind.TrySetResult();
                stream.Release.TrySetResult();
            }
        }

        [TestMethod]
        public async Task CloseWhileConnectingCancelsAndDrainsWithoutAuthentication()
        {
            using var client = LauncherPipeTestSession.CreateClient("PowerToys.Workspaces.UnitTests." + Guid.NewGuid().ToString("N"));
            await using var connection = new LauncherConnection(Environment.ProcessId, client, LauncherPipeTestSession.OpenObservedProcess, 2000, 100, 100);
            var connecting = connection.ConnectAsync();
            connection.Close();
            await Task.WhenAll(connecting, connection.DisposeAsync().AsTask()).WaitAsync(LauncherPipeTestSession.Bound);
            Assert.IsFalse(connection.IsOpen);
            Assert.IsFalse(connection.HasFailed);
            Assert.IsFalse(await connection.SendReadyAsync());
        }

        [TestMethod]
        public async Task MissingServerConnectDeadlineFailsClosed()
        {
            using var client = LauncherPipeTestSession.CreateClient("PowerToys.Workspaces.UnitTests." + Guid.NewGuid().ToString("N"));
            await using var connection = new LauncherConnection(Environment.ProcessId, client, LauncherPipeTestSession.OpenObservedProcess, 100, 100, 100);
            await connection.ConnectAsync().WaitAsync(LauncherPipeTestSession.Bound);
            Assert.IsFalse(connection.IsOpen);
            Assert.IsTrue(connection.HasFailed);
        }

        [TestMethod]
        public async Task BrokenPipeWriteFailsClosedWithoutAReceiveLoop()
        {
            await using var session = new LauncherPipeTestSession();
            await session.ConnectAsync();
            session.Server.Disconnect();
            Assert.IsFalse(await session.Connection.SendReadyAsync().WaitAsync(LauncherPipeTestSession.Bound));
            await AssertFailedAsync(session);
        }

        [TestMethod]
        public async Task DisposeWaitsForInProgressParentObservation()
        {
            using var entered = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            using var client = LauncherPipeTestSession.CreateClient("PowerToys.Workspaces.UnitTests." + Guid.NewGuid().ToString("N"));
            await using var connection = new LauncherConnection(
                Environment.ProcessId,
                client,
                pid =>
                {
                    entered.Set();
                    Assert.IsTrue(release.Wait(LauncherPipeTestSession.Bound));
                    return LauncherPipeTestSession.OpenObservedProcess(pid);
                },
                1000,
                1000,
                500);
            var connecting = connection.ConnectAsync();
            try
            {
                Assert.IsTrue(entered.Wait(LauncherPipeTestSession.Bound));
                Assert.IsFalse(await connection.SendReadyAsync());
                var disposal = connection.DisposeAsync().AsTask();
                Assert.IsFalse(disposal.IsCompleted);
                release.Set();
                await Task.WhenAll(connecting, disposal).WaitAsync(LauncherPipeTestSession.Bound);
                Assert.IsFalse(connection.IsOpen);
                Assert.IsFalse(connection.HasFailed);
            }
            finally
            {
                release.Set();
            }
        }

        private static async Task AssertFailedAsync(LauncherPipeTestSession session)
        {
            await session.Closed;
            Assert.IsFalse(session.Connection.IsOpen);
            Assert.IsTrue(session.Connection.HasFailed);
            Assert.IsFalse(await session.Connection.SendReadyAsync());
            Assert.AreEqual(1, session.ClosedCount);
        }
    }
}
