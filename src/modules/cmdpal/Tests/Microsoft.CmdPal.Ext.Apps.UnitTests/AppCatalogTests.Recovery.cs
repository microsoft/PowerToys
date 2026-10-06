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
using Microsoft.CmdPal.Ext.Apps.Catalog.Sources;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

public partial class AppCatalogTests
{
    [TestMethod]
    public async Task RefreshAsync_CompletesAfterItsCacheSaveWithoutWaitingForBackgroundRecovery()
    {
        var clock = new RecoveryTimeProvider();
        var item = CreateCatalogItem("Published");
        var backgroundLoad = new TaskCompletionSource<IReadOnlyList<AppCatalogItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cacheSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var source = new TestAppSource("test", new AppSourceScanResult([item], retryPaths: [string.Empty], failedPaths: []));
        var cache = new TestCache(null) { DeferredSave = cacheSave.Task };
        using var catalog = new AppCatalog(new MutableSourceProvider([source]), cache, new VisibleApps(), timeProvider: clock, invalidationDelay: TimeSpan.Zero);
        catalog.Changed += (_, _) =>
        {
            source.DeferNextLoad(backgroundLoad.Task);
            clock.Advance(TimeSpan.FromSeconds(5));
        };

        try
        {
            var refresh = catalog.RefreshAsync();
            await WaitForConditionAsync(() => cache.SaveCount == 1);
            Assert.IsFalse(refresh.IsCompleted, "The explicit request still waits for its cache save.");
            cacheSave.SetResult();
            await refresh.WaitAsync(TimeSpan.FromSeconds(5));
            await WaitForConditionAsync(() => source.LoadCount == 2);

            Assert.IsTrue(catalog.IsRefreshing, "Recovery continues independently after explicit refresh completion.");
            Assert.AreEqual("Published", catalog.GetSnapshot().Items.Single().Name);
            Assert.IsTrue(source.LastLoadBackground);
            backgroundLoad.SetResult([item]);
            await WaitForConditionAsync(() => !catalog.IsRefreshing);
        }
        finally
        {
            cacheSave.TrySetResult();
            backgroundLoad.TrySetResult([item]);
        }
    }

