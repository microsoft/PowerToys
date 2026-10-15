// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Awake.UnitTests;

[TestClass]
public class ShutdownCompletionTests
{
    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    public async Task WaitForShutdownAsync_AcceptedCommand_WaitsForActualExitCode(int exitCode)
    {
        var shutdown = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        Task<int> result = Program.WaitForShutdownAsync(0, shutdown.Task);

        Assert.IsFalse(result.IsCompleted, "A successful launch is not application shutdown.");
        shutdown.SetResult(exitCode);
        Assert.AreEqual(exitCode, await result);
    }

    [TestMethod]
    public async Task WaitForShutdownAsync_FailedCommand_DoesNotWaitForShutdown()
    {
        var shutdown = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        Task<int> result = Program.WaitForShutdownAsync(1, shutdown.Task);

        Assert.IsTrue(result.IsCompleted);
        Assert.AreEqual(1, await result);
        Assert.IsFalse(shutdown.Task.IsCompleted);
    }

    [TestMethod]
    public async Task WaitForShutdownAsync_CompletedShutdown_PreservesFailureExitCode()
    {
        Assert.AreEqual(1, await Program.WaitForShutdownAsync(0, Task.FromResult(1)));
    }
}
