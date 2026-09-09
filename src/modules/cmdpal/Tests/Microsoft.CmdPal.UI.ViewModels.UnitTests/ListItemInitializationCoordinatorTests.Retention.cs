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
    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(15000)]
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
        Assert.AreEqual(stopBeforeRun ? 0 : 4, order.Count);
        Assert.IsFalse(IsSnapshotAliveAfterCollection(snapshot), "A completed coordinator must release its item snapshot.");
        GC.KeepAlive(coordinator);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(15000)]
    public async Task InterruptedWorkerReleasesItemSnapshotAfterReturning(bool cancel)
    {
        using var firstStarted = new ManualResetEventSlim();
        using var continueFirst = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource();
        var order = new ConcurrentQueue<int>();
        var coordinator = CreateCoordinatorWithSnapshot(order, out var snapshot, firstStarted, continueFirst);
        var worker = Task.Run(() => coordinator.Run(cancellation.Token));

        try
        {
            Assert.IsTrue(firstStarted.Wait(TimeSpan.FromSeconds(2)));
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
            await worker.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.IsTrue(coordinator.Completion.IsCompletedSuccessfully);
            Assert.AreEqual(1, order.Count);
            Assert.IsFalse(IsSnapshotAliveAfterCollection(snapshot), "The snapshot must be released after the worker returns.");
        }
        finally
        {
            continueFirst.Set();
            coordinator.Stop();
            await worker.WaitAsync(TimeSpan.FromSeconds(2));
        }

        GC.KeepAlive(coordinator);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(15000)]
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
            Assert.IsTrue(GetRetainedDemandNodes(items[3]).Length <= 1, "An idle item must not retain its realization history.");
            Assert.IsTrue(GetIncomingDemandNodes(coordinator).Length <= 1, "An idle coordinator must not retain released requests.");
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

    [TestMethod]
    [Timeout(15000)]
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
            Assert.IsTrue(GetRetainedDemandNodes(items[3]).Length <= 2);
            Assert.AreEqual(3, GetIncomingDemandNodes(coordinator).Length, "Only the three live requests should remain queued.");
            Assert.IsTrue(first.IsValid);
            Assert.IsTrue(second.IsValid);
            Assert.IsTrue(third.IsValid);

            coordinator.Run(CancellationToken.None);
            CollectionAssert.AreEqual(BatchedPriorityOrder, order.ToArray());
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
    [Timeout(15000)]
    public async Task ReleasedDemandStorageStaysBoundedWhileExtensionGetterIsBlocked()
    {
        using var firstStarted = new ManualResetEventSlim();
        using var continueFirst = new ManualResetEventSlim();
        var order = new ConcurrentQueue<int>();
        var (_, items) = CreateItems(4, order, firstStarted, continueFirst);
        var coordinator = new ListItemInitializationCoordinator(items);
        var worker = Task.Run(() => coordinator.Run(CancellationToken.None));
        ListItemRealizationRegistration visible = default;

        try
        {
            Assert.IsTrue(firstStarted.Wait(TimeSpan.FromSeconds(2)));
            visible = items[3].BeginRealization();
            AddReleasedRealizations(items[3], 5000);
            Assert.IsTrue(GetRetainedDemandNodes(items[3]).Length <= 2);
            Assert.IsTrue(GetIncomingDemandNodes(coordinator).Length <= 2);

            continueFirst.Set();
            await worker.WaitAsync(TimeSpan.FromSeconds(2));
            CollectionAssert.AreEqual(RealizedPriorityOrder, order.ToArray());
        }
        finally
        {
            visible.Release();
            continueFirst.Set();
            coordinator.Stop();
            await worker.WaitAsync(TimeSpan.FromSeconds(2));
        }
    }

    [TestMethod]
    [Timeout(30000)]
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
            var producer = Task.Run(() =>
            {
                Assert.IsTrue(start.SignalAndWait(TimeSpan.FromSeconds(5)));
                AddReleasedRealizations(items[liveCount + 1], 5000);
            });
            var worker = Task.Run(() =>
            {
                Assert.IsTrue(start.SignalAndWait(TimeSpan.FromSeconds(5)));
                coordinator.Run(CancellationToken.None);
            });

            try
            {
                await Task.WhenAll(producer, worker).WaitAsync(TimeSpan.FromSeconds(10));

                // The churned item may be claimed before its registration is released.
                // All earlier live requests must still precede speculation, in order.
                var actual = order.Where(static index => index != liveCount + 1).ToArray();
                CollectionAssert.AreEqual(expected, actual);
            }
            finally
            {
                coordinator.Stop();
                foreach (var registration in registrations)
                {
                    registration.Release();
                }

                await Task.WhenAll(producer, worker).WaitAsync(TimeSpan.FromSeconds(10));
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
