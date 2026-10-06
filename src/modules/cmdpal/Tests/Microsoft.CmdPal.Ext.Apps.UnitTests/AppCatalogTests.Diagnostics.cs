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
    public async Task Diagnostics_SeparatesSetupScanPublicationWaitObserversAndCache()
    {
        var clock = new DiagnosticsTimeProvider();
        var logger = new RecordingLogger<AppCatalog>();
        var watcherSetup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstLoad = new TaskCompletionSource<IReadOnlyList<AppCatalogItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondLoad = new TaskCompletionSource<IReadOnlyList<AppCatalogItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cacheSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstItem = CreateCatalogItem("First", sourceId: "first");
        var secondItem = CreateCatalogItem("Second", sourceId: "second");
        using var first = new TestAppSource("first", [firstItem]);
        using var second = new TestAppSource("second", [secondItem]);
        first.DeferInitialization(watcherSetup.Task);
        first.DeferNextLoad(firstLoad.Task);
        second.DeferNextLoad(secondLoad.Task);
        var cache = new TestCache(null) { DeferredSave = cacheSave.Task };
        using var catalog = new AppCatalog(
            new MutableSourceProvider([first, second]),
            cache,
            new VisibleApps(),
            timeProvider: clock,
            invalidationDelay: TimeSpan.Zero,
            logger: logger,
            diagnosticsEnabled: () => true);
        catalog.Changed += (_, _) => clock.Advance(TimeSpan.FromMilliseconds(7));

        try
        {
            var initialization = catalog.InitializeAsync();
            await WaitForConditionAsync(() => first.InitializeCount == 1);
            clock.Advance(TimeSpan.FromMilliseconds(5));
            watcherSetup.SetResult();
            await WaitForConditionAsync(() => first.LoadCount == 1);
            clock.Advance(TimeSpan.FromMilliseconds(10));
            firstLoad.SetResult([firstItem]);
            await WaitForConditionAsync(() => second.LoadCount == 1);
            Assert.IsFalse(initialization.IsCompleted);
            Assert.AreEqual("First", catalog.GetSnapshot().Items.Single().Name);
            Assert.AreEqual(1, logger.GetEntries(16).Count);
            Assert.AreEqual(0, cache.SaveCount, "Source publication must not save the cache before the batch finishes.");
            clock.Advance(TimeSpan.FromMilliseconds(100));
            secondLoad.SetResult([secondItem]);
            await initialization;
            await WaitForConditionAsync(() => cache.SaveCount == 1);

            var loaded = logger.GetEntries(14);
            Assert.AreEqual(10.0, loaded.Single(entry => Equals(entry["SourceId"], "first"))["DurationMs"]);
            Assert.AreEqual(100.0, loaded.Single(entry => Equals(entry["SourceId"], "second"))["DurationMs"]);
            var started = logger.GetEntries(13);
            Assert.AreEqual(17.0, started.Single(entry => Equals(entry["SourceId"], "second"))["QueuedMs"]);
            var setup = logger.GetEntries(19).Single(entry => Equals(entry["SourceId"], "first") && Equals(entry["Phase"], "ready"));
            Assert.AreEqual(5.0, setup["DurationMs"]);

            var delayed = logger.GetEntries(17);
            Assert.AreEqual(0.0, delayed.Single(entry => Equals(entry["SourceId"], "first"))["DelayMs"]);
            Assert.AreEqual(0.0, delayed.Single(entry => Equals(entry["SourceId"], "second"))["DelayMs"]);
            var publications = logger.GetEntries(16);
            Assert.AreEqual(2, publications.Count);
            string[] expectedSources = ["first", "second"];
            CollectionAssert.AreEquivalent(expectedSources, publications.Select(entry => entry["SourceId"]).ToArray());
            Assert.IsTrue(publications.All(entry => Equals(0.0, entry["PublicationMs"]) && Equals(0.0, entry["BuildMs"])));
            Assert.IsTrue(publications.All(entry => Equals(7.0, entry["ObserverMs"])));
            Assert.IsTrue(loaded.All(entry => Equals(entry["BatchId"], publications[0]["BatchId"])));

            clock.Advance(TimeSpan.FromMilliseconds(3));
            cacheSave.SetResult();
            await WaitForConditionAsync(() => logger.HasEvent(18));
            var batch = logger.GetEntries(18).Single();
            Assert.AreEqual(3.0, batch["CacheMs"]);
            Assert.AreEqual(127.0, batch["DurationMs"]);
            Assert.AreEqual(true, batch["CacheAttempted"]);
            Assert.AreEqual(1, cache.SaveCount);
            Assert.AreEqual(2, catalog.GetSnapshot().Items.Count);
        }
        finally
        {
            watcherSetup.TrySetResult();
            firstLoad.TrySetResult([firstItem]);
            secondLoad.TrySetResult([secondItem]);
            cacheSave.TrySetResult();
        }
    }

    [TestMethod]
    public async Task Diagnostics_DisabledDoesNotReadTheClockOrLogTimingEvents()
    {
        var clock = new DiagnosticsTimeProvider();
        var logger = new RecordingLogger<AppCatalog>();
        using var source = new TestAppSource("source", [CreateCatalogItem("App")]);
        using var catalog = new AppCatalog(
            new MutableSourceProvider([source]),
            new TestCache(null),
            new VisibleApps(),
            timeProvider: clock,
            logger: logger);

        await catalog.InitializeAsync();
        await WaitForConditionAsync(() => !catalog.IsRefreshing);

        Assert.AreEqual(0, clock.TimestampReads);
        Assert.IsFalse(Enumerable.Range(12, 9).Any(logger.HasEvent));
        Assert.AreEqual(1, catalog.GetSnapshot().Items.Count);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Diagnostics_ReportsCachedRowsReuseAndActualLoadingCompletion(bool enabled)
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), $"cmdpal-row-diagnostics-{Guid.NewGuid():N}.json");
        File.WriteAllText(settingsPath, enabled ? "{\"apps.EnableCatalogDiagnostics\":\"true\"}" : "{}");
        try
        {
            var settings = new AllAppsSettings(settingsPath);
            var item = CreateCatalogItem("Cached", sourceId: "cached");
            using var source = new TestAppSource("cached", [item]);
            var cache = new TestCache(new AppCatalogCacheFile
            {
                Sources = [new AppCatalogSourceSnapshot { SourceId = source.Id, Items = [item] }],
            });
            var catalogLogger = new RecordingLogger<AppCatalog>();
            var rowLogger = new RecordingLogger<AppListItemSource>();
            using var catalog = new AppCatalog(
                new MutableSourceProvider([source]),
                cache,
                new MutableVisibilityStore(),
                logger: catalogLogger,
                diagnosticsEnabled: () => settings.EnableCatalogDiagnostics);
            using var rows = new AppListItemSource(catalog, settings, rowLogger);
            await catalog.InitializeAsync();
            await WaitForConditionAsync(() => !rows.IsLoading && (!enabled || rowLogger.HasEvent(5)));
            var originalRow = rows.GetSnapshot().VisibleItems.Single();
            await catalog.SetAppHiddenAsync(item.Identity, hidden: true);
            Assert.AreSame(originalRow, rows.GetSnapshot().HiddenItems.Single());
            await rows.RefreshAsync();

            if (enabled)
            {
                Assert.AreEqual(1, rowLogger.GetEntries(4).Count);
                var loading = rowLogger.GetEntries(5);
                Assert.AreEqual(2, loading.Count);
                Assert.AreEqual("startup", loading[0]["Kind"]);
                Assert.AreEqual("refresh", loading[1]["Kind"]);
                Assert.IsTrue(loading.All(entry => (double)entry["DurationMs"]! >= 0));
                var projections = rowLogger.GetEntries(3);
                Assert.AreEqual(1, projections[0]["CreatedCount"]);
                Assert.AreEqual(0, projections[^1]["CreatedCount"]);
                Assert.AreEqual(1, projections[^1]["ReusedCount"]);
                Assert.IsTrue(catalogLogger.HasEvent(20));
            }
            else
            {
                Assert.IsFalse(Enumerable.Range(3, 3).Any(rowLogger.HasEvent));
                Assert.IsFalse(catalogLogger.HasEvent(20));
            }
        }
        finally
        {
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Diagnostics_ReportsMetadataReuseWithoutExtraReads(bool enabled)
    {
        var root = Path.Combine(Path.GetTempPath(), $"cmdpal-index-diagnostics-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "App.exe");
            File.WriteAllText(path, "fixture");
            var loads = 0;
            var logger = new RecordingLogger<Win32AppSource>();
            var origin = new CustomDirectoryAppSource("probe", root, ["exe"], Win32ProgramSourceProfile.IncludeRawExecutables, 1);
            using var source = new Win32AppSource(
                origin,
                (candidate, _) =>
                {
                    Interlocked.Increment(ref loads);
                    return TestDataHelper.CreateTestWin32Program("App", candidate);
                },
                createWatchers: false,
                diagnosticsEnabled: () => enabled,
                logger: logger);
            var initial = await source.LoadAsync(CancellationToken.None);
            var unchanged = await source.LoadAsync(CancellationToken.None, background: true);

            Assert.AreEqual(1, loads);
            Assert.AreSame(initial.Single(), unchanged.Single());
            if (enabled)
            {
                var indexing = logger.GetEntries(5);
                Assert.AreEqual(1, indexing[0]["LoadedCount"]);
                Assert.AreEqual(0, indexing[0]["ReusedCount"]);
                Assert.AreEqual(0, indexing[1]["LoadedCount"]);
                Assert.AreEqual(1, indexing[1]["ReusedCount"]);
                Assert.AreEqual(true, indexing[1]["Background"]);
            }
            else
            {
                Assert.IsFalse(logger.HasEvent(5));
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class DiagnosticsTimeProvider : TimeProvider
    {
        private long _timestamp;
        private int _timestampReads;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public int TimestampReads => Volatile.Read(ref _timestampReads);

        public override long GetTimestamp()
        {
            Interlocked.Increment(ref _timestampReads);
            return Interlocked.Read(ref _timestamp);
        }

        public void Advance(TimeSpan elapsed)
        {
            Interlocked.Add(ref _timestamp, elapsed.Ticks);
        }
    }
}
