// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

public partial class AppCatalogTests
{
    [TestMethod]
    public async Task Recovery_StartupFailureDoesNotWaitForRetries()
    {
        var clock = new RecoveryTimeProvider();
        using var source = new TestAppSource("test", []) { LoadFailure = new IOException("Temporary read failure") };
        using var catalog = CreateRecoveryCatalog([source], clock);
        await catalog.InitializeAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(1, source.LoadCount);

        source.LoadFailure = null;
        source.SetItems([CreateCatalogItem("Recovered")]);
        clock.Advance(TimeSpan.FromSeconds(5));
        await WaitForConditionAsync(() => ContainsApp(catalog.GetSnapshot().Items, "Recovered") && !catalog.IsRefreshing);
        Assert.IsTrue(source.LastLoadBackground);
    }

    [TestMethod]
    public async Task Recovery_RejectedShortcutRetriesOnlyItsPathAndStopsAfterSuccess()
    {
        const string path = @"C:\Apps\New.lnk";
        var clock = new RecoveryTimeProvider();
        using var source = new TestAppSource("test", new AppSourceScanResult([CreateCatalogItem("Existing")], retryPaths: [path]));
        using var other = new TestAppSource("other", [CreateCatalogItem("Other", sourceId: "other")]);
        using var catalog = CreateRecoveryCatalog([source, other], clock);
        await catalog.InitializeAsync();
        source.SetIncrementalFactory(items => [.. items, CreateCatalogItem("New")]);

        clock.Advance(TimeSpan.FromSeconds(5));
        await WaitForConditionAsync(() => source.IncrementalLoadCount == 1 && !catalog.IsRefreshing);
        Assert.AreEqual(path, source.LastIncrementalChanges.Single().Path);
        Assert.IsTrue(source.LastIncrementalBackground);
        Assert.IsTrue(ContainsApp(catalog.GetSnapshot().Items, "New"));
        Assert.AreEqual(1, other.LoadCount);

        clock.Advance(TimeSpan.FromMinutes(2));
        Assert.AreEqual(1, source.IncrementalLoadCount);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task Recovery_PromotedFullScanClearsOtherResolvedRetriesAndValidatesOnlyCompleteScans(bool complete)
    {
        const string first = @"C:\Apps\First.exe";
        const string second = @"C:\Apps\Second.exe";
        var clock = new RecoveryTimeProvider();
        using var source = new TestAppSource("test", new AppSourceScanResult([], retryPaths: [first]));
        var cache = new TestCache(null);
        using var catalog = new AppCatalog(new MutableSourceProvider([source]), cache, new VisibleApps(), timeProvider: clock, invalidationDelay: TimeSpan.Zero);
        await catalog.InitializeAsync();

        clock.Advance(TimeSpan.FromSeconds(2));
        source.SetIncrementalFactory(_ => new AppSourceScanResult([], retryPaths: [first, second]));
        source.Invalidate(AppSourceInvalidatedEventArgs.ForPath(new AppSourcePathChange(WatcherChangeTypes.Changed, second)));
        await WaitForConditionAsync(() => source.IncrementalLoadCount == 1 && !catalog.IsRefreshing);

        source.SetIncrementalFactory(_ => new AppSourceScanResult([CreateCatalogItem("Scanned")], isComplete: complete, failedPaths: [], isFullScan: true));
        clock.Advance(TimeSpan.FromSeconds(3));
        await WaitForConditionAsync(() => source.IncrementalLoadCount == 2 && !catalog.IsRefreshing);
        Assert.AreEqual(first, source.LastIncrementalChanges.Single().Path);

        clock.Advance(TimeSpan.FromSeconds(2));
        await WaitForConditionAsync(() => !catalog.IsRefreshing);
        Assert.AreEqual(2, source.IncrementalLoadCount, "The whole-source scan already checked the other pending retry.");
        Assert.AreEqual(complete, cache.LastFullyReconciledSourceIds.Contains(source.Id));
    }

    [TestMethod]
    public async Task Recovery_ItemRetriesAreBoundedAndNotificationsDoNotRenewBudget()
    {
        const string path = @"C:\Apps\Broken.lnk";
        var clock = new RecoveryTimeProvider();
        using var source = new TestAppSource("test", new AppSourceScanResult([], retryPaths: [path]));
        source.SetIncrementalFactory(_ => new AppSourceScanResult([], retryPaths: [path]));
        using var catalog = CreateRecoveryCatalog([source], clock);
        await catalog.InitializeAsync();

        clock.Advance(TimeSpan.FromSeconds(5));
        await WaitForConditionAsync(() => source.IncrementalLoadCount == 1 && !catalog.IsRefreshing);
        source.Invalidate(AppSourceInvalidatedEventArgs.ForPath(new AppSourcePathChange(WatcherChangeTypes.Changed, path)));
        await WaitForConditionAsync(() => source.IncrementalLoadCount == 2 && !catalog.IsRefreshing);
        clock.Advance(TimeSpan.FromSeconds(15));
        await WaitForConditionAsync(() => source.IncrementalLoadCount == 3 && !catalog.IsRefreshing);
        clock.Advance(TimeSpan.FromSeconds(40));
        await WaitForConditionAsync(() => source.IncrementalLoadCount == 4 && !catalog.IsRefreshing);
        source.Invalidate(AppSourceInvalidatedEventArgs.ForPath(new AppSourcePathChange(WatcherChangeTypes.Changed, path)));
        await WaitForConditionAsync(() => source.IncrementalLoadCount == 5 && !catalog.IsRefreshing);

        clock.Advance(TimeSpan.FromMinutes(3));
        Assert.AreEqual(5, source.IncrementalLoadCount);
    }

    [TestMethod]
    public async Task Recovery_FailedSourceKeepsSnapshotAndRetriesAtMostThreeTimes()
    {
        var clock = new RecoveryTimeProvider();
        using var source = new TestAppSource("test", [CreateCatalogItem("Existing")]);
        using var catalog = CreateRecoveryCatalog([source], clock);
        await catalog.InitializeAsync();
        source.LoadFailure = new IOException("Temporary read failure");
        await catalog.RefreshAsync();

        var expectedLoads = 2;
        foreach (var delay in new[] { 5, 15, 40 })
        {
            clock.Advance(TimeSpan.FromSeconds(delay));
            expectedLoads++;
            await WaitForConditionAsync(() => source.LoadCount == expectedLoads && !catalog.IsRefreshing);
            Assert.IsTrue(ContainsApp(catalog.GetSnapshot().Items, "Existing"));
        }

        clock.Advance(TimeSpan.FromMinutes(3));
        Assert.AreEqual(5, source.LoadCount);
    }

    [TestMethod]
    public async Task Recovery_IncompleteScanRetainsMissingRowsUntilACompleteScan()
    {
        var clock = new RecoveryTimeProvider();
        using var source = new TestAppSource("test", [CreateCatalogItem("Existing")]);
        using var catalog = CreateRecoveryCatalog([source], clock);
        await catalog.InitializeAsync();
        source.SetItems(new AppSourceScanResult([CreateCatalogItem("New")], isComplete: false));
        await catalog.RefreshAsync();
        Assert.IsTrue(ContainsApp(catalog.GetSnapshot().Items, "Existing"));
        Assert.IsTrue(ContainsApp(catalog.GetSnapshot().Items, "New"));

        source.SetItems([CreateCatalogItem("New")]);
        clock.Advance(TimeSpan.FromSeconds(5));
        await WaitForConditionAsync(() => source.LoadCount == 3 && !catalog.IsRefreshing);
        Assert.IsFalse(ContainsApp(catalog.GetSnapshot().Items, "Existing"));
        Assert.IsTrue(ContainsApp(catalog.GetSnapshot().Items, "New"));
    }

    [TestMethod]
    public async Task Recovery_BackgroundReconciliationRecoversMissedChanges()
    {
        var clock = new RecoveryTimeProvider();
        using var source = new TestAppSource("test", [CreateCatalogItem("Existing")]);
        using var catalog = CreateRecoveryCatalog([source], clock);
        var initialization = catalog.InitializeAsync();
        await initialization;
        Assert.IsFalse(source.LastLoadBackground);
        source.SetItems([CreateCatalogItem("New")]);

        clock.Advance(TimeSpan.FromMinutes(10));
        await WaitForConditionAsync(() => source.LoadCount == 2 && !catalog.IsRefreshing);
        Assert.IsTrue(initialization.IsCompletedSuccessfully);
        Assert.IsTrue(ContainsApp(catalog.GetSnapshot().Items, "New"));
        Assert.IsFalse(ContainsApp(catalog.GetSnapshot().Items, "Existing"));
        Assert.IsTrue(source.LastLoadBackground);
        await catalog.RefreshAsync();
        Assert.IsFalse(source.LastLoadBackground, "Explicit refreshes must retain their normal priority.");
    }

    [TestMethod]
    public void Recovery_UnusedCatalogDoesNotStartPeriodicDiscovery()
    {
        var clock = new RecoveryTimeProvider();
        using var source = new TestAppSource("test", []);
        using var catalog = CreateRecoveryCatalog([source], clock);
        clock.Advance(TimeSpan.FromMinutes(20));
        Assert.AreEqual(0, source.LoadCount);
    }

    [TestMethod]
    public async Task Recovery_RejectedPathQueueIsCapped()
    {
        var paths = Enumerable.Range(0, 1000).Select(index => $@"C:\Apps\{index}.lnk").ToArray();
        var clock = new RecoveryTimeProvider();
        using var source = new TestAppSource("test", new AppSourceScanResult([], retryPaths: paths));
        using var catalog = CreateRecoveryCatalog([source], clock);
        await catalog.InitializeAsync();

        clock.Advance(TimeSpan.FromSeconds(5));
        await WaitForConditionAsync(() => source.IncrementalLoadCount == 1 && !catalog.IsRefreshing);
        Assert.AreEqual(256, source.LastIncrementalChanges.Count);
    }

    [TestMethod]
    public async Task Recovery_RetiredSourceAndDisposedCatalogCannotRetry()
    {
        var clock = new RecoveryTimeProvider();
        using var source = new TestAppSource("test", new AppSourceScanResult([], retryPaths: [@"C:\Apps\Missing.lnk"]));
        using var replacement = new TestAppSource("test", [CreateCatalogItem("Replacement")]);
        var provider = new MutableSourceProvider([source]);
        using var catalog = new AppCatalog(provider, new TestCache(null), new VisibleApps(), timeProvider: clock, invalidationDelay: TimeSpan.Zero);
        await catalog.InitializeAsync();
        provider.SetSources([replacement]);
        await WaitForConditionAsync(() => replacement.LoadCount == 1 && !catalog.IsRefreshing);
        clock.Advance(TimeSpan.FromSeconds(5));
        Assert.AreEqual(0, source.IncrementalLoadCount);

        catalog.Dispose();
        clock.Advance(TimeSpan.FromMinutes(20));
        Assert.AreEqual(1, replacement.LoadCount);
        Assert.IsTrue(clock.Timers.All(timer => timer.IsDisposed));
    }

    [TestMethod]
    public async Task Recovery_BackgroundReconciliationRenewsExhaustedRetries()
    {
        var clock = new RecoveryTimeProvider();
        using var source = new TestAppSource("test", []) { LoadFailure = new IOException("Temporary read failure") };
        using var catalog = CreateRecoveryCatalog([source], clock);
        await catalog.InitializeAsync();
        var expectedLoads = 1;
        foreach (var delay in new[] { 5, 15, 40 })
        {
            clock.Advance(TimeSpan.FromSeconds(delay));
            expectedLoads++;
            await WaitForConditionAsync(() => source.LoadCount == expectedLoads && !catalog.IsRefreshing);
        }

        clock.Advance(TimeSpan.FromMinutes(10));
        await WaitForConditionAsync(() => source.LoadCount == 5 && !catalog.IsRefreshing);
        source.LoadFailure = null;
        source.SetItems([CreateCatalogItem("Recovered")]);
        clock.Advance(TimeSpan.FromSeconds(5));
        await WaitForConditionAsync(() => ContainsApp(catalog.GetSnapshot().Items, "Recovered") && !catalog.IsRefreshing);
        Assert.AreEqual(6, source.LoadCount);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Recovery_ScopedFailureAppliesDeletionsAndRetargeting(bool incremental)
    {
        const string locked = @"C:\Apps\Locked.lnk";
        const string deleted = @"C:\Apps\Deleted.lnk";
        const string retargeted = @"C:\Apps\Retargeted.lnk";
        var clock = new RecoveryTimeProvider();
        using var source = new TestAppSource(
            "test",
            [CreateRecoveryItem("Locked", locked), CreateRecoveryItem("Deleted", deleted), CreateRecoveryItem("Old target", retargeted)]);
        using var catalog = CreateRecoveryCatalog([source], clock);
        await catalog.InitializeAsync();
        var partial = new AppSourceScanResult(
            [CreateRecoveryItem("New target", retargeted)],
            isComplete: false,
            failedPaths: [locked],
            checkedPaths: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { retargeted });
        if (incremental)
        {
            source.SetIncrementalFactory(_ => partial);
            source.Invalidate(AppSourceInvalidatedEventArgs.ForPath(new AppSourcePathChange(WatcherChangeTypes.Changed, @"C:\Apps")));
            await WaitForConditionAsync(() => source.IncrementalLoadCount == 1 && !catalog.IsRefreshing);
        }
        else
        {
            source.SetItems(partial);
            await catalog.RefreshAsync();
        }

        string[] expected = ["Locked", "New target"];
        CollectionAssert.AreEquivalent(expected, catalog.GetSnapshot().Items.Select(item => item.Name).ToArray());
        clock.Advance(TimeSpan.FromMinutes(3));
        Assert.AreEqual(incremental ? 1 : 2, source.LoadCount, "A permanent scoped failure must not schedule full-source retries.");
    }

    [TestMethod]
    public async Task Recovery_UnreadableSubtreeDoesNotPreserveReadableSiblingsOrCheckedPaths()
    {
        const string folder = @"C:\Apps\Restricted";
        const string unknown = @"C:\Apps\Restricted\Unknown.lnk";
        const string checkedPath = @"C:\Apps\Restricted\Changed.lnk";
        using var source = new TestAppSource(
            "test",
            [CreateRecoveryItem("Unknown", unknown), CreateRecoveryItem("Old", checkedPath), CreateRecoveryItem("Deleted", @"C:\Apps\RestrictedSibling\Deleted.lnk")]);
        using var catalog = CreateRecoveryCatalog([source], new RecoveryTimeProvider());
        await catalog.InitializeAsync();
        source.SetItems(new AppSourceScanResult(
            [CreateRecoveryItem("New", checkedPath)],
            isComplete: false,
            failedPaths: [folder],
            checkedPaths: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { checkedPath }));
        await catalog.RefreshAsync();
        string[] expected = ["Unknown", "New"];
        CollectionAssert.AreEquivalent(expected, catalog.GetSnapshot().Items.Select(item => item.Name).ToArray());
    }

    [TestMethod]
    public void Recovery_RetainedDuplicateDropsRetargetedRepresentationAndItsLaunchAlias()
    {
        const string first = @"C:\Apps\First.lnk";
        const string second = @"C:\Apps\Second.lnk";
        const string target = @"C:\Targets\Old.exe";
        var old = CreateRecoveryItem("Old", first, target).MergeProvenance(CreateRecoveryItem("Old", second, target));
        var partial = new AppSourceScanResult(
            [CreateRecoveryItem("Old", first, @"C:\Targets\New.exe")],
            isComplete: false,
            failedPaths: [second],
            checkedPaths: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { first });
        var retained = partial.GetRetainedItem(old)!;
        Assert.IsNotNull(retained);
        Assert.AreEqual(second, retained.Provenance.References.Single().ItemId);
        Assert.AreEqual(string.Empty, ((Win32AppPayload)retained.Payload).LnkFilePath);
        CollectionAssert.DoesNotContain(retained.IdentityAliases.ToArray(), $"win32:{first}|args:");
        CollectionAssert.Contains(retained.IdentityAliases.ToArray(), $"win32:{second}|args:");
        CollectionAssert.Contains(partial[0].CommandIds.ToArray(), old.Payload.GetCommandId());
        CollectionAssert.DoesNotContain(retained.CommandIds.ToArray(), old.Payload.GetCommandId());
    }

    [TestMethod]
    public async Task Recovery_WholeSourceRetryMarkerDoesNotRenewItsBudget()
    {
        var clock = new RecoveryTimeProvider();
        using var source = new TestAppSource("test", new AppSourceScanResult([], isComplete: false, retryPaths: [string.Empty], failedPaths: []));
        using var catalog = CreateRecoveryCatalog([source], clock);
        await catalog.InitializeAsync();
        var expectedLoads = 1;
        foreach (var seconds in new[] { 5, 15, 40 })
        {
            clock.Advance(TimeSpan.FromSeconds(seconds));
            expectedLoads++;
            await WaitForConditionAsync(() => source.LoadCount == expectedLoads && !catalog.IsRefreshing);
        }

        clock.Advance(TimeSpan.FromMinutes(3));
        Assert.AreEqual(4, source.LoadCount);
    }

    [TestMethod]
    public async Task Recovery_ForegroundRefreshCancelsBackgroundWorkAndSupersedesItsScan()
    {
        var clock = new RecoveryTimeProvider();
        using var source = new TestAppSource("test", [CreateCatalogItem("Initial")]);
        using var other = new TestAppSource("other", [CreateCatalogItem("Other", sourceId: "other")]);
        using var catalog = CreateRecoveryCatalog([source, other], clock);
        await catalog.InitializeAsync();
        var background = new TaskCompletionSource<IReadOnlyList<AppCatalogItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        source.DeferNextLoad(background.Task);
        clock.Advance(TimeSpan.FromMinutes(10));
        await WaitForConditionAsync(() => source.LoadCount == 2);
        var token = source.LastLoadToken;
        Assert.IsTrue(source.LastLoadBackground);
        var otherLoads = other.LoadCount;

        source.SetItems([CreateCatalogItem("Foreground")]);
        await catalog.RefreshAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(token.IsCancellationRequested);
        Assert.IsFalse(source.LastLoadBackground);
        Assert.IsTrue(ContainsApp(catalog.GetSnapshot().Items, "Foreground"));
        background.SetResult([CreateCatalogItem("Obsolete")]);
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.AreEqual(3, source.LoadCount);
        Assert.AreEqual(otherLoads + 1, other.LoadCount);
        Assert.IsFalse(other.LastLoadBackground);
        Assert.IsFalse(ContainsApp(catalog.GetSnapshot().Items, "Obsolete"));
    }

    [TestMethod]
    public async Task Recovery_WatcherPublishesIncrementallyBeforeBackgroundResumes()
    {
        var clock = new RecoveryTimeProvider();
        using var source = new TestAppSource("test", [CreateCatalogItem("Initial")]);
        using var catalog = new AppCatalog(new MutableSourceProvider([source]), new TestCache(null), new VisibleApps(), timeProvider: clock, invalidationDelay: TimeSpan.FromSeconds(5));
        await catalog.InitializeAsync();
        var background = new TaskCompletionSource<IReadOnlyList<AppCatalogItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        source.DeferNextLoad(background.Task);
        clock.Advance(TimeSpan.FromMinutes(10));
        await WaitForConditionAsync(() => source.LoadCount == 2);
        var token = source.LastLoadToken;
        source.SetIncrementalFactory(items => [.. items, CreateCatalogItem("New")]);
        source.Invalidate(AppSourceInvalidatedEventArgs.ForPath(new AppSourcePathChange(WatcherChangeTypes.Created, @"C:\Apps\New.lnk")));
        await WaitForConditionAsync(() => !catalog.IsRefreshing && clock.TimerCount >= 3);
        Assert.IsTrue(token.IsCancellationRequested);

        clock.Advance(TimeSpan.FromSeconds(5));
        await WaitForConditionAsync(() => source.IncrementalLoadCount == 1 && !catalog.IsRefreshing && clock.TimerCount >= 4);
        Assert.IsFalse(source.LastIncrementalBackground);
        Assert.AreEqual(2, source.LoadCount, "A watcher update must stay incremental when a full background scan was paused.");
        Assert.IsTrue(ContainsApp(catalog.GetSnapshot().Items, "New"));

        source.SetItems([CreateCatalogItem("Reconciled")]);
        clock.Advance(TimeSpan.FromSeconds(5));
        await WaitForConditionAsync(() => source.LoadCount == 3 && !catalog.IsRefreshing);
        Assert.IsTrue(source.LastLoadBackground);
        Assert.IsTrue(ContainsApp(catalog.GetSnapshot().Items, "Reconciled"));
    }

    [TestMethod]
    [DataRow(true, true)]
    [DataRow(false, true)]
    [DataRow(false, false)]
    public async Task Recovery_QueuedReconciliationDoesNotAbsorbWatcherUpdates(bool backgroundFirst, bool flushWatcherFirst)
    {
        var clock = new RecoveryTimeProvider();
        using var blocker = new TestAppSource("other", [CreateCatalogItem("Other", sourceId: "other")]);
        using var source = new TestAppSource("test", [CreateCatalogItem("Initial")]);
        using var catalog = new AppCatalog(new MutableSourceProvider([blocker, source]), new TestCache(null), new VisibleApps(), timeProvider: clock, invalidationDelay: TimeSpan.FromSeconds(5));
        await catalog.InitializeAsync();
        var blocked = new TaskCompletionSource<IReadOnlyList<AppCatalogItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        blocker.DeferNextLoad(blocked.Task);
        blocker.Invalidate();
        clock.Advance(TimeSpan.FromSeconds(5));
        await WaitForConditionAsync(() => blocker.LoadCount == 2);

        if (backgroundFirst)
        {
            clock.Advance(TimeSpan.FromMinutes(10));
        }

        source.SetIncrementalFactory(items => [.. items, CreateCatalogItem("New")]);
        source.Invalidate(AppSourceInvalidatedEventArgs.ForPath(new AppSourcePathChange(WatcherChangeTypes.Created, @"C:\Apps\New.lnk")));
        if (flushWatcherFirst)
        {
            clock.Advance(TimeSpan.FromSeconds(5));
        }

        if (!backgroundFirst)
        {
            clock.Advance(TimeSpan.FromMinutes(10));
        }

        blocked.SetResult([CreateCatalogItem("Other", sourceId: "other")]);
        await WaitForConditionAsync(() => source.IncrementalLoadCount == 1 && !catalog.IsRefreshing);
        Assert.IsFalse(source.LastIncrementalBackground);
        Assert.AreEqual(1, source.LoadCount, "Queued reconciliation must not turn a watcher update into a full scan.");
        Assert.IsTrue(ContainsApp(catalog.GetSnapshot().Items, "New"));

        source.SetItems([CreateCatalogItem("Reconciled")]);
        clock.Advance(TimeSpan.FromSeconds(5));
        await WaitForConditionAsync(() => source.LoadCount == 2 && !catalog.IsRefreshing);
        Assert.IsTrue(source.LastLoadBackground);
        Assert.IsTrue(ContainsApp(catalog.GetSnapshot().Items, "Reconciled"));
    }

    private static AppCatalogItem CreateRecoveryItem(string name, string sourcePath, string? targetPath = null)
    {
        targetPath ??= $@"C:\Targets\{name}.exe";
        var payload = Win32AppPayload.From(TestDataHelper.CreateTestWin32Program(name, targetPath));
        payload = payload with { LnkFilePath = sourcePath };
        return new AppCatalogItem(
            $"win32:{targetPath}|args:",
            0,
            new AppCatalogSourceReference("test", sourcePath),
            [name],
            payload,
            identityAliases: [$"win32:{sourcePath}|args:"]);
    }

    private static AppCatalog CreateRecoveryCatalog(IReadOnlyList<IAppSource> sources, TimeProvider clock)
    {
        return new AppCatalog(new MutableSourceProvider(sources), new TestCache(null), new VisibleApps(), timeProvider: clock, invalidationDelay: TimeSpan.Zero);
    }

    private sealed class RecoveryTimeProvider : TimeProvider
    {
        private readonly Lock _lock = new();
        private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public List<RecoveryTimer> Timers { get; } = [];

        public int TimerCount
        {
            get
            {
                lock (_lock)
                {
                    return Timers.Count;
                }
            }
        }

        public override DateTimeOffset GetUtcNow()
        {
            lock (_lock)
            {
                return _now;
            }
        }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            lock (_lock)
            {
                var timer = new RecoveryTimer(this, callback, state);
                timer.Change(dueTime, period);
                Timers.Add(timer);
                return timer;
            }
        }

        public void Advance(TimeSpan elapsed)
        {
            RecoveryTimer[] timers;
            lock (_lock)
            {
                _now += elapsed;
                timers = [.. Timers];
            }

            foreach (var timer in timers)
            {
                timer.Fire();
            }
        }
    }

    private sealed class RecoveryTimer(RecoveryTimeProvider clock, TimerCallback callback, object? state) : ITimer
    {
        private readonly Lock _lock = new();
        private DateTimeOffset _dueAt = DateTimeOffset.MaxValue;
        private TimeSpan _period;

        public bool IsDisposed { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            var now = clock.GetUtcNow();
            lock (_lock)
            {
                _dueAt = dueTime == Timeout.InfiniteTimeSpan ? DateTimeOffset.MaxValue : now + dueTime;
                _period = period;
                return !IsDisposed;
            }
        }

        public void Fire()
        {
            var now = clock.GetUtcNow();
            lock (_lock)
            {
                if (IsDisposed || now < _dueAt)
                {
                    return;
                }

                _dueAt = _period == Timeout.InfiniteTimeSpan ? DateTimeOffset.MaxValue : now + _period;
            }

            callback(state);
        }

        public void Dispose()
        {
            lock (_lock)
            {
                IsDisposed = true;
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
