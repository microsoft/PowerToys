// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

/// <summary>
/// Verifies that crash-recovery work started from a Node process exit is owned rather than
/// detached: uninstall, service stop, and disposal must cancel it, await it, and clean it up.
/// The service's own crash path needs a live Node process, so these tests drive the tracker
/// that owns that work, including the uninstall-versus-recovery deadlock shape (recovery
/// blocked on the same directory lifecycle gate the uninstall is about to take).
/// </summary>
[TestClass]
public class CrashRecoveryTrackerTests
{
    private const string DirectoryA = @"C:\temp\cmdpal-ext-a";
    private const string DirectoryB = @"C:\temp\cmdpal-ext-b";

    private static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(5);

    [TestMethod]
    public async Task TryTrack_RunsWork_AndStopsTrackingWhenItCompletes()
    {
        using var tracker = new CrashRecoveryTracker();

        var ran = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.IsTrue(tracker.TryTrack(DirectoryA, _ =>
        {
            ran.SetResult();
            return Task.CompletedTask;
        }));

        await ran.Task.WaitAsync(DrainTimeout);

        // Drain is the observable "it finished and was cleaned up" point.
        await tracker.CancelAndDrainAsync(DirectoryA).WaitAsync(DrainTimeout);
        tracker.CompleteDirectoryRemoval(DirectoryA);
        Assert.AreEqual(0, tracker.InFlightCount);
        Assert.IsFalse(tracker.IsTracking(DirectoryA));
    }

