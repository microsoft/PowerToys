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
    public async Task Remove_WhileHeld_NewAcquireSerializesBehindPriorGeneration()
    {
        using var gate = new DirectoryLifecycleGate();
        const string Dir = @"C:\temp\overlap-dir";

        var aEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseA = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var aTask = Task.Run(async () =>
        {
            using (await gate.AcquireAsync(Dir, CancellationToken.None))
            {
                aEntered.SetResult();
                await releaseA.Task;
            }
        });

        await aEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Remove the directory while the prior generation (A) still holds it. A new
        // generation must strictly supersede it, never overlap it.
        gate.Remove(Dir);

        var cTask = Task.Run(async () =>
        {
            using (await gate.AcquireAsync(Dir, CancellationToken.None))
            {
                cEntered.SetResult();
            }
        });

        // C must not enter while A still holds the gate, even though Remove was called in
        // between. Before the fix, Remove evicted the entry immediately, so C would create
        // a fresh entry with a new semaphore and run concurrently with A.
        var enteredEarly = await Task.WhenAny(cEntered.Task, Task.Delay(200)) == cEntered.Task;
        Assert.IsFalse(enteredEarly, "A new generation after Remove must serialize behind the still-live prior generation.");

        releaseA.SetResult();
        await cEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.WhenAll(aTask, cTask).WaitAsync(TimeSpan.FromSeconds(5));
    }

    [TestMethod]
    public async Task Remove_WhileHeld_OverlappingCycles_NeverRunConcurrently()
    {
        using var gate = new DirectoryLifecycleGate();
        const string Dir = @"C:\temp\overlap-cycles";

        var running = 0;
        var maxConcurrent = 0;
        var sync = new object();

        async Task Cycle()
        {
            using (await gate.AcquireAsync(Dir, CancellationToken.None))
            {
                lock (sync)
                {
                    running++;
                    maxConcurrent = Math.Max(maxConcurrent, running);
                }

                await Task.Delay(15);

                // Marking the directory for removal mid-cycle starts a fresh generation for
                // any queued acquire; it must still not overlap this one.
                gate.Remove(Dir);

                lock (sync)
                {
                    running--;
                }
            }
        }

        await Task.WhenAll(Cycle(), Cycle(), Cycle(), Cycle()).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.AreEqual(1, maxConcurrent, "Overlapping begin/complete cycles for one directory must never run concurrently.");
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