    [TestMethod]
    public async Task RefreshAsync_AnEarlierInFlightScanCannotCompleteANewerRequest()
    {
        var firstLoad = new TaskCompletionSource<IReadOnlyList<AppCatalogItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondLoad = new TaskCompletionSource<IReadOnlyList<AppCatalogItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var source = new TestAppSource("test", []);
        using var catalog = CreateCatalog([source], new TestCache(null));
        source.DeferNextLoad(firstLoad.Task);
        try
        {
            var firstRefresh = catalog.RefreshAsync();
            await WaitForConditionAsync(() => source.LoadCount == 1);
            source.DeferNextLoad(secondLoad.Task);
            var secondRefresh = catalog.RefreshAsync();
            firstLoad.SetResult([CreateCatalogItem("First")]);
            await firstRefresh.WaitAsync(TimeSpan.FromSeconds(5));
            await WaitForConditionAsync(() => source.LoadCount == 2);

            Assert.IsFalse(secondRefresh.IsCompleted, "Completion belongs to the queued request, not just its source ID.");
            Assert.AreEqual("First", catalog.GetSnapshot().Items.Single().Name);
            secondLoad.SetResult([CreateCatalogItem("Second")]);
            await secondRefresh.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.AreEqual("Second", catalog.GetSnapshot().Items.Single().Name);
            Assert.IsFalse(catalog.IsRefreshing);
        }
        finally
        {
            firstLoad.TrySetResult([]);
            secondLoad.TrySetResult([]);
        }
    }

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
    public async Task Recovery_PathLimitDoesNotStrandAReusedRejectedRetry(bool recovers)
    {
        const string path = @"C:\Apps\Rejected.lnk";
        var clock = new RecoveryTimeProvider();
        var queuedPaths = Enumerable.Range(0, 256).Select(index => $@"C:\Apps\Queued{index}.lnk").ToArray();
        var firstScan = new TaskCompletionSource<IReadOnlyList<AppCatalogItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var blocked = new TaskCompletionSource<IReadOnlyList<AppCatalogItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var blocker = new TestAppSource("other", []);
        using var source = new TestAppSource("test", new AppSourceScanResult([], retryPaths: queuedPaths));
        using var catalog = CreateRecoveryCatalog([source, blocker], clock);
        try
        {
            await catalog.InitializeAsync();
            await WaitForConditionAsync(() => !catalog.IsRefreshing);
            source.DeferNextLoad(firstScan.Task);
            blocker.DeferNextLoad(blocked.Task);
            source.Invalidate();
            await WaitForConditionAsync(() => source.LoadCount == 2);
            blocker.Invalidate();
            source.SetItems(new AppSourceScanResult([], reusedRejectedPaths: [path.ToUpperInvariant()]));
            source.SetIncrementalFactory(_ => recovers
                ? new AppSourceScanResult([CreateCatalogItem("Recovered")])
                : new AppSourceScanResult([], reusedRejectedPaths: [path.ToUpperInvariant()]));
            clock.Advance(TimeSpan.FromSeconds(5));
            firstScan.SetResult(new AppSourceScanResult([], retryPaths: [path], reusedRejectedPaths: queuedPaths.Skip(1).ToArray()));
            await WaitForConditionAsync(() => blocker.LoadCount == 2);
            clock.Advance(TimeSpan.FromSeconds(5));
            blocked.SetResult([]);
            await WaitForConditionAsync(() => source.LoadCount == 3 && !catalog.IsRefreshing);
            Assert.AreEqual(0, source.IncrementalLoadCount, "The omitted retry path promotes the full queue to a full scan.");
            Assert.IsTrue(source.LastLoadBackground);

            clock.Advance(TimeSpan.FromSeconds(15));
            await WaitForConditionAsync(() => source.IncrementalLoadCount == 1 && !catalog.IsRefreshing);
            Assert.AreEqual(path, source.LastIncrementalChanges.Single().Path);
            Assert.IsTrue(source.LastIncrementalBackground);
            if (recovers)
            {
                Assert.IsTrue(ContainsApp(catalog.GetSnapshot().Items, "Recovered"));
            }
            else
            {
                clock.Advance(TimeSpan.FromSeconds(40));
                await WaitForConditionAsync(() => source.IncrementalLoadCount == 2 && !catalog.IsRefreshing);
            }

            clock.Advance(TimeSpan.FromMinutes(3));
            Assert.AreEqual(recovers ? 1 : 2, source.IncrementalLoadCount, "Cached rejections must not renew the three-attempt budget.");
        }
        finally
        {
            firstScan.TrySetResult([]);
            blocked.TrySetResult([]);
        }
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task Recovery_CachedRejectionKeepsPendingRetriesAcrossReconciliation(bool targetAppears)
    {
        var root = Path.Combine(Path.GetTempPath(), $"cmdpal-cached-retry-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var shortcut = Path.Combine(root, "App.lnk");
            var target = Path.Combine(root, "App.exe");
            var reads = 0;
            var clock = new RecoveryTimeProvider();
            using var source = new Win32AppSource(
                new CustomDirectoryAppSource("retry", root, ["lnk"], Win32ProgramSourceProfile.None, maximumDepth: 0),
                (_, _) =>
                {
                    Interlocked.Increment(ref reads);
                    return File.Exists(target)
                        ? TestDataHelper.CreateTestWin32Metadata("Installed", target)
                        : new Win32AppMetadata { Valid = false };
                },
                createWatchers: false);
            var cache = new TestCache(null);
            using var catalog = new AppCatalog(new MutableSourceProvider([source]), cache, new VisibleApps(), timeProvider: clock, invalidationDelay: TimeSpan.Zero);
            await catalog.InitializeAsync();
            await WaitForConditionAsync(() => !catalog.IsRefreshing);

            clock.Advance(TimeSpan.FromMinutes(10) - TimeSpan.FromSeconds(1));
            File.WriteAllText(shortcut, "shortcut fixture");
            await catalog.RefreshAsync();
            Assert.AreEqual(1, reads);
            var savedBeforeReconciliation = cache.SaveCount;

            clock.Advance(TimeSpan.FromSeconds(1));
            await WaitForConditionAsync(() => cache.SaveCount > savedBeforeReconciliation && !catalog.IsRefreshing);
            Assert.AreEqual(1, reads, "Reconciliation must reuse the rejected shortcut without cancelling its retry.");
            if (targetAppears)
            {
                File.WriteAllText(target, "installed");
            }

            clock.Advance(TimeSpan.FromSeconds(4));
            await WaitForConditionAsync(() => reads == 2 && !catalog.IsRefreshing);
            if (targetAppears)
            {
                Assert.IsTrue(ContainsApp(catalog.GetSnapshot().Items, "Installed"));
                clock.Advance(TimeSpan.FromMinutes(2));
                Assert.AreEqual(2, reads, "A successful retry must clear the remaining attempts.");
            }
            else
            {
                clock.Advance(TimeSpan.FromSeconds(15));
                await WaitForConditionAsync(() => reads == 3 && !catalog.IsRefreshing);
                clock.Advance(TimeSpan.FromSeconds(40));
                await WaitForConditionAsync(() => reads == 4 && !catalog.IsRefreshing);
                savedBeforeReconciliation = cache.SaveCount;
                clock.Advance(TimeSpan.FromMinutes(10));
                await WaitForConditionAsync(() => cache.SaveCount > savedBeforeReconciliation && !catalog.IsRefreshing);
                clock.Advance(TimeSpan.FromMinutes(1));
                Assert.AreEqual(4, reads, "Reusing an exhausted rejection must not start another retry burst.");
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task Recovery_RetryMergedWithFullReconciliationStillReReadsItsShortcut(bool retryFirst)
    {
        var root = Path.Combine(Path.GetTempPath(), $"cmdpal-merged-retry-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var blocked = new TaskCompletionSource<IReadOnlyList<AppCatalogItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var rejected = Path.Combine(root, "Rejected.lnk");
            var unchanged = Path.Combine(root, "Unchanged.lnk");
            File.WriteAllText(unchanged, "shortcut fixture");
            var rejectedReads = 0;
            var unchangedReads = 0;
            var installed = false;
            var clock = new RecoveryTimeProvider();
            using var blocker = new TestAppSource("other", [CreateCatalogItem("Other", sourceId: "other")]);
            using var source = new Win32AppSource(
                new CustomDirectoryAppSource("retry", root, ["lnk"], Win32ProgramSourceProfile.None, maximumDepth: 0),
                (path, _) =>
                {
                    if (StringComparer.OrdinalIgnoreCase.Equals(path, rejected))
                    {
                        Interlocked.Increment(ref rejectedReads);
                        return installed
                            ? TestDataHelper.CreateTestWin32Metadata("Installed", Path.ChangeExtension(path, ".exe"))
                            : new Win32AppMetadata { Valid = false };
                    }

                    Interlocked.Increment(ref unchangedReads);
                    return TestDataHelper.CreateTestWin32Metadata("Unchanged", Path.ChangeExtension(path, ".exe"));
                },
                createWatchers: false);
            using var catalog = CreateRecoveryCatalog([blocker, source], clock);
            await catalog.InitializeAsync();
            await WaitForConditionAsync(() => !catalog.IsRefreshing);
            clock.Advance(TimeSpan.FromMinutes(10) - TimeSpan.FromSeconds(1));
            File.WriteAllText(rejected, "shortcut fixture");
            await catalog.RefreshAsync();
            Assert.AreEqual(1, rejectedReads);
            Assert.AreEqual(2, unchangedReads);
            blocker.DeferNextLoad(blocked.Task);
            blocker.Invalidate();
            await WaitForConditionAsync(() => blocker.LoadCount == 3);

            if (!retryFirst)
            {
                clock.Advance(TimeSpan.FromSeconds(1));
            }

            clock.Advance(TimeSpan.FromSeconds(retryFirst ? 5 : 4));
            installed = true;
            blocked.SetResult([CreateCatalogItem("Other", sourceId: "other")]);
            await WaitForConditionAsync(() => ContainsApp(catalog.GetSnapshot().Items, "Installed") && !catalog.IsRefreshing);

            Assert.AreEqual(2, rejectedReads, "The queued retry must survive either full-scan coalescing order.");
            Assert.AreEqual(2, unchangedReads, "The full background scan must reuse unrelated candidates.");
            clock.Advance(TimeSpan.FromMinutes(1));
            Assert.AreEqual(2, rejectedReads, "A successful retry must release its remaining retry budget.");
        }
        finally
        {
            blocked.TrySetResult([]);
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task Recovery_PublicationFailureDoesNotRescanSourcesOrBlockOtherPublications()
    {
        var clock = new RecoveryTimeProvider();
        var filter = new ThrowingCatalogFilter { FailingIdentity = "win32:Rejected" };
        var logger = new RecordingLogger<AppCatalog>();
        var cache = new TestCache(null);
        using var first = new TestAppSource("test", [CreateCatalogItem("Original")]);
        using var second = new TestAppSource("other", [CreateCatalogItem("Other", sourceId: "other")]);
        using var catalog = new AppCatalog(new MutableSourceProvider([first, second]), cache, new VisibleApps(), [filter], timeProvider: clock, invalidationDelay: TimeSpan.Zero, logger: logger);
        await catalog.InitializeAsync();
        await WaitForConditionAsync(() => !catalog.IsRefreshing);
        first.SetItems([CreateCatalogItem("Rejected")]);
        second.SetItems([CreateCatalogItem("Updated", sourceId: "other")]);

        await catalog.RefreshAsync().WaitAsync(TimeSpan.FromSeconds(5));

        string[] expectedNames = ["Original", "Updated"];
        CollectionAssert.AreEquivalent(expectedNames, catalog.GetSnapshot().Items.Select(item => item.Name).ToArray());
        Assert.IsTrue(logger.HasEvent(4));
        Assert.IsFalse(logger.HasEvent(2), "A publication failure is not a source read failure.");
        Assert.AreEqual("other", cache.LastFullyReconciledSourceIds.Single());
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.AreEqual(2, first.LoadCount);
        Assert.AreEqual(2, second.LoadCount);
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
        var payload = Win32AppPayload.From(TestDataHelper.CreateTestWin32Metadata(name, targetPath));
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