    [TestMethod]
    public async Task TryTrack_UsesSameEntry_ForEquivalentDirectoryPaths()
    {
        using var tracker = new CrashRecoveryTracker();

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Assert.IsTrue(tracker.TryTrack(DirectoryA, async ct =>
        {
            started.SetResult();
            await WaitForCancellationAsync(ct).ConfigureAwait(false);
            canceled.SetResult();
        }));

        await started.Task.WaitAsync(DrainTimeout);

        // The uninstall path hands over whatever path the watcher reported, so a trailing
        // separator or different casing must still cancel the same directory's recovery.
        await tracker.CancelAndDrainAsync(DirectoryA.ToUpperInvariant() + @"\").WaitAsync(DrainTimeout);
        tracker.CompleteDirectoryRemoval(DirectoryA);

        await canceled.Task.WaitAsync(DrainTimeout);
        Assert.AreEqual(0, tracker.InFlightCount);
    }

    [TestMethod]
    public async Task CancelAndDrain_WhileRecoveryIsActive_CancelsAndAwaitsIt()
    {
        using var tracker = new CrashRecoveryTracker();

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = false;

        Assert.IsTrue(tracker.TryTrack(DirectoryA, async ct =>
        {
            started.SetResult();
            await WaitForCancellationAsync(ct).ConfigureAwait(false);

            // Model the tail of a restart that still has teardown to do after cancellation.
            await Task.Delay(50, CancellationToken.None).ConfigureAwait(false);
            finished = true;
        }));

        await started.Task.WaitAsync(DrainTimeout);

        await tracker.CancelAndDrainAsync(DirectoryA).WaitAsync(DrainTimeout);
        tracker.CompleteDirectoryRemoval(DirectoryA);

        Assert.IsTrue(finished, "Uninstall must await the recovery task, not just cancel it.");
        Assert.AreEqual(0, tracker.InFlightCount);
    }

    [TestMethod]
    public async Task CancelAndDrain_WhileRecoveryIsQueuedBehindTheGate_DoesNotDeadlock()
    {
        // The shape that would deadlock without cancel-before-await: recovery is blocked
        // acquiring the directory's lifecycle gate, and the uninstall is about to take that
        // same gate. Canceling first releases the waiter, so the drain completes.
        using var gate = new DirectoryLifecycleGate();
        using var tracker = new CrashRecoveryTracker();

        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observedCancellation = false;

        using (await gate.AcquireAsync(DirectoryA, CancellationToken.None))
        {
            Assert.IsTrue(tracker.TryTrack(DirectoryA, async ct =>
            {
                waiting.SetResult();
                try
                {
                    using (await gate.AcquireAsync(DirectoryA, ct).ConfigureAwait(false))
                    {
                        Assert.Fail("Recovery must not acquire the gate the uninstall holds.");
                    }
                }
                catch (OperationCanceledException)
                {
                    observedCancellation = true;
                    throw;
                }
            }));

            await waiting.Task.WaitAsync(DrainTimeout);

            // Drain while the gate is still held. It must return instead of waiting forever
            // on a task that can only finish once the gate is released.
            await tracker.CancelAndDrainAsync(DirectoryA).WaitAsync(DrainTimeout);
        }

        Assert.IsTrue(observedCancellation, "The queued recovery must observe cancellation.");
        Assert.AreEqual(0, tracker.InFlightCount);
        tracker.CompleteDirectoryRemoval(DirectoryA);
    }

    [TestMethod]
    public async Task CancelAndDrain_RejectsNewRecoveryForThatDirectoryWhileDraining()
    {
        using var tracker = new CrashRecoveryTracker();

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Assert.IsTrue(tracker.TryTrack(DirectoryA, async _ =>
        {
            started.SetResult();
            await release.Task.ConfigureAwait(false);
        }));

        await started.Task.WaitAsync(DrainTimeout);

        var drain = tracker.CancelAndDrainAsync(DirectoryA);

        // An extension torn down by the uninstall raises its own process exit; that late
        // crash must not queue a restart for a directory that is going away.
        Assert.IsFalse(
            tracker.TryTrack(DirectoryA, _ => Task.CompletedTask),
            "Recovery must not be accepted for a directory that is being uninstalled.");

        release.SetResult();
        await drain.WaitAsync(DrainTimeout);

        Assert.IsFalse(
            tracker.TryTrack(DirectoryA, _ => Task.CompletedTask),
            "Recovery must stay blocked until the uninstall finishes.");

        // Once the uninstall has drained, a reinstall of the same directory can recover again.
        tracker.CompleteDirectoryRemoval(DirectoryA);
        Assert.IsTrue(tracker.TryTrack(DirectoryA, _ => Task.CompletedTask));
        await tracker.CancelAndDrainAsync(DirectoryA).WaitAsync(DrainTimeout);
        tracker.CompleteDirectoryRemoval(DirectoryA);
    }

    [TestMethod]
    public async Task TimedOutDrain_ReopensDirectoryAfterRemovalAndRecoveryComplete()
    {
        using var tracker = new CrashRecoveryTracker();

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Assert.IsTrue(tracker.TryTrack(DirectoryA, async _ =>
        {
            started.SetResult();
            await release.Task.ConfigureAwait(false);
            completed.SetResult();
        }));

        await started.Task.WaitAsync(DrainTimeout);
        await tracker.CancelAndDrainAsync(DirectoryA, TimeSpan.FromMilliseconds(25));
        tracker.CompleteDirectoryRemoval(DirectoryA);

        Assert.IsFalse(
            tracker.TryTrack(DirectoryA, _ => Task.CompletedTask),
            "Recovery should stay blocked while the timed out task is still running.");

        release.SetResult();
        await completed.Task.WaitAsync(DrainTimeout);

        var accepted = false;
        var deadline = DateTime.UtcNow + DrainTimeout;
        while (!accepted && DateTime.UtcNow < deadline)
        {
            accepted = tracker.TryTrack(DirectoryA, _ => Task.CompletedTask);
            if (!accepted)
            {
                await Task.Delay(10);
            }
        }

        Assert.IsTrue(accepted, "The directory should reopen after removal and recovery both finish.");
        await tracker.CancelAndDrainAsync(DirectoryA).WaitAsync(DrainTimeout);
        tracker.CompleteDirectoryRemoval(DirectoryA);
    }

    [TestMethod]
    public async Task CancelAndDrain_LeavesOtherDirectoriesRunning()
    {
        using var tracker = new CrashRecoveryTracker();

        var otherStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseOther = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var otherCanceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        Assert.IsTrue(tracker.TryTrack(DirectoryB, async ct =>
        {
            otherStarted.SetResult();
            await releaseOther.Task.ConfigureAwait(false);
            otherCanceled.SetResult(ct.IsCancellationRequested);
        }));

        Assert.IsTrue(tracker.TryTrack(DirectoryA, _ => Task.CompletedTask));

        await otherStarted.Task.WaitAsync(DrainTimeout);
        await tracker.CancelAndDrainAsync(DirectoryA).WaitAsync(DrainTimeout);
        tracker.CompleteDirectoryRemoval(DirectoryA);

        Assert.IsTrue(tracker.IsTracking(DirectoryB), "Uninstalling one extension must not cancel another's recovery.");

        // Let the other directory's recovery observe its own token before anything cancels
        // it, so the assertion is ordered rather than racing a later drain.
        releaseOther.SetResult();
        Assert.IsFalse(
            await otherCanceled.Task.WaitAsync(DrainTimeout),
            "The untouched directory's recovery must keep running with a live token.");

        await tracker.DrainAllAsync().WaitAsync(DrainTimeout);
        Assert.AreEqual(0, tracker.InFlightCount);
    }

    [TestMethod]
    public async Task DrainAll_ClosesRecoveryUntilTheNextLoadCycle()
    {
        using var tracker = new CrashRecoveryTracker();

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Assert.IsTrue(tracker.TryTrack(DirectoryA, async ct =>
        {
            started.SetResult();
            await WaitForCancellationAsync(ct).ConfigureAwait(false);
            canceled.SetResult();
        }));

        await started.Task.WaitAsync(DrainTimeout);

        await tracker.DrainAllAsync().WaitAsync(DrainTimeout);
        await canceled.Task.WaitAsync(DrainTimeout);
        Assert.AreEqual(0, tracker.InFlightCount);

        // Stopping the service tears extensions down, which raises process exits. Those must
        // not start restart work behind the shutdown.
        Assert.IsFalse(
            tracker.TryTrack(DirectoryB, _ => Task.CompletedTask),
            "A stopped tracker must refuse new recovery.");

        // A later load cycle re-opens it.
        tracker.BeginCycle();
        Assert.IsTrue(tracker.TryTrack(DirectoryB, _ => Task.CompletedTask));
        await tracker.DrainAllAsync().WaitAsync(DrainTimeout);
    }

    [TestMethod]
    public async Task DrainAll_TimesOut_NextCycleUsesFreshCancellationToken()
    {
        using var tracker = new CrashRecoveryTracker();

        var oldStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseOld = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.IsTrue(tracker.TryTrack(DirectoryA, async _ =>
        {
            oldStarted.SetResult();
            await releaseOld.Task.ConfigureAwait(false);
        }));

        await oldStarted.Task.WaitAsync(DrainTimeout);
        await tracker.DrainAllAsync(TimeSpan.FromMilliseconds(25));

        tracker.BeginCycle();
        var newTokenCanceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.IsTrue(tracker.TryTrack(DirectoryA, ct =>
        {
            newTokenCanceled.SetResult(ct.IsCancellationRequested);
            return Task.CompletedTask;
        }));

        Assert.IsFalse(
            await newTokenCanceled.Task.WaitAsync(DrainTimeout),
            "A new load cycle should not reuse the canceled token from a straggler.");

        releaseOld.SetResult();
        await tracker.DrainAllAsync().WaitAsync(DrainTimeout);
    }

    [TestMethod]
    public async Task Dispose_WhileRecoveryIsActive_CancelsAwaitsAndRefusesMore()
    {
        var tracker = new CrashRecoveryTracker();

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Assert.IsTrue(tracker.TryTrack(DirectoryA, async ct =>
        {
            started.SetResult();
            await WaitForCancellationAsync(ct).ConfigureAwait(false);
            completed.SetResult();
        }));

        await started.Task.WaitAsync(DrainTimeout);

        // Dispose is synchronous and may run on the UI thread, so it must return promptly.
        var disposeTask = Task.Run(tracker.Dispose);
        await disposeTask.WaitAsync(DrainTimeout);

        await completed.Task.WaitAsync(DrainTimeout);
        Assert.AreEqual(0, tracker.InFlightCount);
        Assert.IsFalse(tracker.TryTrack(DirectoryA, _ => Task.CompletedTask), "A disposed tracker must refuse new recovery.");

        // Disposal is idempotent.
        tracker.Dispose();
    }

    [TestMethod]
    public async Task Dispose_WithWedgedRecovery_ReturnsWithinItsBoundedWait()
    {
        var tracker = new CrashRecoveryTracker();

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Assert.IsTrue(tracker.TryTrack(DirectoryA, async _ =>
        {
            started.SetResult();
            await release.Task.ConfigureAwait(false);
        }));

        await started.Task.WaitAsync(DrainTimeout);

        // Recovery that ignores cancellation must not hang shutdown: Dispose waits a bounded
        // time and then leaves the canceled straggler to unwind.
        var disposeTask = Task.Run(tracker.Dispose);
        await disposeTask.WaitAsync(TimeSpan.FromSeconds(10));

        release.SetResult();
    }

    [TestMethod]
    public async Task TrackedWork_ThatThrows_DoesNotFaultTheDrain()
    {
        using var tracker = new CrashRecoveryTracker();

        Assert.IsTrue(tracker.TryTrack(DirectoryA, _ => throw new InvalidOperationException("boom")));

        // A failing restart is logged by the service; the drain must still complete so
        // uninstall and shutdown are never blocked or faulted by it.
        await tracker.CancelAndDrainAsync(DirectoryA).WaitAsync(DrainTimeout);
        tracker.CompleteDirectoryRemoval(DirectoryA);
        Assert.AreEqual(0, tracker.InFlightCount);
    }

    private static async Task WaitForCancellationAsync(CancellationToken ct)
    {
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using (ct.Register(() => canceled.TrySetResult()))
        {
            await canceled.Task.ConfigureAwait(false);
        }
    }
}
