// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

public sealed partial class ListItemInitializationCoordinatorTests
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "MSTEST0045:Use cooperative cancellation for timeout", Justification = "Synchronous and native operations require the independent MSTest timeout watchdog.")]
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(15000, CooperativeCancellation = false)]
    public void CompletedCoordinatorReleasesItemSnapshot(bool stopBeforeRun)
    {
        var order = new ConcurrentQueue<int>();
        var coordinator = CreateCoordinatorWithSnapshot(order, out var snapshot);
        Assert.IsTrue(IsSnapshotAliveAfterCollection(snapshot));

        if (stopBeforeRun)
        {
            coordinator.Stop();
        }

        coordinator.Run(CancellationToken.None);

        Assert.IsTrue(coordinator.Completion.IsCompletedSuccessfully);
        Assert.HasCount(stopBeforeRun ? 0 : 4, order);
        Assert.IsFalse(IsSnapshotAliveAfterCollection(snapshot), "A completed coordinator must release its item snapshot.");
        GC.KeepAlive(coordinator);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(15000, CooperativeCancellation = true)]
    public async Task InterruptedWorkerReleasesItemSnapshotAfterReturning(bool cancel)
    {
        using var firstStarted = new ManualResetEventSlim();
        using var continueFirst = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource();
        var order = new ConcurrentQueue<int>();
        var coordinator = CreateCoordinatorWithSnapshot(order, out var snapshot, firstStarted, continueFirst);
        var worker = Task.Run(() => coordinator.Run(cancellation.Token), TestContext.CancellationToken);

        try
        {
            Assert.IsTrue(firstStarted.Wait(TimeSpan.FromSeconds(2), TestContext.CancellationToken));
            if (cancel)
            {
                cancellation.Cancel();
            }
            else
            {
                coordinator.Stop();
            }

            Assert.IsFalse(coordinator.Completion.IsCompleted);
            Assert.IsTrue(IsSnapshotAliveAfterCollection(snapshot), "The running worker still owns its snapshot.");

            continueFirst.Set();
            await worker.WaitAsync(TimeSpan.FromSeconds(2), TestContext.CancellationToken);

            Assert.IsTrue(coordinator.Completion.IsCompletedSuccessfully);
            Assert.HasCount(1, order);
            Assert.IsFalse(IsSnapshotAliveAfterCollection(snapshot), "The snapshot must be released after the worker returns.");
        }
        finally
        {
            continueFirst.Set();
            coordinator.Stop();
            await worker.WaitAsync(TimeSpan.FromSeconds(2), System.Threading.CancellationToken.None);
        }

        GC.KeepAlive(coordinator);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "MSTEST0045:Use cooperative cancellation for timeout", Justification = "Synchronous and native operations require the independent MSTest timeout watchdog.")]
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(15000, CooperativeCancellation = false)]
    public void ReleasedDemandStorageStaysBoundedWithoutWorkerProgress(bool stopped)
    {
        var (_, items) = CreateItems(4, new ConcurrentQueue<int>());
        var coordinator = new ListItemInitializationCoordinator(items);
        if (stopped)
        {
            coordinator.Stop();
        }

        try
        {
            AddReleasedRealizations(items[3], 5000);
            Assert.IsLessThanOrEqualTo(1, GetRetainedDemandNodes(items[3]).Length, "An idle item must not retain its realization history.");
            Assert.IsLessThanOrEqualTo(1, GetIncomingDemandNodes(coordinator).Length, "An idle coordinator must not retain released requests.");
        }
        finally
        {
            coordinator.Stop();
            foreach (var item in items)
            {
                item.SafeCleanup();
            }
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "MSTEST0045:Use cooperative cancellation for timeout", Justification = "Synchronous and native operations require the independent MSTest timeout watchdog.")]
    [TestMethod]
    [Timeout(15000, CooperativeCancellation = false)]
    public void PruningReleasedDemandsPreservesLiveRequestsAndFifoOrder()
    {
        var order = new ConcurrentQueue<int>();
        var (_, items) = CreateItems(4, order);
        var coordinator = new ListItemInitializationCoordinator(items);
        var first = items[3].BeginRealization();
        AddReleasedRealizations(items[3], 2500);
        var second = items[1].BeginRealization();
        AddReleasedRealizations(items[3], 2500);
        var third = items[2].BeginRealization();

        try
        {
            Assert.IsLessThanOrEqualTo(2, GetRetainedDemandNodes(items[3]).Length);
            Assert.HasCount(3, GetIncomingDemandNodes(coordinator), "Only the three live requests should remain queued.");
            Assert.IsTrue(first.IsValid);
            Assert.IsTrue(second.IsValid);
            Assert.IsTrue(third.IsValid);

            coordinator.Run(CancellationToken.None);
            Assert.AreSequenceEqual(BatchedPriorityOrder, order.ToArray());
        }
        finally
        {
            first.Release();
            second.Release();
            third.Release();
            coordinator.Stop();
        }
    }

    [TestMethod]
    [Timeout(15000, CooperativeCancellation = true)]
    public async Task ReleasedDemandStorageStaysBoundedWhileExtensionGetterIsBlocked()
    {
        using var firstStarted = new ManualResetEventSlim();
        using var continueFirst = new ManualResetEventSlim();
        var order = new ConcurrentQueue<int>();
        var (_, items) = CreateItems(4, order, firstStarted, continueFirst);
        var coordinator = new ListItemInitializationCoordinator(items);
        var worker = Task.Run(() => coordinator.Run(CancellationToken.None), TestContext.CancellationToken);
        ListItemRealizationRegistration visible = default;

        try
        {
            Assert.IsTrue(firstStarted.Wait(TimeSpan.FromSeconds(2), TestContext.CancellationToken));
            visible = items[3].BeginRealization();
            AddReleasedRealizations(items[3], 5000);
            Assert.IsLessThanOrEqualTo(2, GetRetainedDemandNodes(items[3]).Length);
            Assert.IsLessThanOrEqualTo(2, GetIncomingDemandNodes(coordinator).Length);

            continueFirst.Set();
            await worker.WaitAsync(TimeSpan.FromSeconds(2), TestContext.CancellationToken);
            Assert.AreSequenceEqual(RealizedPriorityOrder, order.ToArray());
        }
        finally
        {
            visible.Release();
            continueFirst.Set();
            coordinator.Stop();
            await worker.WaitAsync(TimeSpan.FromSeconds(2), System.Threading.CancellationToken.None);
        }
    }

    [TestMethod]
    [Timeout(30000, CooperativeCancellation = true)]
    public async Task ConcurrentPruningAndDequeuePreservePublishedOrder()
    {
        const int liveCount = 128;
        var expected = Enumerable.Range(1, liveCount).Append(0).ToArray();
        for (var iteration = 0; iteration < 16; iteration++)
        {
            var order = new ConcurrentQueue<int>();
            var (_, items) = CreateItems(liveCount + 2, order);
            var coordinator = new ListItemInitializationCoordinator(items);
            var registrations = new ListItemRealizationRegistration[liveCount];
            for (var i = 0; i < liveCount; i++)
            {
                registrations[i] = items[i + 1].BeginRealization();
            }

            using var start = new Barrier(2);
            var producer = Task.Run(
                () =>
            {
                Assert.IsTrue(start.SignalAndWait(TimeSpan.FromSeconds(5), TestContext.CancellationToken));
                AddReleasedRealizations(items[liveCount + 1], 5000);
            },
                TestContext.CancellationToken);
            var worker = Task.Run(
                () =>
            {
                Assert.IsTrue(start.SignalAndWait(TimeSpan.FromSeconds(5), TestContext.CancellationToken));
                coordinator.Run(CancellationToken.None);
            },
                TestContext.CancellationToken);

            try
            {
                await Task.WhenAll(producer, worker).WaitAsync(TimeSpan.FromSeconds(10), TestContext.CancellationToken);

                // The churned item may be claimed before its registration is released.
                // All earlier live requests must still precede speculation, in order.
                var actual = order.Where(static index => index != liveCount + 1).ToArray();
                Assert.AreSequenceEqual(expected, actual);
            }
            finally
            {
                coordinator.Stop();
                foreach (var registration in registrations)
                {
                    registration.Release();
                }

                await Task.WhenAll(producer, worker).WaitAsync(TimeSpan.FromSeconds(10), System.Threading.CancellationToken.None);
            }
        }
    }

    // Separate frames keep Debug locals from extending the snapshot's lifetime.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ListItemInitializationCoordinator CreateCoordinatorWithSnapshot(
        ConcurrentQueue<int> order,
        out WeakReference<ListItemViewModel[]> snapshot,
        ManualResetEventSlim? firstStarted = null,
        ManualResetEventSlim? continueFirst = null)
    {
        var (_, items) = CreateItems(4, order, firstStarted, continueFirst);
        snapshot = new(items);
        return new(items);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool IsSnapshotAliveAfterCollection(WeakReference<ListItemViewModel[]> snapshot)
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
        return snapshot.TryGetTarget(out _);
    }

    private static ListItemInitializationDemandNode[] GetIncomingDemandNodes(ListItemInitializationCoordinator coordinator)
    {
        // Only inspect a queue whose producers and consumer are idle or blocked.
        var field = typeof(ListItemInitializationCoordinator).GetField("_incomingRequests", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new AssertFailedException("The coordinator's incoming demand storage was not found.");
        return GetDemandNodes(field.GetValue(coordinator));
    }
}
