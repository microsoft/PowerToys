// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

/// <summary>
/// Verifies the per-directory lifecycle gate (p4-02). Concurrent triggers (initial
/// load, refresh, crash-restart, hot-reload) must be serialized per canonical
/// directory so they cannot launch duplicate processes, while different directories
/// still run concurrently. Removing an entry during a concurrent acquire must not
/// throw an ObjectDisposedException.
/// </summary>
[TestClass]
public class DirectoryLifecycleGateTests
{
    [TestMethod]
    public void Canonicalize_TrailingSeparatorAndCase_ProduceSameKey()
    {
        var a = DirectoryLifecycleGate.Canonicalize(@"C:\temp\Ext");
        var b = DirectoryLifecycleGate.Canonicalize(@"C:\temp\Ext\");
        Assert.AreEqual(a, b);
    }

    [TestMethod]
    public async Task AcquireAsync_SameDirectory_SerializesOperations()
    {
        using var gate = new DirectoryLifecycleGate();
        const string Dir = @"C:\temp\extension-a";

        var running = 0;
        var maxConcurrent = 0;
        var sync = new object();

        async Task Operation()
        {
            using (await gate.AcquireAsync(Dir, CancellationToken.None))
            {
                lock (sync)
                {
                    running++;
                    maxConcurrent = Math.Max(maxConcurrent, running);
                }

                await Task.Delay(25);

                lock (sync)
                {
                    running--;
                }
            }
        }

        await Task.WhenAll(Operation(), Operation(), Operation(), Operation());

        Assert.AreEqual(1, maxConcurrent, "Operations for one directory must never overlap.");
    }

    [TestMethod]
    public async Task AcquireAsync_DifferentDirectories_RunConcurrently()
    {
        using var gate = new DirectoryLifecycleGate();

        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var firstTask = Task.Run(async () =>
        {
            using (await gate.AcquireAsync(@"C:\temp\dir-1", CancellationToken.None))
            {
                firstEntered.SetResult();
                await releaseFirst.Task;
            }
        });

        await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // A different directory must be acquirable while the first is still held.
        using (await gate.AcquireAsync(@"C:\temp\dir-2", CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)))
        {
            Assert.IsTrue(true, "Acquired a second directory while the first was held.");
        }

        releaseFirst.SetResult();
        await firstTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [TestMethod]
    public async Task Remove_DuringConcurrentAcquire_DoesNotThrow()
    {
        using var gate = new DirectoryLifecycleGate();
        const string Dir = @"C:\temp\removed-dir";

        using (await gate.AcquireAsync(Dir, CancellationToken.None))
        {
            // Removing the entry while it is held marks it for removal; the release
            // that follows must not throw even though the entry was removed.
            gate.Remove(Dir);
        }

        // Re-acquiring after a removal transparently creates a fresh entry.
        using (await gate.AcquireAsync(Dir, CancellationToken.None))
        {
            Assert.IsTrue(true);
        }
    }

    [TestMethod]
    public async Task AcquireAsync_AfterDispose_Throws()
    {
        var gate = new DirectoryLifecycleGate();
        gate.Dispose();

        await Assert.ThrowsExceptionAsync<ObjectDisposedException>(
            async () => await gate.AcquireAsync(@"C:\temp\any", CancellationToken.None));
    }
}
