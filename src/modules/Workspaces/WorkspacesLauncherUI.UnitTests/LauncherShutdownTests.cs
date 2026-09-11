// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesLauncherUI.IPC;

namespace WorkspacesLauncherUI.UnitTests
{
    [TestClass]
    public sealed class LauncherShutdownTests
    {
        [TestMethod]
        public async Task DuplicatedServerHandleCannotKeepOrphanedConnectionAlive()
        {
            await using var child = await LauncherPipeChild.StartAsync();
            using var client = LauncherPipeTestSession.CreateClient(child.PipeName);
            await using var connection = new LauncherConnection(child.ProcessId, client, LauncherPipeTestSession.OpenObservedProcess, 2000, 2000, 150);
            var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var unexpectedMessage = new TaskCompletionSource<LauncherMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            connection.Closed += () => closed.TrySetResult();
            await connection.ConnectAsync().WaitAsync(LauncherPipeTestSession.Bound);
            await child.WaitForConnectionAsync();
            Assert.IsTrue(connection.IsOpen);
            connection.StartReceiving(message => unexpectedMessage.TrySetResult(message));
            using var retainedServer = await child.DuplicateServerAsync();
            var readyFrame = child.ReadReadyFrameAsync();
            Assert.IsTrue(await connection.SendReadyAsync().WaitAsync(LauncherPipeTestSession.Bound));
            var expected = LauncherProtocolTestData.Frame(LauncherProtocolTestData.ExpectedSend("ready"));
            CollectionAssert.AreEqual(expected, await readyFrame);
            await child.ExitAsync();
            await closed.Task.WaitAsync(LauncherPipeTestSession.Bound);
            Assert.IsTrue(connection.HasFailed);
            Assert.IsFalse(connection.IsOpen);
            Assert.IsFalse(unexpectedMessage.Task.IsCompleted);
            Assert.IsFalse(retainedServer.IsClosed);
            Assert.IsFalse(await connection.SendCancelAsync());
            await connection.DisposeAsync().AsTask().WaitAsync(LauncherPipeTestSession.Bound);
        }

        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task ParentExitDrainsBufferedShutdownButHasBoundedGrace(bool releaseBufferedFrame)
        {
            await using var child = await LauncherPipeChild.StartAsync();
            using var client = LauncherPipeTestSession.CreateClient(child.PipeName);
            using var stream = new ControlledPipeStream(client) { GateReadCall = 3 };
            await using var connection = new LauncherConnection(child.ProcessId, client, LauncherPipeTestSession.OpenObservedProcess, 2000, 5000, releaseBufferedFrame ? 2000 : 150, stream);
            var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var message = new TaskCompletionSource<LauncherMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            connection.Closed += () => closed.TrySetResult();
            await connection.ConnectAsync().WaitAsync(LauncherPipeTestSession.Bound);
            await child.WaitForConnectionAsync();
            Assert.IsTrue(connection.IsOpen);
            connection.StartReceiving(received => message.TrySetResult(received));
            try
            {
                await child.SendShutdownAsync();
                await stream.Entered.Task.WaitAsync(LauncherPipeTestSession.Bound);
                await child.ExitAsync();
                if (releaseBufferedFrame)
                {
                    stream.Release.TrySetResult();
                    Assert.AreEqual("shutdown", (await message.Task.WaitAsync(LauncherPipeTestSession.Bound)).Type);
                }

                await closed.Task.WaitAsync(LauncherPipeTestSession.Bound);
                Assert.IsFalse(connection.IsOpen);
                Assert.AreEqual(!releaseBufferedFrame, connection.HasFailed);
                Assert.AreEqual(releaseBufferedFrame, message.Task.IsCompleted);
                await connection.DisposeAsync().AsTask().WaitAsync(LauncherPipeTestSession.Bound);
                await connection.DisposeAsync().AsTask().WaitAsync(LauncherPipeTestSession.Bound);
            }
            finally
            {
                stream.Release.TrySetResult();
            }
        }
    }
}
