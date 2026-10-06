// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

using MEL = Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public partial class AppCatalogTests
{
    private static readonly TimeProvider TestTimeProvider = new FixedTimeProvider(
        new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero));

    [TestMethod]
    public void Provenance_CaseVariantsRemainAdjacentWhenNormalizingAndMerging()
    {
        var first = new AppCatalogSourceReference("A", "x");
        var second = new AppCatalogSourceReference("A", "y");
        var duplicate = new AppCatalogSourceReference("a", "X");
        var normalized = new AppCatalogProvenance(0, first, [first, second, duplicate]);
        var merged = new AppCatalogProvenance(0, first, [first, second])
            .Merge(new AppCatalogProvenance(0, duplicate));

        Assert.AreEqual(2, normalized.References.Length);
        Assert.AreEqual(2, merged.References.Length);
        Assert.IsTrue(normalized.HasSamePersistedContent(merged));
        Assert.AreEqual(first.SourceId, merged.References[0].SourceId);
        Assert.AreEqual(first.ItemId, merged.References[0].ItemId);
    }

    [TestMethod]
    public void AppCatalogItem_RequiresPayload()
    {
        Assert.ThrowsException<ArgumentNullException>(() => new AppCatalogItem(
            "win32:test",
            priority: 0,
            new AppCatalogSourceReference("test", "test"),
            [],
            payload: null!));
    }

    [TestMethod]
    public void AppCatalogItem_CopiesMutableMatchTerms()
    {
        var terms = new List<string> { "Original" };
        var program = TestDataHelper.CreateTestWin32Program("App", @"C:\Apps\App.exe");
        var item = new AppCatalogItem(
            "win32:app",
            priority: 0,
            new AppCatalogSourceReference("test", program.FullPath),
            terms,
            Win32AppPayload.From(program));

        terms.Add("Added later");

        Assert.AreEqual(1, item.MatchTerms.Length);
        Assert.AreEqual("Original", item.MatchTerms[0]);
    }

    [TestMethod]
    public void MergeProvenance_IsAssociative()
    {
        const string identity = "win32:overlap";
        var itemA = CreateCatalogItem("A", priority: 0, identity: identity, sourceId: "z");
        var itemB = CreateCatalogItem("B", priority: 1, identity: identity, sourceId: "a");
        var itemC = CreateCatalogItem("C", priority: 0, identity: identity, sourceId: "b");

        var leftGrouped = itemA.MergeProvenance(itemB).MergeProvenance(itemC);
        var rightGrouped = itemA.MergeProvenance(itemB.MergeProvenance(itemC));

        Assert.AreEqual("C", (leftGrouped.Payload as Win32AppPayload)?.Name);
        Assert.AreEqual("C", (rightGrouped.Payload as Win32AppPayload)?.Name);
        Assert.IsTrue(leftGrouped.HasSamePersistedContent(rightGrouped));
        Assert.AreEqual(3, leftGrouped.Provenance.References.Length);
        Assert.AreEqual(3, leftGrouped.MatchTerms.Length);
    }

    [TestMethod]
    public void HaveSamePersistedContent_IgnoresItemOrderAndIdentityCasing()
    {
        var first = CreateCatalogItem("First");
        var second = CreateCatalogItem("Second");
        var caseVariant = CreateCatalogItem("First", identity: first.Identity.ToUpperInvariant());

        Assert.IsTrue(AppCatalogItem.HaveSamePersistedContent([first, second], [second, caseVariant]));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void HaveSamePersistedContent_RejectsDuplicateIdentities(bool duplicatesOnLeft)
    {
        var first = CreateCatalogItem("First");
        var second = CreateCatalogItem("Second");
        AppCatalogItem[] unique = [first, second];
        AppCatalogItem[] duplicates = [first, CreateCatalogItem("First", identity: first.Identity.ToUpperInvariant())];

        Assert.IsFalse(duplicatesOnLeft
            ? AppCatalogItem.HaveSamePersistedContent(duplicates, unique)
            : AppCatalogItem.HaveSamePersistedContent(unique, duplicates));
    }

    [TestMethod]
    public void HaveSamePersistedContent_DetectsProvenanceChangesWithoutPreventingRowReuse()
    {
        var first = CreateCatalogItem("First");
        var updated = first.MergeProvenance(CreateCatalogItem("First", sourceId: "alternate"));

        Assert.IsTrue(first.CanReuseMaterializedApp(updated));
        Assert.IsFalse(AppCatalogItem.HaveSamePersistedContent([first], [updated]));
    }

    [TestMethod]
    public async Task InitializeAsync_LateCacheDoesNotReplaceRefreshedApps()
    {
        var cacheRead = new TaskCompletionSource<AppCatalogCacheFile?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cache = new TestCache(null) { DeferredLoad = cacheRead.Task };
        using var source = new TestAppSource("test", [CreateCatalogItem("Fresh")]);
        using var catalog = CreateCatalog([source], cache);
        var initialization = catalog.InitializeAsync();

        await catalog.RefreshAsync();
        cacheRead.SetResult(new AppCatalogCacheFile
        {
            Sources = [new AppCatalogSourceSnapshot { SourceId = "test", Items = [CreateCatalogItem("Cached")] }],
        });
        await initialization;

        Assert.AreEqual(1, catalog.GetSnapshot().Items.Count);
        Assert.AreEqual("Fresh", catalog.GetSnapshot().Items[0].Name);
    }

    [TestMethod]
    public async Task InitializeAsync_ValidCache_DoesNotWaitForSourceInitializationOrLoadSources()
    {
        var cachedItem = CreateCatalogItem("Cached");
        var cache = new TestCache(new AppCatalogCacheFile
        {
            Sources = [new AppCatalogSourceSnapshot { SourceId = "test", Items = [cachedItem] }],
        });
        using var source = new TestAppSource("test", [CreateCatalogItem("Fresh")]);
        using var catalog = CreateCatalog([source], cache);
        var sourceInitialization = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        source.DeferInitialization(sourceInitialization.Task);

        try
        {
            await catalog.InitializeAsync().WaitAsync(TimeSpan.FromSeconds(5));
            await WaitForConditionAsync(() => source.InitializeCount == 1);

            Assert.IsFalse(sourceInitialization.Task.IsCompleted);
            Assert.AreEqual(0, source.LoadCount);
            Assert.AreEqual("Cached", catalog.GetSnapshot().Items[0].Name);
        }
        finally
        {
            sourceInitialization.TrySetResult();
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task InitializeAsync_CachedNotificationFailure_DoesNotInterruptStartup(bool partialCache)
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), $"cmdpal-cached-notification-{Guid.NewGuid():N}.json");
        var settings = new AllAppsSettings(settingsPath);
        var cacheRead = new TaskCompletionSource<AppCatalogCacheFile?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cachedInitialization = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var missingLoad = new TaskCompletionSource<IReadOnlyList<AppCatalogItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cachedItem = CreateCatalogItem("Cached", sourceId: "cached");
        var cacheFile = new AppCatalogCacheFile
        {
            Sources = [new AppCatalogSourceSnapshot { SourceId = "cached", Items = [cachedItem] }],
        };
        using var cached = new TestAppSource("cached", [cachedItem]);
        using var missing = new TestAppSource("missing", []);
        cached.DeferInitialization(cachedInitialization.Task);
        missing.DeferNextLoad(missingLoad.Task);
        var logger = new RecordingLogger<AppCatalog>();
        IAppSource[] sources = partialCache ? [cached, missing] : [cached];
        using var catalog = CreateCatalog(sources, new TestCache(cacheFile) { DeferredLoad = cacheRead.Task }, logger: logger);
        try
        {
            using var list = new AppListItemSource(catalog, settings);
            catalog.Changed += (_, _) =>
            {
                throw new InvalidOperationException("Test cached notification failure.");
            };
            var initialization = catalog.InitializeAsync();
            cacheRead.SetResult(cacheFile);
            await WaitForConditionAsync(() => cached.InitializeCount == 1 && list.GetSnapshot().VisibleItems.Count == 1);

            Assert.AreEqual("Cached", list.GetSnapshot().VisibleItems.Single().Title);
            Assert.IsFalse(cachedInitialization.Task.IsCompleted);
            Assert.AreEqual(0, cached.LoadCount);
            Assert.IsTrue(logger.HasEvent(11));
            Assert.IsFalse(logger.HasEvent(4));
            if (partialCache)
            {
                await WaitForConditionAsync(() => missing.LoadCount == 1);
                Assert.AreEqual(1, missing.InitializeCount);
                Assert.IsTrue(list.IsLoading);
                await Assert.ThrowsExceptionAsync<TimeoutException>(() => initialization.WaitAsync(TimeSpan.FromMilliseconds(250)));
                missingLoad.SetResult([CreateCatalogItem("Fresh", sourceId: missing.Id)]);
            }

            await initialization.WaitAsync(TimeSpan.FromSeconds(5));
            await WaitForConditionAsync(() => !list.IsLoading);
            Assert.IsFalse(cachedInitialization.Task.IsCompleted, "Cached readiness must not wait for watcher setup or the delayed reconciliation.");
            Assert.AreEqual(partialCache ? 2 : 1, list.GetSnapshot().VisibleItems.Count);
            Assert.IsTrue(ContainsApp(catalog.GetSnapshot().Items, "Cached"));
            Assert.AreEqual(partialCache, ContainsApp(catalog.GetSnapshot().Items, "Fresh"));
            Assert.IsFalse(logger.HasEvent(4));
        }
        finally
        {
            cacheRead.TrySetResult(cacheFile);
            cachedInitialization.TrySetResult();
            missingLoad.TrySetResult([]);
            await settings.WaitForAliasSavesAsync();
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    public async Task InitializeAsync_ThrowingFirstListener_DeliversToProjectorAndLaterListener()
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), $"cmdpal-observer-isolation-{Guid.NewGuid():N}.json");
        var settings = new AllAppsSettings(settingsPath);
        var cacheRead = new TaskCompletionSource<AppCatalogCacheFile?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var observedTitle = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cache = new TestCache(null) { DeferredLoad = cacheRead.Task };
        var logger = new RecordingLogger<AppCatalog>();
        using var source = new TestAppSource("test", [CreateCatalogItem("Fresh")]);
        using var catalog = CreateCatalog([source], cache, logger: logger);
        catalog.Changed += (_, _) =>
        {
            throw new InvalidOperationException("Test first change listener failure.");
        };
        try
        {
            using var list = new AppListItemSource(catalog, settings);
            catalog.Changed += (_, _) =>
            {
                observedTitle.TrySetResult(list.GetSnapshot().VisibleItems.SingleOrDefault()?.Title);
            };
            var initialization = catalog.InitializeAsync();
            cacheRead.SetResult(null);

            await initialization.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.AreEqual("Fresh", await observedTitle.Task.WaitAsync(TimeSpan.FromSeconds(5)));
            await WaitForConditionAsync(() => !list.IsLoading && cache.SaveCount == 1);

            Assert.AreEqual("Fresh", list.GetSnapshot().VisibleItems.Single().Title);
            Assert.AreEqual("Fresh", ((Win32AppPayload)cache.LastSavedSnapshots![source.Id].Single().Payload).Name);
            Assert.IsTrue(logger.HasEvent(11));
            Assert.IsFalse(logger.HasEvent(4));
        }
        finally
        {
            cacheRead.TrySetResult(null);
            await settings.WaitForAliasSavesAsync();
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task InitializeAsync_RetiredSourcesDoNotLogInitializationFailures(bool initializedBeforeRetirement, bool useCache)
    {
        var firstInitialization = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var retiredInitialization = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstItem = CreateCatalogItem("First", sourceId: "first");
        using var first = new TestAppSource("first", [firstItem]);
        using var retired = new TestAppSource("retired", []);
        using var last = new TestAppSource("last", []);
        first.DeferInitialization(firstInitialization.Task);
        if (initializedBeforeRetirement)
        {
            retired.DeferInitialization(retiredInitialization.Task);
        }

        var cacheFile = new AppCatalogCacheFile
        {
            Sources =
            [
                new AppCatalogSourceSnapshot { SourceId = first.Id, Items = [firstItem] },
                new AppCatalogSourceSnapshot { SourceId = retired.Id, Items = [] },
                new AppCatalogSourceSnapshot { SourceId = last.Id, Items = [] },
            ],
        };
        var logger = new RecordingLogger<AppCatalog>();
        using var provider = new MutableSourceProvider([first, retired, last]);
        using var catalog = new AppCatalog(provider, new TestCache(useCache ? cacheFile : null), new VisibleApps(), logger: logger, invalidationDelay: TimeSpan.Zero);
        try
        {
            var initialization = catalog.InitializeAsync();
            await WaitForConditionAsync(() => first.InitializeCount == 1);
            if (initializedBeforeRetirement)
            {
                firstInitialization.SetResult();
                await WaitForConditionAsync(() => retired.InitializeCount == 1);
            }

            provider.SetSources([first, last]);
            await WaitForConditionAsync(() => retired.IsDisposed);
            if (initializedBeforeRetirement)
            {
                retiredInitialization.SetException(new ObjectDisposedException(retired.Id));
            }
            else
            {
                firstInitialization.SetResult();
            }

            await WaitForConditionAsync(() => last.InitializeCount == 1);
            await initialization.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.AreEqual(initializedBeforeRetirement ? 1 : 0, retired.InitializeCount);
            Assert.IsFalse(logger.HasEvent(1), "Expected source retirement must not log an initialization failure.");
            Assert.AreEqual("First", catalog.GetSnapshot().Items.Single().Name);
        }
        finally
        {
            firstInitialization.TrySetResult();
            retiredInitialization.TrySetResult();
        }
    }

    [TestMethod]
    public async Task InitializeAsync_ActiveSourceObjectDisposedExceptionIsLogged()
    {
        var logger = new RecordingLogger<AppCatalog>();
        using var source = new TestAppSource("active", [CreateCatalogItem("Fresh", sourceId: "active")]);
        source.DeferInitialization(Task.FromException(new ObjectDisposedException(source.Id)));
        using var catalog = CreateCatalog([source], new TestCache(null), logger: logger);

        await catalog.InitializeAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.IsFalse(source.IsDisposed);
        Assert.IsTrue(logger.HasEvent(1), "An active source's initialization failure must remain diagnosable.");
        Assert.AreEqual("Fresh", catalog.GetSnapshot().Items.Single().Name);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task InitializeAsync_ReplacedSourceWaitsForCurrentPublication(bool pendingRequest, bool partialCache)
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), $"cmdpal-startup-replacement-{Guid.NewGuid():N}.json");
        var settings = new AllAppsSettings(settingsPath);
        var earlierLoad = new TaskCompletionSource<IReadOnlyList<AppCatalogItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var originalLoad = new TaskCompletionSource<IReadOnlyList<AppCatalogItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var replacementInitialization = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var replacementLoad = new TaskCompletionSource<IReadOnlyList<AppCatalogItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var earlier = new TestAppSource("earlier", []);
        using var original = new TestAppSource("changing", []);
        using var replacement = new TestAppSource("changing", []);
        replacement.DeferInitialization(replacementInitialization.Task);
        replacement.DeferNextLoad(replacementLoad.Task);
        var cacheFile = new AppCatalogCacheFile
        {
            Sources = [new AppCatalogSourceSnapshot { SourceId = earlier.Id, Items = [] }],
        };
        using var provider = new MutableSourceProvider([earlier, original]);
        using var catalog = new AppCatalog(provider, new TestCache(partialCache ? cacheFile : null), new VisibleApps(), invalidationDelay: TimeSpan.Zero);
        try
        {
            if (pendingRequest)
            {
                earlier.DeferNextLoad(earlierLoad.Task);
                _ = catalog.RefreshAsync();
                await WaitForConditionAsync(() => earlier.LoadCount == 1);
            }
            else
            {
                original.DeferNextLoad(originalLoad.Task);
            }

            using var list = new AppListItemSource(catalog, settings);
            var initialization = catalog.InitializeAsync();
            await WaitForConditionAsync(() => pendingRequest ? original.InitializeCount == 1 : original.LoadCount == 1);
            await Assert.ThrowsExceptionAsync<TimeoutException>(() => initialization.WaitAsync(TimeSpan.FromMilliseconds(250)));

            provider.SetSources([earlier, replacement]);
            await WaitForConditionAsync(() => original.IsDisposed && replacement.InitializeCount == 1);
            earlierLoad.TrySetResult([]);
            originalLoad.TrySetResult([CreateCatalogItem("Obsolete", sourceId: original.Id)]);
            await WaitForConditionAsync(() => !catalog.IsRefreshing);

            Assert.IsTrue(list.IsLoading, "An unpublished replacement must retain startup loading even while the refresh loop is idle.");
            await Assert.ThrowsExceptionAsync<TimeoutException>(() => initialization.WaitAsync(TimeSpan.FromMilliseconds(250)));
            Assert.IsFalse(ContainsApp(catalog.GetSnapshot().Items, "Obsolete"));

            replacementInitialization.SetResult();
            await WaitForConditionAsync(() => replacement.LoadCount == 1);
            Assert.IsTrue(list.IsLoading);
            await Assert.ThrowsExceptionAsync<TimeoutException>(() => initialization.WaitAsync(TimeSpan.FromMilliseconds(250)));

            replacementLoad.SetResult([CreateCatalogItem("Replacement", sourceId: replacement.Id)]);
            await initialization.WaitAsync(TimeSpan.FromSeconds(5));
            await WaitForConditionAsync(() => !list.IsLoading);
            Assert.AreEqual("Replacement", list.GetSnapshot().VisibleItems.Single().Title);
        }
        finally
        {
            earlierLoad.TrySetResult([]);
            originalLoad.TrySetResult([]);
            replacementInitialization.TrySetResult();
            replacementLoad.TrySetResult([]);
            await settings.WaitForAliasSavesAsync();
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task InitializeAsync_RemovedSourceDoesNotWaitForItsRetiredScan(bool throwingListener)
    {
        var originalLoad = new TaskCompletionSource<IReadOnlyList<AppCatalogItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var removalNotification = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var original = new TestAppSource("removed", [CreateCatalogItem("Original", sourceId: "removed")]);
        using var provider = new MutableSourceProvider([original]);
        using var catalog = new AppCatalog(provider, new TestCache(null), new VisibleApps(), invalidationDelay: TimeSpan.Zero);
        try
        {
            if (throwingListener)
            {
                await catalog.RefreshAsync();
                catalog.Changed += (_, _) =>
                {
                    if (catalog.GetSnapshot().Items.Count == 0)
                    {
                        removalNotification.TrySetResult();
                        throw new InvalidOperationException("Test removal notification failure.");
                    }
                };
            }

            original.DeferNextLoad(originalLoad.Task);
            var initialLoadCount = original.LoadCount;
            var initialization = catalog.InitializeAsync();
            await WaitForConditionAsync(() => original.LoadCount == initialLoadCount + 1);
            provider.SetSources([]);

            await initialization.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsTrue(original.IsDisposed);
            Assert.IsFalse(originalLoad.Task.IsCompleted);
            Assert.AreEqual(0, catalog.GetSnapshot().Items.Count);
            if (throwingListener)
            {
                Assert.IsTrue(removalNotification.Task.IsCompletedSuccessfully);
            }
        }
        finally
        {
            originalLoad.TrySetResult([]);
            await WaitForConditionAsync(() => !catalog.IsRefreshing);
        }
    }

    [TestMethod]
    public async Task InitializeAsync_MixedRemovalAndReplacementContinuesAfterNotificationFailure()
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), $"cmdpal-startup-notification-{Guid.NewGuid():N}.json");
        var settings = new AllAppsSettings(settingsPath);
        var originalLoad = new TaskCompletionSource<IReadOnlyList<AppCatalogItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var replacementInitialization = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var replacementLoad = new TaskCompletionSource<IReadOnlyList<AppCatalogItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cachedItem = CreateCatalogItem("Cached", sourceId: "cached");
        using var cached = new TestAppSource("cached", [cachedItem]);
        using var original = new TestAppSource("changing", []);
        using var replacement = new TestAppSource("changing", []);
        original.DeferNextLoad(originalLoad.Task);
        replacement.DeferInitialization(replacementInitialization.Task);
        replacement.DeferNextLoad(replacementLoad.Task);
        var cacheFile = new AppCatalogCacheFile
        {
            Sources = [new AppCatalogSourceSnapshot { SourceId = cached.Id, Items = [cachedItem] }],
        };
        var logger = new RecordingLogger<AppCatalog>();
        using var provider = new MutableSourceProvider([cached, original]);
        using var catalog = new AppCatalog(provider, new TestCache(cacheFile), new VisibleApps(), logger: logger, invalidationDelay: TimeSpan.Zero);
        try
        {
            using var list = new AppListItemSource(catalog, settings);
            catalog.Changed += (_, _) =>
            {
                if (catalog.GetSnapshot().Items.Count == 0)
                {
                    throw new InvalidOperationException("Test removal notification failure.");
                }
            };
            var initialization = catalog.InitializeAsync();
            await WaitForConditionAsync(() => original.LoadCount == 1 && list.GetSnapshot().VisibleItems.Count == 1);

            provider.SetSources([replacement]);
            await WaitForConditionAsync(() => cached.IsDisposed && original.IsDisposed && replacement.InitializeCount == 1);
            Assert.IsTrue(logger.HasEvent(11), "A failing change listener must be logged without interrupting replacement initialization.");
            originalLoad.SetResult([CreateCatalogItem("Obsolete", sourceId: original.Id)]);
            await WaitForConditionAsync(() => !catalog.IsRefreshing);
            Assert.IsTrue(list.IsLoading);
            Assert.AreEqual(0, list.GetSnapshot().VisibleItems.Count);
            await Assert.ThrowsExceptionAsync<TimeoutException>(() => initialization.WaitAsync(TimeSpan.FromMilliseconds(250)));

            replacementInitialization.SetResult();
            await WaitForConditionAsync(() => replacement.LoadCount == 1);
            Assert.IsFalse(initialization.IsCompleted);
            Assert.IsTrue(list.IsLoading);
            replacementLoad.SetResult([CreateCatalogItem("Replacement", sourceId: replacement.Id)]);
            await initialization.WaitAsync(TimeSpan.FromSeconds(5));
            await WaitForConditionAsync(() => !list.IsLoading);
            Assert.AreEqual("Replacement", list.GetSnapshot().VisibleItems.Single().Title);
        }
        finally
        {
            originalLoad.TrySetResult([]);
            replacementInitialization.TrySetResult();
            replacementLoad.TrySetResult([]);
            await settings.WaitForAliasSavesAsync();
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task InitializeAsync_FirstPublicationCompletesWhileSourceInvalidationsContinue(bool partialCache)
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), $"cmdpal-startup-loading-{Guid.NewGuid():N}.json");
        var settings = new AllAppsSettings(settingsPath);
        var desktopLoad = new TaskCompletionSource<IReadOnlyList<AppCatalogItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var laterDesktopLoad = new TaskCompletionSource<IReadOnlyList<AppCatalogItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cachedSourceInitialization = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var cachedPackage = new AppCatalogItem(
                "packaged:Contoso.Cached_123!App",
                priority: 0,
                new AppCatalogSourceReference("packaged", "Contoso.Cached_123!App"),
                [],
                new PackagedAppSnapshot { Name = "Cached package", UserModelId = "Contoso.Cached_123!App" });
            var cacheFile = new AppCatalogCacheFile
            {
                Sources = [new AppCatalogSourceSnapshot { SourceId = "packaged", Items = [cachedPackage] }],
            };
            var cache = new TestCache(partialCache ? cacheFile : null);
            using var packagedSource = new TestAppSource("packaged", [cachedPackage]);
            using var desktopSource = new TestAppSource("win32:desktop", []);
            if (partialCache)
            {
                packagedSource.DeferInitialization(cachedSourceInitialization.Task);
            }

            desktopSource.DeferNextLoad(desktopLoad.Task);
            using var catalog = CreateCatalog([packagedSource, desktopSource], cache);
            var cachedPublication = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            catalog.Changed += (_, _) =>
            {
                if (ContainsApp(catalog.GetSnapshot().Items, "Cached package"))
                {
                    cachedPublication.TrySetResult();
                }

                if (desktopSource.LoadCount == 1 && ContainsApp(catalog.GetSnapshot().Items, "Fresh desktop"))
                {
                    desktopSource.DeferNextLoad(laterDesktopLoad.Task);
                    desktopSource.Invalidate();
                }
            };
            using var list = new AppListItemSource(catalog, settings);

            var initialization = catalog.InitializeAsync();
            if (partialCache)
            {
                await cachedPublication.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }

            await WaitForConditionAsync(() => desktopSource.LoadCount == 1 && list.GetSnapshot().VisibleItems.Count == 1);
            await Assert.ThrowsExceptionAsync<TimeoutException>(() => initialization.WaitAsync(TimeSpan.FromMilliseconds(250)));

            Assert.IsFalse(initialization.IsCompleted);
            Assert.IsTrue(list.IsLoading);
            if (partialCache)
            {
                Assert.AreEqual("Cached package", list.GetSnapshot().VisibleItems.Single().Title);
            }

            Assert.AreEqual(partialCache ? 0 : 1, packagedSource.LoadCount);
            for (var i = 0; i < 3; i++)
            {
                desktopSource.Invalidate();
            }

            desktopLoad.SetResult([CreateCatalogItem("Fresh desktop", sourceId: "desktop")]);
            await WaitForConditionAsync(() => desktopSource.LoadCount == 2);
            await initialization.WaitAsync(TimeSpan.FromSeconds(5));
            await WaitForConditionAsync(() => !list.IsLoading);

            Assert.IsTrue(catalog.IsRefreshing);
            Assert.IsFalse(laterDesktopLoad.Task.IsCompleted);
            var expectedTitles = new[] { "Cached package", "Fresh desktop" };
            CollectionAssert.AreEquivalent(
                expectedTitles,
                list.GetSnapshot().VisibleItems.Select(item => item.Title).ToArray());
            Assert.AreEqual(partialCache ? 0 : 1, packagedSource.LoadCount);

            laterDesktopLoad.SetResult([CreateCatalogItem("Later desktop", sourceId: "desktop")]);
            await WaitForConditionAsync(() => !catalog.IsRefreshing && list.GetSnapshot().VisibleItems.Any(item => item.Title == "Later desktop"));
        }
        finally
        {
            desktopLoad.TrySetResult([]);
            laterDesktopLoad.TrySetResult([]);
            cachedSourceInitialization.TrySetResult();
            await settings.WaitForAliasSavesAsync();
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    public async Task RefreshAsync_PreservesSourcesPublishedFromCacheWhileLoading()
    {
        var cacheRead = new TaskCompletionSource<AppCatalogCacheFile?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sourceLoad = new TaskCompletionSource<IReadOnlyList<AppCatalogItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cache = new TestCache(null) { DeferredLoad = cacheRead.Task };
        using var first = new TestAppSource("first", []);
        using var second = new TestAppSource("second", []);
        first.DeferNextLoad(sourceLoad.Task);
        second.DeferNextLoad(Task.FromException<IReadOnlyList<AppCatalogItem>>(new IOException("Unavailable source")));
        using var catalog = CreateCatalog([first, second], cache);
        var initialization = catalog.InitializeAsync();
        var refresh = catalog.RefreshAsync();
        await WaitForConditionAsync(() => first.LoadCount == 1);

        cacheRead.SetResult(new AppCatalogCacheFile
        {
            Sources =
            [
                new AppCatalogSourceSnapshot { SourceId = "first", Items = [CreateCatalogItem("CachedFirst")] },
                new AppCatalogSourceSnapshot { SourceId = "second", Items = [CreateCatalogItem("CachedSecond")] },
            ],
        });
        await initialization.WaitAsync(TimeSpan.FromSeconds(5));
        sourceLoad.SetResult([CreateCatalogItem("FreshFirst")]);
        await refresh.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.AreEqual(2, catalog.GetSnapshot().Items.Count);
        Assert.IsTrue(ContainsApp(catalog.GetSnapshot().Items, "FreshFirst"));
        Assert.IsTrue(ContainsApp(catalog.GetSnapshot().Items, "CachedSecond"));
        Assert.AreEqual(2, cache.LastSavedSnapshots!.Count);
        Assert.AreEqual("win32:CachedSecond", cache.LastSavedSnapshots["second"][0].Identity);
    }

    [TestMethod]
    public async Task PathInvalidation_WithoutSnapshotLoadsTheCompleteSource()
    {
        using var source = new TestAppSource("test", [CreateCatalogItem("First"), CreateCatalogItem("Second")]);
        source.DeferNextLoad(Task.FromException<IReadOnlyList<AppCatalogItem>>(new IOException("Initial load failed")));
        source.SetIncrementalFactory(_ => [CreateCatalogItem("First")]);
        var cache = new TestCache(null);
        using var catalog = CreateCatalog([source], cache);
        await catalog.InitializeAsync();
        Assert.AreEqual(0, catalog.GetSnapshot().Items.Count);

        source.Invalidate(AppSourceInvalidatedEventArgs.ForPath(new AppSourcePathChange(WatcherChangeTypes.Changed, @"C:\Apps\First.exe")));
        await WaitForConditionAsync(() => !catalog.IsRefreshing);

        Assert.AreEqual(2, source.LoadCount);
        Assert.AreEqual(0, source.IncrementalLoadCount);
        Assert.AreEqual(2, catalog.GetSnapshot().Items.Count);
        Assert.AreEqual(2, cache.LastSavedSnapshots!["test"].Count);
    }

    [TestMethod]
    public async Task RefreshAsync_DoesNotPublishAReplacedSourceAfterAnotherSourceFinishes()
    {
        using var first = new TestAppSource("first", [CreateCatalogItem("Original")]);
        using var second = new TestAppSource("second", []);
        using var replacement = new TestAppSource("first", [CreateCatalogItem("Replacement")]);
        using var provider = new MutableSourceProvider([first, second]);
        using var catalog = new AppCatalog(provider, new TestCache(null), new VisibleApps(), invalidationDelay: TimeSpan.Zero);
        await catalog.InitializeAsync();

        var secondLoad = new TaskCompletionSource<IReadOnlyList<AppCatalogItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        first.SetItems([CreateCatalogItem("Obsolete")]);
        second.DeferNextLoad(secondLoad.Task);
        var publishedObsolete = false;
        catalog.Changed += (_, _) => publishedObsolete |= ContainsApp(catalog.GetSnapshot().Items, "Obsolete");
        var refresh = catalog.RefreshAsync();
        await WaitForConditionAsync(() => second.LoadCount == 2);
        provider.SetSources([replacement, second]);
        await WaitForConditionAsync(() => first.IsDisposed);
        secondLoad.SetResult([]);
        await refresh.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitForConditionAsync(() => ContainsApp(catalog.GetSnapshot().Items, "Replacement"));

        Assert.IsFalse(publishedObsolete);
    }

    [TestMethod]
    public async Task InitializeAsync_NoCache_LoadsAndPublishesSources()
    {
        var cache = new TestCache(null);
        using var source = new TestAppSource("test", [CreateCatalogItem("Fresh")]);
        using var catalog = CreateCatalog([source], cache);

        await catalog.InitializeAsync();
        await WaitForConditionAsync(() => cache.SaveCount == 1);

        Assert.AreEqual(1, source.LoadCount);
        Assert.AreEqual("Fresh", catalog.GetSnapshot().Items[0].Name);
        Assert.AreEqual(1, cache.SaveCount);
    }

    [TestMethod]
    public async Task InitializeAsync_FirstPublicationDoesNotWaitForCachePersistence()
    {
        var cacheSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cache = new TestCache(null) { DeferredSave = cacheSave.Task };
        using var source = new TestAppSource("test", [CreateCatalogItem("Fresh")]);
        using var catalog = CreateCatalog([source], cache);
        try
        {
            await catalog.InitializeAsync().WaitAsync(TimeSpan.FromSeconds(5));
            await WaitForConditionAsync(() => cache.SaveCount == 1);

            Assert.AreEqual("Fresh", catalog.GetSnapshot().Items.Single().Name);
            Assert.IsTrue(catalog.IsRefreshing);
            Assert.IsFalse(cacheSave.Task.IsCompleted);
        }
        finally
        {
            cacheSave.TrySetResult();
        }
    }

    [TestMethod]
    public async Task InitializeAsync_DisposeReleasesFirstPublicationWait()
    {
        var sourceLoad = new TaskCompletionSource<IReadOnlyList<AppCatalogItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var source = new TestAppSource("test", []);
        source.DeferNextLoad(sourceLoad.Task);
        using var catalog = CreateCatalog([source], new TestCache(null));
        try
        {
            var initialization = catalog.InitializeAsync();
            await WaitForConditionAsync(() => source.LoadCount == 1);

            catalog.Dispose();
            await initialization.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsFalse(sourceLoad.Task.IsCompleted);
        }
        finally
        {
            sourceLoad.TrySetResult([]);
        }
    }

    [TestMethod]
    public async Task RefreshAsync_NotificationFailure_DoesNotSkipCachePersistence()
    {
        var cache = new TestCache(null);
        var logger = new RecordingLogger<AppCatalog>();
        using var source = new TestAppSource("test", [CreateCatalogItem("Original")]);
        using var catalog = CreateCatalog([source], cache, logger: logger);
        await catalog.RefreshAsync();
        source.SetItems([CreateCatalogItem("Fresh")]);
        catalog.Changed += (_, _) =>
        {
            throw new InvalidOperationException("Test refresh notification failure.");
        };

        await catalog.RefreshAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.AreEqual("Fresh", catalog.GetSnapshot().Items.Single().Name);
        Assert.AreEqual(2, cache.SaveCount);
        Assert.AreEqual("Fresh", ((Win32AppPayload)cache.LastSavedSnapshots![source.Id].Single().Payload).Name);
        Assert.IsTrue(logger.HasEvent(11));
        Assert.IsFalse(logger.HasEvent(4));
    }

    [TestMethod]
    public async Task GetSnapshot_ReusesTheCurrentAtomicPublication()
    {
        using var source = new TestAppSource("test", [CreateCatalogItem("First")]);
        using var catalog = CreateCatalog([source], new TestCache(null));
        await catalog.RefreshAsync();

        var firstSnapshot = catalog.GetSnapshot();

        Assert.AreSame(firstSnapshot, catalog.GetSnapshot());

        source.SetItems([CreateCatalogItem("Second")]);
        await catalog.RefreshAsync();
        var secondSnapshot = catalog.GetSnapshot();

        Assert.AreNotSame(firstSnapshot, secondSnapshot);
        Assert.AreEqual("First", firstSnapshot.Items[0].Name);
        Assert.AreEqual("Second", secondSnapshot.Items[0].Name);
    }

    [TestMethod]
    public async Task InitializeAsync_EmptySource_CachesAuthoritativeEmptySnapshot()
    {
        var cache = new TestCache(null);
        using var source = new TestAppSource("test", []);
        using var catalog = CreateCatalog([source], cache);

        await catalog.InitializeAsync();
        await WaitForConditionAsync(() => cache.SaveCount == 1);

        Assert.AreEqual(1, source.LoadCount);
        Assert.AreEqual(0, catalog.GetSnapshot().Items.Count);
        Assert.AreEqual(1, cache.SaveCount);
        Assert.IsTrue(cache.LastSavedSnapshots?.ContainsKey(source.Id) == true);
    }

    [TestMethod]
    public async Task RefreshAsync_OverlappingSources_PublishesPreferredItemOnce()
    {
        var lowPriority = CreateCatalogItem("Preferred", priority: 0, identity: "win32:app");
        var highPriority = CreateCatalogItem("Duplicate", priority: 20, identity: "win32:app");
        using var sourceA = new TestAppSource("a", [highPriority]);
        using var sourceB = new TestAppSource("b", [lowPriority]);
        using var catalog = CreateCatalog([sourceA, sourceB], new TestCache(null));

        await catalog.RefreshAsync();

        Assert.AreEqual(1, catalog.GetSnapshot().Items.Count);
        Assert.AreEqual("Preferred", catalog.GetSnapshot().Items[0].Name);
        Assert.AreEqual(2, catalog.GetSnapshot().Items[0].MatchTerms.Count);
        Assert.IsTrue(ContainsString(catalog.GetSnapshot().Items[0].MatchTerms, "Preferred"));
        Assert.IsTrue(ContainsString(catalog.GetSnapshot().Items[0].MatchTerms, "Duplicate"));
    }

    [TestMethod]
    public async Task RefreshAsync_ConflictingEqualPreferenceRepresentations_LogsAndSkipsConflict()
    {
        const string identity = "win32:conflict";
        const string itemId = @"C:\Apps\app.exe";
        var firstProgram = TestDataHelper.CreateTestWin32Program("First", itemId);
        var secondProgram = TestDataHelper.CreateTestWin32Program("Second", itemId);
        var sourceReference = new AppCatalogSourceReference("same-origin", itemId);
        var firstItem = new AppCatalogItem(identity, 0, sourceReference, ["First"], Win32AppPayload.From(firstProgram));
        var secondItem = new AppCatalogItem(identity, 0, sourceReference, ["Second"], Win32AppPayload.From(secondProgram));
        var logger = new RecordingLogger<AppCatalog>();
        using var firstSource = new TestAppSource("first", [firstItem]);
        using var secondSource = new TestAppSource("second", [secondItem]);
        using var catalog = CreateCatalog([firstSource, secondSource], new TestCache(null), logger: logger);

        await catalog.RefreshAsync();

        Assert.AreEqual(1, catalog.GetSnapshot().Items.Count);
        Assert.IsTrue(logger.HasEvent(10));
    }

    [TestMethod]
    [DataRow("ubuntu")]
    [DataRow("wt")]
    public async Task RefreshAsync_AppExecutionAlias_CoalescesPackagedAndExecutableRepresentations(string aliasName)
    {
        const string aumid = "CanonicalGroupLimited.Ubuntu_79rhkp1fndgsc!ubuntu";
        var aliasPath = $@"C:\Users\test\AppData\Local\Microsoft\WindowsApps\{aliasName}.exe";
        const string targetPath = @"C:\Program Files\WindowsApps\CanonicalGroupLimited.Ubuntu_1.0.0.0_x64__79rhkp1fndgsc\ubuntu.exe";
        var aliasProgram = CreateAppExecutionAliasProgram(aliasName, aliasPath, targetPath, aumid);
        var targetProgram = TestDataHelper.CreateTestWin32Program("ubuntu.exe", targetPath);
        var packagedIdentity = AppIdentity.ForPackaged(aumid);
        var aliasItem = CreateWin32CatalogItem(
            aliasProgram,
            identity: $"win32:{aliasPath}|args:",
            priority: 40,
            sourceId: "path");
        var targetItem = CreateWin32CatalogItem(
            targetProgram,
            identity: $"win32:{targetPath}|args:",
            priority: 1030,
            sourceId: "registry");
        var packagedItem = new AppCatalogItem(
            packagedIdentity,
            priority: 0,
            new AppCatalogSourceReference("packaged", aumid),
            ["Ubuntu"],
            new PackagedAppSnapshot
            {
                Name = "Ubuntu",
                UserModelId = aumid,
                PackageFullName = "CanonicalGroupLimited.Ubuntu_1.0.0.0_x64__79rhkp1fndgsc",
            });
        using var aliasSource = new TestAppSource("path", [aliasItem]);
        using var targetSource = new TestAppSource("registry", [targetItem]);
        using var packagedSource = new TestAppSource("packaged", [packagedItem]);
        using var catalog = CreateCatalog(
            [aliasSource, targetSource, packagedSource],
            new TestCache(null));

        await catalog.RefreshAsync();

        Assert.AreEqual(1, catalog.GetSnapshot().Items.Count);
        Assert.AreEqual("Ubuntu", catalog.GetSnapshot().Items[0].Name);
        Assert.AreEqual(packagedIdentity, catalog.GetSnapshot().Items[0].CatalogId);
        Assert.IsTrue(catalog.GetSnapshot().Items[0].IsPackaged);
        Assert.IsTrue(ContainsString(catalog.GetSnapshot().Items[0].MatchTerms, "ubuntu.exe"));
        Assert.IsTrue(ContainsString(catalog.GetSnapshot().Items[0].MatchTerms, "Ubuntu"));
        var row = new AppListItem(catalog.GetSnapshot().Items[0], useThumbnails: false);
        var matcher = new Microsoft.CmdPal.Common.Text.PrecomputedFuzzyMatcher();
        Assert.IsTrue(new AppSearch(aliasName, matcher, ExecutableNameMatchMode.FilenameAndStem).Evaluate(row).IsExactExecutableMatch);
        Assert.IsTrue(new AppSearch($"{aliasName}.exe", matcher, ExecutableNameMatchMode.FilenameAndStem).Evaluate(row).IsExactExecutableMatch);
        Assert.IsTrue(new AppSearch("ubuntu.exe", matcher, ExecutableNameMatchMode.FilenameAndStem).Evaluate(row).IsExactExecutableMatch);
        var snapshot = new AppListItemSnapshot([row], []);
        foreach (var representation in new[] { aliasItem, targetItem, packagedItem })
        {
            var id = new AppCommand(representation.ToAppItem()).Id;
            Assert.AreSame(row, snapshot.GetVisibleApp(id));
            Assert.AreEqual(id, snapshot.GetCommandItem(id)?.Command?.Id);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RefreshAsync_AppExecutionAliasWithoutPackagedItem_PrefersRunnableAlias(bool defaultWorkingDirectory)
    {
        const string aumid = "CanonicalGroupLimited.Ubuntu_79rhkp1fndgsc!ubuntu";
        const string aliasPath = @"C:\Users\test\AppData\Local\Microsoft\WindowsApps\ubuntu.exe";
        const string targetPath = @"C:\Program Files\WindowsApps\CanonicalGroupLimited.Ubuntu_1.0.0.0_x64__79rhkp1fndgsc\ubuntu.exe";
        var aliasProgram = CreateAppExecutionAliasProgram("ubuntu", aliasPath, targetPath, aumid);
        var targetProgram = TestDataHelper.CreateTestWin32Program("ubuntu.exe", targetPath);
        if (defaultWorkingDirectory)
        {
            aliasProgram.WorkingDirectory = Path.GetDirectoryName(targetPath)!;
            targetProgram.WorkingDirectory = Path.GetDirectoryName(targetPath)!;
        }

        var packagedIdentity = AppIdentity.ForPackaged(aumid);
        var aliasItem = CreateWin32CatalogItem(aliasProgram, "win32:alias", 40, "path");
        var targetItem = CreateWin32CatalogItem(targetProgram, "win32:target", 1030, "registry");
        using var aliasSource = new TestAppSource("path", [aliasItem]);
        using var targetSource = new TestAppSource("registry", [targetItem]);
        using var catalog = CreateCatalog([aliasSource, targetSource], new TestCache(null));

        await catalog.RefreshAsync();

        Assert.AreEqual(1, catalog.GetSnapshot().Items.Count);
        Assert.AreEqual("ubuntu", catalog.GetSnapshot().Items[0].Name);
        Assert.AreEqual(aliasPath, catalog.GetSnapshot().Items[0].ExePath);
        Assert.AreEqual(targetPath, catalog.GetSnapshot().Items[0].FullExecutablePath);
        Assert.AreEqual(aumid, catalog.GetSnapshot().Items[0].UserModelId);
        Assert.AreEqual(packagedIdentity, catalog.GetSnapshot().Items[0].CatalogId);
        Assert.IsFalse(catalog.GetSnapshot().Items[0].IsPackaged);

        var legacyCommand = new AppCommand(new AppItem
        {
            Name = aliasProgram.Name,
            Subtitle = aliasProgram.Description,
            ExePath = aliasPath,
        });
        var row = new AppListItem(catalog.GetSnapshot().Items[0], useThumbnails: false);
        var snapshot = new AppListItemSnapshot([row], []);
        Assert.AreNotEqual(legacyCommand.Id, row.Command!.Id);
        Assert.AreSame(row, snapshot.GetVisibleApp(legacyCommand.Id));
        Assert.AreEqual(legacyCommand.Id, snapshot.GetCommandItem(legacyCommand.Id)?.Command?.Id);
    }

    [TestMethod]
    public async Task RefreshAsync_PackagedShortcut_CoalescesWithPackagedApplication()
    {
        const string aumid = "Microsoft.Microsoft3DViewer_8wekyb3d8bbwe!Microsoft.Microsoft3DViewer";
        const string shortcutPath = @"C:\Users\test\Desktop\3D Viewer - Shortcut.lnk";
        var shortcutProgram = TestDataHelper.CreateTestWin32Program("3D Viewer - Shortcut", shortcutPath);
        shortcutProgram.AppType = Win32Program.ApplicationType.ShortcutApplication;
        shortcutProgram.PackagedAppUserModelId = aumid;
        var shortcutItem = CreateWin32CatalogItem(shortcutProgram, "win32:3d-viewer-shortcut", 20, "desktop");
        var packagedIdentity = AppIdentity.ForPackaged(aumid);
        var packagedItem = new AppCatalogItem(
            packagedIdentity,
            priority: 0,
            new AppCatalogSourceReference("packaged", aumid),
            ["3D Viewer"],
            new PackagedAppSnapshot
            {
                Name = "3D Viewer",
                UserModelId = aumid,
                PackageFullName = "Microsoft.Microsoft3DViewer_1.0.0.0_x64__8wekyb3d8bbwe",
            });
        using var shortcutSource = new TestAppSource("desktop", [shortcutItem]);
        using var packagedSource = new TestAppSource("packaged", [packagedItem]);
        using var catalog = CreateCatalog([shortcutSource, packagedSource], new TestCache(null));

        await catalog.RefreshAsync();

        Assert.AreEqual(1, catalog.GetSnapshot().Items.Count);
        Assert.AreEqual("3D Viewer", catalog.GetSnapshot().Items[0].Name);
        Assert.AreEqual(packagedIdentity, catalog.GetSnapshot().Items[0].CatalogId);
        Assert.IsTrue(catalog.GetSnapshot().Items[0].IsPackaged);
        Assert.IsTrue(ContainsString(catalog.GetSnapshot().Items[0].MatchTerms, "3D Viewer - Shortcut"));
    }

    [TestMethod]
    [DataRow("--profile work", "", false)]
    [DataRow("", @"C:\Projects\Work", false)]
    [DataRow("", @"C:\Projects\Work", true)]
    public async Task RefreshAsync_TargetWithLaunchSettings_DoesNotCoalesceWithAppExecutionAlias(string arguments, string workingDirectory, bool hasPackagedIdentity)
    {
        const string aumid = "Contoso.App_123!app";
        const string aliasPath = @"C:\Users\test\AppData\Local\Microsoft\WindowsApps\app.exe";
        const string targetPath = @"C:\Program Files\WindowsApps\Contoso.App_1.0.0.0_x64__123\app.exe";
        var aliasProgram = CreateAppExecutionAliasProgram("app", aliasPath, targetPath, aumid);
        var targetProgram = TestDataHelper.CreateTestWin32Program("App profile", targetPath);
        targetProgram.Arguments = arguments;
        targetProgram.WorkingDirectory = workingDirectory;
        targetProgram.PackagedAppUserModelId = hasPackagedIdentity ? aumid : string.Empty;
        var aliasItem = CreateWin32CatalogItem(aliasProgram, "win32:alias", 40, "path");
        var targetItem = CreateWin32CatalogItem(targetProgram, "win32:target-with-arguments", 1030, "start-menu");
        using var aliasSource = new TestAppSource("path", [aliasItem]);
        using var targetSource = new TestAppSource("start-menu", [targetItem]);
        using var catalog = CreateCatalog([aliasSource, targetSource], new TestCache(null));

        await catalog.RefreshAsync();

        Assert.AreEqual(2, catalog.GetSnapshot().Items.Count);
        Assert.IsTrue(ContainsApp(catalog.GetSnapshot().Items, "app"));
        Assert.IsTrue(ContainsApp(catalog.GetSnapshot().Items, "App profile"));
    }

    [TestMethod]
    public async Task RefreshAsync_AmbiguousAliasTarget_DoesNotCoalesceExecutable()
    {
        const string targetPath = @"C:\Program Files\WindowsApps\Contoso.Shared\app.exe";
        var firstAlias = CreateAppExecutionAliasProgram(
            "First alias",
            @"C:\Users\test\AppData\Local\Microsoft\WindowsApps\first.exe",
            targetPath,
            "Contoso.First_123!app");
        var secondAlias = CreateAppExecutionAliasProgram(
            "Second alias",
            @"C:\Users\test\AppData\Local\Microsoft\WindowsApps\second.exe",
            targetPath,
            "Contoso.Second_123!app");
        var targetProgram = TestDataHelper.CreateTestWin32Program("Shared executable", targetPath);
        using var firstSource = new TestAppSource(
            "first-path",
            [CreateWin32CatalogItem(firstAlias, "win32:first-alias", 40, "first-path")]);
        using var secondSource = new TestAppSource(
            "second-path",
            [CreateWin32CatalogItem(secondAlias, "win32:second-alias", 40, "second-path")]);
        using var targetSource = new TestAppSource(
            "registry",
            [CreateWin32CatalogItem(targetProgram, "win32:target", 1030, "registry")]);
        using var catalog = CreateCatalog([firstSource, secondSource, targetSource], new TestCache(null));

        await catalog.RefreshAsync();

        Assert.AreEqual(3, catalog.GetSnapshot().Items.Count);
        Assert.IsTrue(ContainsApp(catalog.GetSnapshot().Items, "First alias"));
        Assert.IsTrue(ContainsApp(catalog.GetSnapshot().Items, "Second alias"));
        Assert.IsTrue(ContainsApp(catalog.GetSnapshot().Items, "Shared executable"));
    }

    [TestMethod]
    public async Task RefreshAsync_KeepsPreviousSnapshotUntilRefreshCompletes()
    {
        var cache = new TestCache(new AppCatalogCacheFile
        {
            Sources = [new AppCatalogSourceSnapshot { SourceId = "test", Items = [CreateCatalogItem("Old")] }],
        });
        using var source = new TestAppSource("test", []);
        using var catalog = CreateCatalog([source], cache);
        await catalog.InitializeAsync();

        var completion = new TaskCompletionSource<IReadOnlyList<AppCatalogItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        source.DeferNextLoad(completion.Task);
        var refresh = catalog.RefreshAsync();

        Assert.IsTrue(catalog.IsRefreshing);
        Assert.AreEqual("Old", catalog.GetSnapshot().Items[0].Name);

        completion.SetResult([CreateCatalogItem("New")]);
        await refresh;

        Assert.IsFalse(catalog.IsRefreshing);
        Assert.AreEqual("New", catalog.GetSnapshot().Items[0].Name);
    }

    [TestMethod]
    public async Task RefreshCompletion_DoesNotClearRefreshStartedByCompletionSubscriber()
    {
        using var source = new TestAppSource("test", [CreateCatalogItem("First")]);
        using var catalog = CreateCatalog([source], new TestCache(null));
        var secondCompletion = new TaskCompletionSource<IReadOnlyList<AppCatalogItem>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Task? secondRefresh = null;
        catalog.RefreshStateChanged += (_, _) =>
        {
            if (!catalog.IsRefreshing && secondRefresh is null)
            {
                source.DeferNextLoad(secondCompletion.Task);
                secondRefresh = catalog.RefreshAsync();
            }
        };

        var firstRefresh = catalog.RefreshAsync();
        await firstRefresh;
        await WaitForConditionAsync(() => source.LoadCount == 2);

        Assert.IsNotNull(secondRefresh);
        Assert.IsTrue(catalog.IsRefreshing);

        secondCompletion.SetResult([CreateCatalogItem("Second")]);
        await secondRefresh;
        Assert.IsFalse(catalog.IsRefreshing);
    }

    [TestMethod]
    public async Task RefreshFailure_PreservesRefreshQueuedByFailingSubscriber()
    {
        using var source = new TestAppSource("test", [CreateCatalogItem("First")]);
        using var catalog = CreateCatalog([source], new TestCache(null));
        Task? queuedRefresh = null;
        var failOnce = true;
        catalog.Changed += (_, _) =>
        {
            if (!failOnce)
            {
                return;
            }

            failOnce = false;
            source.SetItems([CreateCatalogItem("Second")]);
            queuedRefresh = catalog.RefreshAsync();
            throw new InvalidOperationException("Test subscriber failure");
        };

        var refresh = catalog.RefreshAsync();
        await refresh;

        Assert.IsNotNull(queuedRefresh);
        Assert.AreNotSame(refresh, queuedRefresh);
        await queuedRefresh;
        Assert.AreEqual(2, source.LoadCount);
        Assert.AreEqual("Second", catalog.GetSnapshot().Items[0].Name);
        Assert.IsFalse(catalog.IsRefreshing);
    }

    [TestMethod]
    public async Task RefreshAsync_CacheFailureDoesNotFaultOrBlockLaterRefresh()
    {
        var cache = new TestCache(null);
        cache.FailNextSave(new InvalidOperationException("Test cache failure"));
        using var source = new TestAppSource("test", [CreateCatalogItem("First")]);
        using var catalog = CreateCatalog([source], cache);

        await catalog.RefreshAsync();

        Assert.AreEqual("First", catalog.GetSnapshot().Items[0].Name);
        Assert.IsFalse(catalog.IsRefreshing);

        source.SetItems([CreateCatalogItem("Second")]);
        await catalog.RefreshAsync();

        Assert.AreEqual(2, source.LoadCount);
        Assert.AreEqual(2, cache.SaveCount);
        Assert.AreEqual("Second", catalog.GetSnapshot().Items[0].Name);
        Assert.IsFalse(catalog.IsRefreshing);
    }

    [TestMethod]
    public async Task SourceInvalidation_RefreshesOnlyInvalidatedSource()
    {
        using var sourceA = new TestAppSource("a", [CreateCatalogItem("A")]);
        using var sourceB = new TestAppSource("b", [CreateCatalogItem("B")]);
        using var catalog = CreateCatalog([sourceA, sourceB], new TestCache(null));
        await catalog.RefreshAsync();
        sourceA.SetItems([CreateCatalogItem("A2")]);

        sourceA.Invalidate();
        await WaitForConditionAsync(() => sourceA.LoadCount == 2 && !catalog.IsRefreshing);

        Assert.AreEqual(2, sourceA.LoadCount);
        Assert.AreEqual(1, sourceB.LoadCount);
        Assert.IsTrue(ContainsApp(catalog.GetSnapshot().Items, "A2"));
        Assert.IsTrue(ContainsApp(catalog.GetSnapshot().Items, "B"));
    }

    [TestMethod]
    public async Task SourceProviderChange_RefreshesOnlyReplacedSource()
    {
        var original = new TestAppSource("a", [CreateCatalogItem("A")]);
        var unchanged = new TestAppSource("b", [CreateCatalogItem("B")]);
        var provider = new MutableSourceProvider([original, unchanged]);
        using var catalog = new AppCatalog(
            provider,
            new TestCache(null),
            new VisibleApps(),
            invalidationDelay: TimeSpan.Zero);
        await catalog.RefreshAsync();
        var replacement = new TestAppSource("a", [CreateCatalogItem("A2")]);

        provider.SetSources([replacement, unchanged]);
        await WaitForConditionAsync(() => replacement.LoadCount == 1 && !catalog.IsRefreshing);

        Assert.AreEqual(1, original.LoadCount);
        Assert.AreEqual(1, unchanged.LoadCount);
        Assert.IsTrue(ContainsApp(catalog.GetSnapshot().Items, "A2"));
        Assert.IsTrue(ContainsApp(catalog.GetSnapshot().Items, "B"));

        provider.SetSources([unchanged]);
        await WaitForConditionAsync(() => !ContainsApp(catalog.GetSnapshot().Items, "A2"));
        Assert.IsTrue(ContainsApp(catalog.GetSnapshot().Items, "B"));
    }

    [TestMethod]
    public async Task RefreshAsync_EquivalentSnapshot_DoesNotRaiseCatalogChanged()
    {
        using var source = new TestAppSource("test", [CreateCatalogItem("Same")]);
        using var catalog = CreateCatalog([source], new TestCache(null));
        await catalog.RefreshAsync();
        var changedCount = 0;
        catalog.Changed += (_, _) => changedCount++;
        source.SetItems([CreateCatalogItem("Same")]);

        await catalog.RefreshAsync();

        Assert.AreEqual(0, changedCount);
    }

    [TestMethod]
    public async Task RefreshAsync_ProvenanceOnlyChange_DoesNotRaiseCatalogChanged()
    {
        var initial = CreateCatalogItem("Same");
        using var source = new TestAppSource("test", [initial]);
        var cache = new TestCache(null);
        using var catalog = CreateCatalog([source], cache);
        await catalog.RefreshAsync();
        var originalApp = catalog.GetSnapshot().Items[0];
        var changedCount = 0;
        catalog.Changed += (_, _) => changedCount++;
        var updated = CreateCatalogItem("Same");
        var alternateRepresentation = new AppCatalogItem(
            updated.Identity,
            priority: 10,
            new AppCatalogSourceReference("alternate", @"C:\Alternate\Same.lnk"),
            updated.MatchTerms,
            updated.Payload);
        updated = updated.MergeProvenance(alternateRepresentation);
        source.SetItems([updated]);

        await catalog.RefreshAsync();

        Assert.AreEqual(0, changedCount);
        Assert.AreSame(originalApp, catalog.GetSnapshot().Items[0]);
        Assert.AreEqual(2, cache.SaveCount);
    }

    [TestMethod]
    public async Task IncrementalInvalidationDuringFullRefresh_UsesCommittedFullSnapshot()
    {
        using var source = new TestAppSource("test", [CreateCatalogItem("Initial")]);
        using var catalog = CreateCatalog([source], new TestCache(null));
        await catalog.RefreshAsync();
        var fullRefreshCompletion = new TaskCompletionSource<IReadOnlyList<AppCatalogItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        source.DeferNextLoad(fullRefreshCompletion.Task);
        source.SetIncrementalFactory(currentItems =>
        {
            var updated = new List<AppCatalogItem>(currentItems)
            {
                CreateCatalogItem("Incremental"),
            };
            return updated;
        });

        var fullRefresh = catalog.RefreshAsync();
        await WaitForConditionAsync(() => source.LoadCount == 2);
        source.Invalidate(
            AppSourceInvalidatedEventArgs.ForPath(
                new AppSourcePathChange(WatcherChangeTypes.Changed, @"C:\Apps\Incremental.lnk")));
        fullRefreshCompletion.SetResult([CreateCatalogItem("Full")]);
        await fullRefresh;
        await WaitForConditionAsync(() => source.IncrementalLoadCount == 1 && !catalog.IsRefreshing);

        Assert.AreEqual(1, source.IncrementalLoadCount);
        Assert.IsFalse(catalog.IsRefreshing);
        Assert.AreEqual("Full", (source.LastIncrementalBaseItems[0].Payload as Win32AppPayload)?.Name);
        Assert.IsTrue(ContainsApp(catalog.GetSnapshot().Items, "Full"));
        Assert.IsTrue(ContainsApp(catalog.GetSnapshot().Items, "Incremental"));
        Assert.IsFalse(ContainsApp(catalog.GetSnapshot().Items, "Initial"));
    }

    [TestMethod]
    public async Task SourceInvalidationFlood_CoalescesSameDirtyPathWithoutTrustingEventOrder()
    {
        const string path = @"C:\Apps\Portable.exe";
        using var source = new TestAppSource("test", [CreateCatalogItem("Initial")]);
        using var catalog = CreateCatalog([source], new TestCache(null));
        await catalog.RefreshAsync();
        var fullRefreshCompletion = new TaskCompletionSource<IReadOnlyList<AppCatalogItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        source.DeferNextLoad(fullRefreshCompletion.Task);
        source.SetIncrementalFactory(currentItems => currentItems);

        var fullRefresh = catalog.RefreshAsync();
        await WaitForConditionAsync(() => source.LoadCount == 2);
        for (var i = 0; i < 100; i++)
        {
            source.Invalidate(AppSourceInvalidatedEventArgs.ForPath(new AppSourcePathChange(WatcherChangeTypes.Created, path)));
            source.Invalidate(AppSourceInvalidatedEventArgs.ForPath(new AppSourcePathChange(WatcherChangeTypes.Deleted, path)));
        }

        fullRefreshCompletion.SetResult([CreateCatalogItem("Full")]);
        await fullRefresh;
        await WaitForConditionAsync(() => source.IncrementalLoadCount == 1 && !catalog.IsRefreshing);

        Assert.AreEqual(1, source.IncrementalLoadCount);
        Assert.AreEqual(1, source.LastIncrementalChanges.Count);
        Assert.AreEqual(path, source.LastIncrementalChanges[0].Path);
    }

    [TestMethod]
    public async Task SourceInvalidationFlood_DistinctPathsPromoteOnlyThatSourceToFullRefresh()
    {
        using var sourceA = new TestAppSource("a", [CreateCatalogItem("Initial A")]);
        using var sourceB = new TestAppSource("b", [CreateCatalogItem("Initial B")]);
        using var catalog = CreateCatalog([sourceA, sourceB], new TestCache(null));
        await catalog.RefreshAsync();
        var fullRefreshCompletion = new TaskCompletionSource<IReadOnlyList<AppCatalogItem>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        sourceA.DeferNextLoad(fullRefreshCompletion.Task);

        var runningFullRefresh = catalog.RefreshAsync();
        await WaitForConditionAsync(() => sourceA.LoadCount == 2);
        for (var index = 0; index <= 256; index++)
        {
            sourceA.Invalidate(
                AppSourceInvalidatedEventArgs.ForPath(
                    new AppSourcePathChange(WatcherChangeTypes.Changed, $@"C:\Apps\Changed-{index}.lnk")));
        }

        sourceA.SetItems([CreateCatalogItem("After flood")]);
        fullRefreshCompletion.SetResult([CreateCatalogItem("First full")]);
        await runningFullRefresh;
        await WaitForConditionAsync(() => !catalog.IsRefreshing && ContainsApp(catalog.GetSnapshot().Items, "After flood"));

        Assert.AreEqual(3, sourceA.LoadCount);
        Assert.AreEqual(0, sourceA.IncrementalLoadCount);
        Assert.AreEqual(2, sourceB.LoadCount);
        Assert.IsTrue(ContainsApp(catalog.GetSnapshot().Items, "After flood"));
    }

    [TestMethod]
    public async Task SetAppHidden_MovesExistingItemWithoutRebuildingCatalog()
    {
        using var source = new TestAppSource("test", [CreateCatalogItem("Notepad", identity: "win32:notepad")]);
        var visibility = new MutableVisibilityStore();
        using var catalog = new AppCatalog(
            new MutableSourceProvider([source]),
            new TestCache(null),
            visibility,
            invalidationDelay: TimeSpan.Zero);
        await catalog.RefreshAsync();
        var originalItem = catalog.GetSnapshot().Items[0];
        var catalogId = originalItem.CatalogId;
        var catalogChangedCount = 0;
        var visibilityChangedCount = 0;
        catalog.Changed += (_, _) => catalogChangedCount++;
        catalog.VisibilityChanged += (_, _) => visibilityChangedCount++;

        await catalog.SetAppHiddenAsync(catalogId, hidden: true);

        Assert.AreEqual(1, source.LoadCount);
        Assert.AreEqual(0, catalog.GetSnapshot().Items.Count);
        Assert.AreSame(originalItem, catalog.GetSnapshot().HiddenItems[0]);
        Assert.AreEqual(0, catalogChangedCount);
        Assert.AreEqual(1, visibilityChangedCount);
        Assert.AreEqual(1, visibility.PersistCount);

        await catalog.SetAppHiddenAsync(catalogId, hidden: false);

        Assert.AreEqual(1, source.LoadCount);
        Assert.AreSame(originalItem, catalog.GetSnapshot().Items[0]);
        Assert.AreEqual(0, catalog.GetSnapshot().HiddenItems.Count);
        Assert.AreEqual(0, catalogChangedCount);
        Assert.AreEqual(2, visibilityChangedCount);
        Assert.AreEqual(2, visibility.PersistCount);
    }

    [TestMethod]
    public async Task SetAppHidden_ThrowingFirstListener_DeliversLaterNotificationsAndPersists()
    {
        var item = CreateCatalogItem("Notepad", identity: "win32:notepad");
        var visibility = new MutableVisibilityStore();
        var logger = new RecordingLogger<AppCatalog>();
        using var source = new TestAppSource("test", [item]);
        using var catalog = CreateCatalog([source], new TestCache(null), visibility, logger: logger);
        await catalog.RefreshAsync();
        var originalItem = catalog.GetSnapshot().Items.Single();
        var catalogId = originalItem.CatalogId;
        var notifications = new List<AppVisibilityChangedEventArgs>();
        catalog.VisibilityChanged += (_, _) =>
        {
            throw new InvalidOperationException("Test first visibility listener failure.");
        };
        catalog.VisibilityChanged += (_, args) => notifications.Add(args);

        await catalog.SetAppHiddenAsync(catalogId, hidden: true).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.AreEqual(0, catalog.GetSnapshot().Items.Count);
        Assert.AreSame(originalItem, catalog.GetSnapshot().HiddenItems.Single());
        Assert.AreEqual(AppVisibility.Hidden, visibility.GetVisibility(item));
        Assert.AreEqual(1, visibility.PersistCount);
        Assert.AreEqual(1, notifications.Count);
        Assert.AreEqual(catalogId, notifications[0].CatalogId);
        Assert.IsTrue(notifications[0].Hidden);
        Assert.IsTrue(logger.HasEvent(11));

        await catalog.SetAppHiddenAsync(catalogId, hidden: false).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.AreSame(originalItem, catalog.GetSnapshot().Items.Single());
        Assert.AreEqual(0, catalog.GetSnapshot().HiddenItems.Count);
        Assert.AreEqual(AppVisibility.Visible, visibility.GetVisibility(item));
        Assert.AreEqual(2, visibility.PersistCount);
        Assert.AreEqual(2, notifications.Count);
        Assert.AreEqual(catalogId, notifications[1].CatalogId);
        Assert.IsFalse(notifications[1].Hidden);
    }

    [TestMethod]
    public async Task SetAppHidden_PersistenceFailure_RemainsFaulted()
    {
        var persistenceFailure = new IOException("Test visibility persistence failure.");
        var visibility = new MutableVisibilityStore { PersistException = persistenceFailure };
        var logger = new RecordingLogger<AppCatalog>();
        using var source = new TestAppSource("test", [CreateCatalogItem("Notepad", identity: "win32:notepad")]);
        using var catalog = CreateCatalog([source], new TestCache(null), visibility, logger: logger);
        await catalog.RefreshAsync();
        var catalogId = catalog.GetSnapshot().Items.Single().CatalogId;

        var exception = await Assert.ThrowsExceptionAsync<IOException>(() =>
            catalog.SetAppHiddenAsync(catalogId, hidden: true).WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.AreSame(persistenceFailure, exception);
        Assert.AreEqual(1, visibility.PersistCount);
        Assert.AreEqual(catalogId, catalog.GetSnapshot().HiddenItems.Single().CatalogId);
        Assert.IsFalse(logger.HasEvent(11));
    }

    [TestMethod]
    public void SettingsVisibilityStore_HidesCatalogIdentity()
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), $"cmdpal-apps-settings-{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(
                settingsPath,
                "{\"DisabledProgramSources\":[{\"UniqueIdentifier\":\"win32:hidden\"}]}");
            var settings = new AllAppsSettings(settingsPath);
            using var visibility = new SettingsAppVisibilityStore(settings);
            var item = CreateCatalogItem("Hidden", identity: "win32:hidden");

            Assert.AreEqual(AppVisibility.Hidden, visibility.GetVisibility(item));
        }
        finally
        {
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    public async Task FilterChange_ReprojectsWithoutRefreshingSourcesOrSavingCache()
    {
        using var source = new TestAppSource("test", [CreateCatalogItem("Visible"), CreateCatalogItem("Excluded")]);
        var cache = new TestCache(null);
        var filter = new MutableCatalogFilter();
        using var catalog = new AppCatalog(
            new MutableSourceProvider([source]),
            cache,
            new VisibleApps(),
            [filter],
            timeProvider: TestTimeProvider,
            invalidationDelay: TimeSpan.Zero);
        await catalog.RefreshAsync();
        var changedCount = 0;
        catalog.Changed += (_, _) => changedCount++;

        filter.Exclude("win32:Excluded");
        await WaitForConditionAsync(() => catalog.GetSnapshot().Items.Count == 1 && changedCount == 1);

        Assert.AreEqual("Visible", catalog.GetSnapshot().Items[0].Name);
        Assert.AreEqual(1, source.LoadCount);
        Assert.AreEqual(1, cache.SaveCount);
        Assert.AreEqual(1, changedCount);
    }

    [TestMethod]
    public async Task FilterChange_NotificationFailure_IsNotReportedAsReprojectionFailure()
    {
        var cache = new TestCache(null);
        var filter = new MutableCatalogFilter();
        var logger = new RecordingLogger<AppCatalog>();
        using var source = new TestAppSource("test", [CreateCatalogItem("Visible"), CreateCatalogItem("Excluded")]);
        using var catalog = CreateCatalog([source], cache, filters: [filter], logger: logger);
        await catalog.RefreshAsync();
        catalog.Changed += (_, _) =>
        {
            throw new InvalidOperationException("Test filter notification failure.");
        };

        filter.Exclude("win32:Excluded");
        await WaitForConditionAsync(() => logger.HasEvent(11));

        Assert.AreEqual("Visible", catalog.GetSnapshot().Items.Single().Name);
        Assert.AreEqual(1, source.LoadCount);
        Assert.AreEqual(1, cache.SaveCount);
        Assert.IsFalse(logger.HasEvent(9));
    }

    [TestMethod]
    public async Task FilterReprojectionFailure_IsObservedAndKeepsPublishedState()
    {
        var filter = new ThrowingCatalogFilter();
        var logger = new RecordingLogger<AppCatalog>();
        using var source = new TestAppSource("test", [CreateCatalogItem("Visible")]);
        using var catalog = CreateCatalog(
            [source],
            new TestCache(null),
            filters: [filter],
            logger: logger);
        await catalog.RefreshAsync();

        filter.FailAndRaiseChanged();
        await WaitForConditionAsync(() => logger.HasEvent(9));

        Assert.AreEqual(1, catalog.GetSnapshot().Items.Count);
        Assert.AreEqual("Visible", catalog.GetSnapshot().Items[0].Name);
        Assert.IsFalse(logger.HasEvent(11));
    }

    [TestMethod]
    public async Task MergedCommandIds_FollowVisibilityAndPresentationChanges()
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), $"cmdpal-apps-settings-{Guid.NewGuid():N}.json");
        try
        {
            var program = TestDataHelper.CreateTestWin32Program("Editor", @"C:\Apps\Editor.exe");
            program.Description = "Editor description";
            var preferred = CreateWin32CatalogItem(program, "win32:editor", 0, "test");
            program.Name = "Legacy Editor";
            program.LnkFilePath = @"C:\Links\Legacy Editor.lnk";
            var legacy = CreateWin32CatalogItem(program, preferred.Identity, 10, "test");
            var legacyId = legacy.Payload.GetCommandId();
            using var source = new TestAppSource("test", [preferred.MergeProvenance(legacy)]);
            var settings = new AllAppsSettings(settingsPath);
            using var catalog = CreateCatalog([source], new TestCache(null), new SettingsAppVisibilityStore(settings));
            using var list = new AppListItemSource(catalog, settings);
            using var page = new AllAppsPage(list, TestDataHelper.CreateFuzzyMatcherProvider());
            using var provider = new AllAppsCommandProvider(page, list, settings);
            await catalog.InitializeAsync();
            await WaitForConditionAsync(() => !list.IsLoading);

            var canonical = list.GetSnapshot().VisibleItems.Single();
            var resolved = provider.GetCommandItem(legacyId);
            Assert.IsNotNull(resolved);
            Assert.AreEqual(legacyId, resolved.Command!.Id);
            Assert.AreSame(canonical, list.GetSnapshot().GetVisibleApp(legacyId));
            Assert.AreEqual(canonical.Title, resolved.Title);
            Assert.AreEqual("Editor description", resolved.Subtitle);
            Assert.AreSame(canonical.Icon, resolved.Icon);
            Assert.IsInstanceOfType<Microsoft.CommandPalette.Extensions.IListItem>(resolved);

            var subtitleChanged = false;
            resolved.PropChanged += (_, args) => subtitleChanged |= args.PropertyName == nameof(resolved.Subtitle);
            var form = (SettingsForm)settings.Settings.ToContent().Single();
            form.SubmitForm("{\"apps.HideAppDescriptions\":\"true\"}", string.Empty);
            await WaitForConditionAsync(() => subtitleChanged && resolved.Subtitle == string.Empty);

            await catalog.SetAppHiddenAsync(preferred.Identity, hidden: true);
            Assert.AreEqual(legacyId, provider.GetCommandItem(legacyId)?.Command?.Id);
            Assert.IsNull(list.GetSnapshot().GetVisibleApp(legacyId));
            Assert.AreSame(canonical.MoreCommands, resolved.MoreCommands);
            Assert.AreEqual(Properties.Resources.unhide_app, resolved.MoreCommands.OfType<CommandContextItem>().Last().Command!.Name);
            await catalog.SetAppHiddenAsync(preferred.Identity, hidden: false);
            Assert.AreEqual(legacyId, provider.GetCommandItem(legacyId)?.Command?.Id);

            form.SubmitForm("{\"apps.ExcludedAppNames\":\"[\\\"*Editor*\\\"]\"}", string.Empty);
            await WaitForConditionAsync(() => list.GetSnapshot().PatternHiddenItems.Count == 1);
            Assert.AreEqual(legacyId, provider.GetCommandItem(legacyId)?.Command?.Id);
            Assert.IsNull(list.GetSnapshot().GetVisibleApp(legacyId));
            form.SubmitForm("{\"apps.ExcludedAppNames\":\"[]\"}", string.Empty);
            await WaitForConditionAsync(() => provider.GetCommandItem(legacyId) is not null);
        }
        finally
        {
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExclusionPatterns_UpdateHiddenSectionsWithoutRescanningAndPreserveManualHiding(bool useCache)
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), $"cmdpal-apps-settings-{Guid.NewGuid():N}.json");
        try
        {
            AppCatalogItem[] apps =
            [
                CreateCatalogItem("Visible"),
                CreateCatalogItem("Manual"),
                CreateCatalogItem("Manual Updater"),
                CreateCatalogItem("Utility Updater"),
                CreateCatalogItem("Portable"),
            ];
            using var source = new TestAppSource("test", apps);
            var cached = new AppCatalogCacheFile
            {
                Sources = [new AppCatalogSourceSnapshot { SourceId = "test", Items = [.. apps] }],
            };
            var cache = new TestCache(useCache ? cached : null);
            var settings = new AllAppsSettings(settingsPath);
            settings.Settings.Update(new JsonObject
            {
                ["apps.ExcludedAppPaths"] = new JsonArray(JsonValue.Create(@"C:\Apps\Portable.exe")),
            }.ToJsonString());
            using var catalog = CreateCatalog([source], cache, new SettingsAppVisibilityStore(settings));
            using var list = new AppListItemSource(catalog, settings);
            using var page = new AllAppsPage(list, TestDataHelper.CreateFuzzyMatcherProvider());
            await catalog.InitializeAsync();
            await WaitForConditionAsync(() => !list.IsLoading);
            var initial = list.GetSnapshot();
            Assert.AreEqual("Portable", initial.PatternHiddenItems.Single().Title);
            var rows = initial.VisibleItems.Concat(initial.PatternHiddenItems).ToDictionary(item => item.Title);
            await catalog.SetAppHiddenAsync(rows["Manual"].App.CatalogId, hidden: true);
            await catalog.SetAppHiddenAsync(rows["Manual Updater"].App.CatalogId, hidden: true);
            await WaitForConditionAsync(() => !catalog.IsRefreshing);
            var loadCount = source.LoadCount;
            var saveCount = cache.SaveCount;
            var form = (SettingsForm)settings.Settings.ToContent().Single();

            var values = new JsonObject
            {
                ["apps.ExcludedAppNames"] = new JsonArray(JsonValue.Create("*updater*")).ToJsonString(),
            };
            form.SubmitForm(values.ToJsonString(), string.Empty);
            await WaitForConditionAsync(() => list.GetSnapshot().PatternHiddenItems.Count == 3
                && rows["Manual Updater"].MoreCommands.OfType<CommandContextItem>().Last().Title == Properties.Resources.edit_exclusion_patterns);

            Assert.AreEqual("Visible", list.GetSnapshot().VisibleItems.Single().Title);
            Assert.AreEqual("Manual", list.GetSnapshot().HiddenItems.Single().Title);
            Assert.AreSame(rows["Manual Updater"], list.GetSnapshot().PatternHiddenItems.Single(item => item.Title == "Manual Updater"));
            Assert.AreEqual("Visible", page.GetItems().Single().Title);
            var provider = new AllAppsCommandProvider(page, list, settings);
            Assert.AreSame(rows["Utility Updater"], provider.GetCommandItem(rows["Utility Updater"].Command!.Id));
            Assert.IsNull(list.GetSnapshot().GetVisibleApp(rows["Utility Updater"].Command!.Id));

            page.Filters!.CurrentFilterId = AllAppsFilters.HiddenFilterId;
            CollectionAssert.AreEqual(
                new[] { Properties.Resources.hidden_manually, "Manual", Properties.Resources.hidden_by_exclusion_patterns, "Manual Updater", "Portable", "Utility Updater" },
                page.GetItems().Select(item => item.Title).ToArray());
            Assert.AreEqual(Properties.Resources.unhide_app, rows["Manual"].MoreCommands.OfType<CommandContextItem>().Last().Command!.Name);
            Assert.IsInstanceOfType<Microsoft.CommandPalette.Extensions.IContentPage>(rows["Utility Updater"].MoreCommands.OfType<CommandContextItem>().Last().Command);

            page.SearchText = "Updater";
            CollectionAssert.AreEqual(
                new[] { Properties.Resources.hidden_by_exclusion_patterns, "Manual Updater", "Utility Updater" },
                page.GetItems().Select(item => item.Title).ToArray());

            form.SubmitForm("{\"apps.ExcludedAppNames\":\"[]\",\"apps.ExcludedAppPaths\":\"[]\"}", string.Empty);
            await WaitForConditionAsync(() => list.GetSnapshot().PatternHiddenItems.Count == 0
                && rows["Manual Updater"].MoreCommands.OfType<CommandContextItem>().Last().Command!.Name == Properties.Resources.unhide_app);

            Assert.AreEqual(2, list.GetSnapshot().HiddenItems.Count);
            Assert.AreSame(rows["Manual Updater"], list.GetSnapshot().HiddenItems.Single(item => item.Title == "Manual Updater"));
            Assert.AreSame(rows["Utility Updater"], list.GetSnapshot().VisibleItems.Single(item => item.Title == "Utility Updater"));
            Assert.AreSame(rows["Portable"], list.GetSnapshot().VisibleItems.Single(item => item.Title == "Portable"));
            Assert.AreSame(rows["Utility Updater"], provider.GetCommandItem(rows["Utility Updater"].Command!.Id));
            Assert.AreEqual(loadCount, source.LoadCount);
            Assert.AreEqual(saveCount, cache.SaveCount);
            Assert.AreEqual(useCache ? 0 : 1, loadCount);
        }
        finally
        {
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    public async Task UninstallerFilterChange_ReprojectsAndPreservesHiddenPreference()
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), $"cmdpal-apps-settings-{Guid.NewGuid():N}.json");
        try
        {
            var normalItem = CreateCatalogItem("Visible");
            var uninstallerItem = CreateCatalogItem("Uninstall Contoso");
            using var source = new TestAppSource("test", [normalItem, uninstallerItem]);
            var cache = new TestCache(null);
            var visibility = new MutableVisibilityStore();
            var settings = new AllAppsSettings(settingsPath);
            var filter = new UninstallerAppCatalogFilter(settings);
            using var catalog = new AppCatalog(
                new MutableSourceProvider([source]),
                cache,
                visibility,
                [filter],
                timeProvider: TestTimeProvider,
                invalidationDelay: TimeSpan.Zero);
            await catalog.RefreshAsync();
            await catalog.SetAppHiddenAsync(uninstallerItem.Identity, hidden: true);
            var settingsForm = (SettingsForm)settings.Settings.ToContent()[0];

            settingsForm.SubmitForm("{\"apps.HideUninstallers\":\"true\"}", string.Empty);
            await WaitForConditionAsync(() =>
            {
                var snapshot = catalog.GetSnapshot();
                return snapshot.Items.Count == 1 && snapshot.HiddenItems.Count == 0;
            });

            Assert.AreEqual("Visible", catalog.GetSnapshot().Items[0].Name);
            Assert.AreEqual(1, source.LoadCount);
            Assert.AreEqual(1, cache.SaveCount);

            settingsForm.SubmitForm("{\"apps.HideUninstallers\":\"false\"}", string.Empty);
            await WaitForConditionAsync(() => catalog.GetSnapshot().HiddenItems.Count == 1);

            Assert.AreEqual("Uninstall Contoso", catalog.GetSnapshot().HiddenItems[0].Name);
            Assert.AreEqual(1, source.LoadCount);
            Assert.AreEqual(1, cache.SaveCount);
        }
        finally
        {
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CommandIds_RetainLegacyPinsAcrossRenamingAndSettingsReload(bool packaged)
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), $"cmdpal-app-command-aliases-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(settingsPath, "{\"futureSetting\":\"preserved\"}");
            var program = TestDataHelper.CreateTestWin32Program("Editor", @"C:\Apps\Editor.exe");
            program.LnkFilePath = @"C:\Links\Editor.lnk";
            IAppCatalogPayload originalPayload = packaged
                ? new PackagedAppSnapshot { Name = "Editor", Description = "Text editor", UserModelId = "Contoso.Editor_123!app" }
                : Win32AppPayload.From(program);
            var identity = packaged ? "packaged:Contoso.Editor_123!app" : @"win32:C:\Apps\Editor.exe|args:";
            var original = new AppCatalogItem(identity, 0, new AppCatalogSourceReference("test", "original"), [], originalPayload);
            var legacyId = originalPayload.GetCommandId();
            var stableId = new AppCommand(original.ToAppItem()).Id;
            IAppCatalogPayload renamedPayload = packaged
                ? ((PackagedAppSnapshot)originalPayload) with { Name = "Éditeur", Description = "Éditeur de texte" }
                : ((Win32AppPayload)originalPayload) with { Name = "Renamed Editor", Description = "Updated description", LnkFilePath = @"C:\Links\Renamed Editor.lnk" };
            var renamed = new AppCatalogItem(identity, 0, new AppCatalogSourceReference("test", "renamed"), [], renamedPayload);
            var settings = new AllAppsSettings(settingsPath);

            using (var source = new TestAppSource("test", [original]))
            using (var catalog = CreateCatalog([source], new TestCache(null), new SettingsAppVisibilityStore(settings)))
            using (var list = new AppListItemSource(catalog, settings))
            {
                await catalog.InitializeAsync();
                await WaitForConditionAsync(() => !list.IsLoading);
                Assert.AreEqual(legacyId, list.GetSnapshot().GetCommandItem(legacyId)?.Command?.Id);
                Assert.AreEqual(stableId, list.GetSnapshot().VisibleItems.Single().Command!.Id);

                source.SetItems([renamed]);
                await catalog.RefreshAsync();
                Assert.AreEqual(stableId, list.GetSnapshot().VisibleItems.Single().Command!.Id);
                Assert.AreEqual(renamedPayload.ToAppItem().Name, list.GetSnapshot().GetCommandItem(legacyId)?.Title);
                Assert.AreEqual(legacyId, list.GetSnapshot().GetCommandItem(legacyId)?.Command?.Id);

                await catalog.SetAppHiddenAsync(identity, hidden: true);
                Assert.AreEqual(legacyId, list.GetSnapshot().GetCommandItem(legacyId)?.Command?.Id);
                Assert.IsNull(list.GetSnapshot().GetVisibleApp(legacyId));
                await catalog.SetAppHiddenAsync(identity, hidden: false);
            }

            // Start with a fresh catalog so compatibility does not depend on the discovery cache.
            using var reloadedSource = new TestAppSource("test", [renamed]);
            using var reloadedCatalog = CreateCatalog([reloadedSource], new TestCache(null));
            using var reloadedList = new AppListItemSource(reloadedCatalog, new AllAppsSettings(settingsPath));
            await reloadedCatalog.InitializeAsync();
            await WaitForConditionAsync(() => !reloadedList.IsLoading);
            Assert.AreEqual(stableId, reloadedList.GetSnapshot().GetCommandItem(stableId)?.Command?.Id);
            Assert.AreEqual(legacyId, reloadedList.GetSnapshot().GetCommandItem(legacyId)?.Command?.Id);
            Assert.AreEqual(renamedPayload.ToAppItem().Name, reloadedList.GetSnapshot().GetCommandItem(legacyId)?.Title);
            Assert.IsNotNull(reloadedList.GetSnapshot().GetCommandItem(renamedPayload.GetCommandId()));
            StringAssert.Contains(File.ReadAllText(settingsPath), "\"futureSetting\": \"preserved\"");
        }
        finally
        {
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    public void ListItems_KeepAliasesAndRowsInTheSameCatalogPublication()
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), $"cmdpal-app-snapshot-{Guid.NewGuid():N}.json");
        try
        {
            var program = TestDataHelper.CreateTestWin32Program("Editor", @"C:\Old\Editor.exe");
            program.LnkFilePath = @"C:\Links\Editor.lnk";
            var original = CreateWin32CatalogItem(program, @"win32:C:\Old\Editor.exe|args:", 0, "test").ToAppItem();
            program.FullPath = @"D:\New\Editor.exe";
            var moved = CreateWin32CatalogItem(program, @"win32:D:\New\Editor.exe|args:", 0, "test").ToAppItem();
            var originalSnapshot = new AppCatalogSnapshot([original], []);
            var movedSnapshot = new AppCatalogSnapshot([moved], []);
            var currentSnapshot = originalSnapshot;
            var catalog = new Mock<IAppCatalog>();
            catalog.Setup(value => value.InitializeAsync()).Returns(Task.CompletedTask);
            catalog.Setup(value => value.GetSnapshot()).Returns(() =>
            {
                var snapshot = currentSnapshot;
                currentSnapshot = movedSnapshot;
                return snapshot;
            });
            using var list = new AppListItemSource(catalog.Object, new AllAppsSettings(settingsPath));
            var originalId = new AppCommand(original).Id;

            Assert.AreSame(original, list.GetSnapshot().VisibleItems.Single().App);
            Assert.AreSame(original, list.GetSnapshot().GetVisibleApp(original.CommandIds[0])?.App);

            catalog.Raise(value => value.Changed += null, new AppCatalogChangedEventArgs(
                [new AppCatalogItemChange(AppCatalogChangeKind.Updated, moved.CatalogId, moved, hidden: false)]));

            Assert.AreSame(moved, list.GetSnapshot().VisibleItems.Single().App);
            Assert.AreSame(moved, list.GetSnapshot().GetVisibleApp(originalId)?.App);
        }
        finally
        {
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    public async Task CommandIds_FollowInstallMovesAndRetainEarlierAliasesAfterRestart()
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), $"cmdpal-app-command-aliases-{Guid.NewGuid():N}.json");
        try
        {
            var program = TestDataHelper.CreateTestWin32Program("Editor", @"C:\Program Files (x86)\Editor\Editor.exe");
            program.LnkFilePath = @"C:\Links\Editor.lnk";
            var original = CreateWin32CatalogItem(program, $"win32:{program.FullPath}|args:", 0, "test");
            var legacyId = original.Payload.GetCommandId();
            var originalId = new AppCommand(original.ToAppItem()).Id;
            program.Name = "Renamed Editor";
            var renamed = CreateWin32CatalogItem(program, original.Identity, 0, "test");
            var renamedId = renamed.Payload.GetCommandId();
            program.FullPath = @"C:\Program Files\Editor\Editor.exe";
            var moved = CreateWin32CatalogItem(program, $"win32:{program.FullPath}|args:", 0, "test");
            var movedId = new AppCommand(moved.ToAppItem()).Id;
            Assert.AreNotEqual(originalId, movedId);

            using (var source = new TestAppSource("test", [original]))
            using (var catalog = CreateCatalog([source], new TestCache(null)))
            using (var list = new AppListItemSource(catalog, new AllAppsSettings(settingsPath)))
            {
                await catalog.InitializeAsync();
                await WaitForConditionAsync(() => !list.IsLoading);
                source.SetItems([renamed]);
                await catalog.RefreshAsync();
                source.SetItems([moved]);
                await catalog.RefreshAsync();
                foreach (var id in new[] { originalId, legacyId, renamedId })
                {
                    Assert.AreEqual(movedId, list.GetSnapshot().GetVisibleApp(id)?.Command?.Id);
                    Assert.AreEqual(id, list.GetSnapshot().GetCommandItem(id)?.Command?.Id);
                }
            }

            // A second install move after a restart must redirect the entire saved alias group again.
            program.FullPath = @"D:\Apps\Editor\Editor.exe";
            var movedAgain = CreateWin32CatalogItem(program, $"win32:{program.FullPath}|args:", 0, "test");
            using var freshSource = new TestAppSource("test", [movedAgain]);
            using var freshCatalog = CreateCatalog([freshSource], new TestCache(null));
            using var freshList = new AppListItemSource(freshCatalog, new AllAppsSettings(settingsPath));
            await freshCatalog.InitializeAsync();
            await WaitForConditionAsync(() => !freshList.IsLoading);
            foreach (var id in new[] { originalId, movedId, legacyId, renamedId })
            {
                Assert.AreEqual("Renamed Editor", freshList.GetSnapshot().GetCommandItem(id)?.Title);
                Assert.AreEqual(program.FullPath, freshList.GetSnapshot().GetVisibleApp(id)?.App.FullExecutablePath);
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
    public async Task HiddenApps_FollowInstallMovesAndUnhideEarlierIdentities(bool legacyHide)
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), $"cmdpal-hidden-app-move-{Guid.NewGuid():N}.json");
        try
        {
            const string shortcut = @"C:\Links\Editor.lnk";
            const string launchIdentity = @"win32:C:\Links\Editor.lnk|args:";
            var program = TestDataHelper.CreateTestWin32Program("Editor", @"C:\Old\Editor.exe");
            program.LnkFilePath = shortcut;
            var originalPayload = Win32AppPayload.From(program);
            var original = new AppCatalogItem(
                @"win32:C:\Old\Editor.exe|args:",
                0,
                new AppCatalogSourceReference("test", shortcut),
                [],
                originalPayload,
                identityAliases: [launchIdentity]);
            var moved = new AppCatalogItem(
                @"win32:D:\New\Editor.exe|args:",
                0,
                new AppCatalogSourceReference("test", shortcut),
                [],
                originalPayload with { Name = "Renamed Editor", FullPath = @"D:\New\Editor.exe" },
                identityAliases: [launchIdentity]);
            var settings = new AllAppsSettings(settingsPath);
            if (legacyHide)
            {
                settings.SetAppHidden(original.Identity, hidden: true);
                settings.SaveSettings();
            }

            using (var source = new TestAppSource("test", [original]))
            using (var catalog = CreateCatalog([source], new TestCache(null), new SettingsAppVisibilityStore(settings)))
            using (var list = new AppListItemSource(catalog, settings))
            {
                await catalog.InitializeAsync();
                await WaitForConditionAsync(() => !list.IsLoading);
                if (!legacyHide)
                {
                    await catalog.SetAppHiddenAsync(original.Identity, hidden: true);
                    Assert.IsTrue(settings.IsAppHidden(launchIdentity));
                }

                source.SetItems([moved]);
                await catalog.RefreshAsync();
                await WaitForConditionAsync(() => list.GetSnapshot().VisibleItems.Count == 0
                    && list.GetSnapshot().HiddenItems.SingleOrDefault()?.App.CatalogId == moved.Identity);
                Assert.AreEqual(0, list.GetSnapshot().VisibleItems.Count);
                Assert.AreEqual(moved.Identity, list.GetSnapshot().HiddenItems.Single().App.CatalogId);
                var savedId = new AppCommand(original.ToAppItem()).Id;
                Assert.AreEqual(savedId, list.GetSnapshot().GetCommandItem(savedId)?.Command?.Id);
                Assert.AreEqual(moved.Identity, list.GetSnapshot().GetApp(savedId)?.App.CatalogId);
                Assert.IsNull(list.GetSnapshot().GetVisibleApp(savedId));
            }

            using var freshSource = new TestAppSource("test", [moved]);
            var reloaded = new AllAppsSettings(settingsPath);
            using var freshCatalog = CreateCatalog([freshSource], new TestCache(null), new SettingsAppVisibilityStore(reloaded));
            using var freshList = new AppListItemSource(freshCatalog, reloaded);
            await freshCatalog.InitializeAsync();
            await WaitForConditionAsync(() => !freshList.IsLoading);
            Assert.AreEqual(moved.Identity, freshList.GetSnapshot().HiddenItems.Single().App.CatalogId);
            await freshCatalog.SetAppHiddenAsync(moved.Identity, hidden: false);
            Assert.AreEqual(moved.Identity, freshList.GetSnapshot().VisibleItems.Single().App.CatalogId);
            Assert.IsFalse(reloaded.IsAppHidden(original.Identity));
            Assert.IsFalse(reloaded.IsAppHidden(launchIdentity));

            freshSource.SetItems([original]);
            await freshCatalog.RefreshAsync();
            Assert.AreEqual(original.Identity, freshList.GetSnapshot().VisibleItems.Single().App.CatalogId);
            Assert.AreEqual(0, freshList.GetSnapshot().HiddenItems.Count);
        }
        finally
        {
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    public async Task CommandIds_DoNotRedirectAnInstallationThatStillExistsButIsHidden()
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), $"cmdpal-app-command-aliases-{Guid.NewGuid():N}.json");
        try
        {
            var program = TestDataHelper.CreateTestWin32Program("Editor", @"C:\Old\Editor.exe");
            program.LnkFilePath = @"C:\Links\Editor.lnk";
            var original = CreateWin32CatalogItem(program, $"win32:{program.FullPath}|args:", 0, "test");
            var originalId = new AppCommand(original.ToAppItem()).Id;
            program.FullPath = @"C:\New\Editor.exe";
            var replacement = CreateWin32CatalogItem(program, $"win32:{program.FullPath}|args:", 0, "test");
            var settings = new AllAppsSettings(settingsPath);
            using var source = new TestAppSource("test", [original]);
            using var catalog = CreateCatalog([source], new TestCache(null), new SettingsAppVisibilityStore(settings));
            using var list = new AppListItemSource(catalog, settings);
            await catalog.InitializeAsync();
            await WaitForConditionAsync(() => !list.IsLoading);
            await catalog.SetAppHiddenAsync(original.Identity, hidden: true);
            source.SetItems([original, replacement]);
            await catalog.RefreshAsync();
            Assert.AreEqual(originalId, list.GetSnapshot().GetCommandItem(originalId)?.Command?.Id);
            Assert.IsNull(list.GetSnapshot().GetVisibleApp(originalId));
            Assert.IsNull(list.GetSnapshot().GetCommandItem(original.Payload.GetCommandId()));
            Assert.AreEqual(new AppCommand(replacement.ToAppItem()).Id, list.GetSnapshot().VisibleItems.Single().Command!.Id);
        }
        finally
        {
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    public async Task CommandIds_RetainAliasesWhenWin32RepresentationBecomesPackaged()
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), $"cmdpal-app-command-aliases-{Guid.NewGuid():N}.json");
        try
        {
            var program = TestDataHelper.CreateTestWin32Program("Editor", @"C:\Apps\Editor.exe");
            var win32 = CreateWin32CatalogItem(program, @"win32:C:\Apps\Editor.exe|args:", 0, "test");
            var legacyId = win32.Payload.GetCommandId();
            var win32Id = new AppCommand(win32.ToAppItem()).Id;
            var packaged = new AppCatalogItem(
                "packaged:Contoso.Editor_123!app",
                0,
                new AppCatalogSourceReference("test", "packaged"),
                [],
                new PackagedAppSnapshot { Name = "Packaged Editor", UserModelId = "Contoso.Editor_123!app" });
            using (var source = new TestAppSource("test", [win32]))
            using (var catalog = CreateCatalog([source], new TestCache(null)))
            using (var list = new AppListItemSource(catalog, new AllAppsSettings(settingsPath)))
            {
                await catalog.InitializeAsync();
                await WaitForConditionAsync(() => !list.IsLoading);
                source.SetItems([packaged.MergeProvenance(win32.WithIdentity(packaged.Identity))]);
                await catalog.RefreshAsync();
                Assert.AreEqual(win32Id, list.GetSnapshot().GetCommandItem(win32Id)?.Command?.Id);
                Assert.AreEqual(legacyId, list.GetSnapshot().GetCommandItem(legacyId)?.Command?.Id);
            }

            using var freshSource = new TestAppSource("test", [packaged]);
            using var freshCatalog = CreateCatalog([freshSource], new TestCache(null));
            using var freshList = new AppListItemSource(freshCatalog, new AllAppsSettings(settingsPath));
            await freshCatalog.InitializeAsync();
            await WaitForConditionAsync(() => !freshList.IsLoading);
            Assert.AreEqual(win32Id, freshList.GetSnapshot().GetCommandItem(win32Id)?.Command?.Id);
            Assert.AreEqual(legacyId, freshList.GetSnapshot().GetCommandItem(legacyId)?.Command?.Id);
            Assert.AreEqual("Packaged Editor", freshList.GetSnapshot().GetCommandItem(legacyId)?.Title);
        }
        finally
        {
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CommandIds_DistinguishPackagedAppsWithIdenticalDisplayMetadata(bool coexist)
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), $"cmdpal-app-command-aliases-{Guid.NewGuid():N}.json");
        try
        {
            var first = new AppCatalogItem(
                "packaged:Contoso.First_123!app",
                0,
                new AppCatalogSourceReference("test", "first"),
                [],
                new PackagedAppSnapshot { Name = "Editor", Description = "Text editor", UserModelId = "Contoso.First_123!app" });
            var second = new AppCatalogItem(
                "packaged:Contoso.Second_123!app",
                0,
                new AppCatalogSourceReference("test", "second"),
                [],
                new PackagedAppSnapshot { Name = "Editor", Description = "Text editor", UserModelId = "Contoso.Second_123!app" });
            Assert.AreEqual(first.Payload.GetCommandId(), second.Payload.GetCommandId());
            using var source = new TestAppSource("test", [first]);
            using var catalog = CreateCatalog([source], new TestCache(null));
            using var list = new AppListItemSource(catalog, new AllAppsSettings(settingsPath));
            await catalog.InitializeAsync();
            await WaitForConditionAsync(() => !list.IsLoading);
            var firstId = new AppCommand(first.ToAppItem()).Id;
            var secondId = new AppCommand(second.ToAppItem()).Id;
            Assert.AreNotEqual(firstId, secondId);
            source.SetItems(coexist ? [first, second] : [second]);
            await catalog.RefreshAsync();
            var rows = list.GetSnapshot().VisibleItems;
            Assert.AreEqual(coexist ? 2 : 1, rows.Count);
            Assert.AreEqual(coexist, list.GetSnapshot().GetCommandItem(firstId) is not null, "A matching display name cannot redirect an absent AUMID to a different application.");
            Assert.IsNotNull(list.GetSnapshot().GetCommandItem(secondId));
            foreach (var row in rows)
            {
                Assert.AreSame(row, list.GetSnapshot().GetVisibleApp(row.Command!.Id));
            }

            Assert.IsNull(list.GetSnapshot().GetCommandItem(first.Payload.GetCommandId()), "An ambiguous old ID must not launch an arbitrary application.");
            source.SetItems([first]);
            await catalog.RefreshAsync();
            Assert.IsNull(list.GetSnapshot().GetCommandItem(first.Payload.GetCommandId()));
        }
        finally
        {
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    private static AppCatalog CreateCatalog(
        IReadOnlyList<IAppSource> sources,
        IAppCatalogCache cache,
        IAppVisibilityStore? visibilityStore = null,
        IReadOnlyList<IAppCatalogFilter>? filters = null,
        MEL.ILogger<AppCatalog>? logger = null)
    {
        return new(
            new MutableSourceProvider(sources),
            cache,
            visibilityStore ?? new VisibleApps(),
            filters,
            timeProvider: TestTimeProvider,
            invalidationDelay: TimeSpan.Zero,
            logger: logger);
    }

    private static AppCatalogItem CreateCatalogItem(
        string name,
        int priority = 0,
        string? identity = null,
        string sourceId = "test")
    {
        var program = TestDataHelper.CreateTestWin32Program(name, $@"C:\Apps\{name}.exe");
        return new AppCatalogItem(
            identity ?? $"win32:{name}",
            priority,
            new AppCatalogSourceReference(sourceId, program.FullPath),
            [name],
            Win32AppPayload.From(program));
    }

    private static Win32Program CreateAppExecutionAliasProgram(
        string name,
        string aliasPath,
        string targetPath,
        string aumid)
    {
        var program = TestDataHelper.CreateTestWin32Program(name, aliasPath);
        program.AppType = Win32Program.ApplicationType.RunCommand;
        program.IcoPath = targetPath;
        program.AppExecutionAlias = new ReparsePoint.AppExecutionAliasInfo
        {
            Aumid = aumid,
            TargetPath = targetPath,
        };
        return program;
    }

    private static AppCatalogItem CreateWin32CatalogItem(
        Win32Program program,
        string identity,
        int priority,
        string sourceId)
    {
        return new(
            identity,
            priority,
            new AppCatalogSourceReference(sourceId, program.FullPath),
            [program.Name],
            Win32AppPayload.From(program));
    }

    private static bool ContainsApp(IReadOnlyList<AppItem> items, string name)
    {
        foreach (var item in items)
        {
            if (string.Equals(item.Name, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsString(IReadOnlyList<string> items, string expected)
    {
        foreach (var item in items)
        {
            if (string.Equals(item, expected, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task WaitForConditionAsync(Func<bool> condition)
    {
        var timeout = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow >= timeout)
            {
                Assert.Fail("Timed out waiting for condition.");
            }

            await Task.Delay(10);
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }
    }

    private sealed class VisibleApps : IAppVisibilityStore
    {
        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        public AppVisibility GetVisibility(AppCatalogItem item)
        {
            return AppVisibility.Visible;
        }

        public bool SetHidden(AppCatalogItem item, bool hidden)
        {
            return false;
        }

        public void Persist()
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class MutableVisibilityStore : IAppVisibilityStore
    {
        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        private readonly HashSet<string> _hiddenIds = new(StringComparer.OrdinalIgnoreCase);

        public int PersistCount { get; private set; }

        public Exception? PersistException { get; init; }

        public AppVisibility GetVisibility(AppCatalogItem item)
        {
            return _hiddenIds.Contains(item.Identity) ? AppVisibility.Hidden : AppVisibility.Visible;
        }

        public bool SetHidden(AppCatalogItem item, bool hidden)
        {
            return hidden ? _hiddenIds.Add(item.Identity) : _hiddenIds.Remove(item.Identity);
        }

        public void Persist()
        {
            PersistCount++;
            if (PersistException is { } exception)
            {
                throw exception;
            }
        }

        public void Dispose()
        {
        }
    }

    private sealed class MutableCatalogFilter : IAppCatalogFilter
    {
        public event EventHandler? Changed;

        private readonly HashSet<string> _excludedIds = new(StringComparer.OrdinalIgnoreCase);

        public bool Includes(AppCatalogItem item)
        {
            return !_excludedIds.Contains(item.Identity);
        }

        public void Exclude(string identity)
        {
            if (_excludedIds.Add(identity))
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Dispose()
        {
            Changed = null;
        }
    }

    private sealed class ThrowingCatalogFilter : IAppCatalogFilter
    {
        public event EventHandler? Changed;

        private bool _fail;

        public string? FailingIdentity { get; init; }

        public bool Includes(AppCatalogItem item)
        {
            return !_fail && !StringComparer.OrdinalIgnoreCase.Equals(FailingIdentity, item.Identity)
                ? true
                : throw new InvalidOperationException("Test reprojection failure.");
        }

        public void FailAndRaiseChanged()
        {
            _fail = true;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            Changed = null;
        }
    }

    private sealed class RecordingLogger<T> : MEL.ILogger<T>
    {
        private readonly Lock _eventLock = new();
        private readonly HashSet<int> _eventIds = [];
        private readonly List<(int EventId, IReadOnlyDictionary<string, object?> State)> _entries = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (_eventLock)
            {
                _eventIds.Add(eventId.Id);
                if (state is IEnumerable<KeyValuePair<string, object?>> values)
                {
                    _entries.Add((eventId.Id, values.ToDictionary()));
                }
            }
        }

        public bool HasEvent(int eventId)
        {
            lock (_eventLock)
            {
                return _eventIds.Contains(eventId);
            }
        }

        public IReadOnlyList<IReadOnlyDictionary<string, object?>> GetEntries(int eventId)
        {
            lock (_eventLock)
            {
                return _entries.Where(entry => entry.EventId == eventId).Select(entry => entry.State).ToArray();
            }
        }
    }

    private sealed class TestCache : IAppCatalogCache
    {
        private readonly AppCatalogCacheFile? _cache;
        private Exception? _nextSaveException;
        private int _saveCount;

        public int SaveCount => Volatile.Read(ref _saveCount);

        public Task<AppCatalogCacheFile?>? DeferredLoad { get; init; }

        public Task? DeferredSave { get; init; }

        public IReadOnlyDictionary<string, IReadOnlyList<AppCatalogItem>>? LastSavedSnapshots { get; private set; }

        public IReadOnlyCollection<string> LastFullyReconciledSourceIds { get; private set; } = [];

        public TestCache(AppCatalogCacheFile? cache)
        {
            _cache = cache;
        }

        public Task<AppCatalogCacheFile?> LoadAsync(AppCatalogCacheContext context, CancellationToken cancellationToken)
        {
            return DeferredLoad ?? Task.FromResult(_cache);
        }

        public void FailNextSave(Exception exception)
        {
            _nextSaveException = exception;
        }

        public Task SaveAsync(
            IReadOnlyDictionary<string, IReadOnlyList<AppCatalogItem>> sourceSnapshots,
            IReadOnlyCollection<string> fullyReconciledSourceIds,
            AppCatalogCacheContext context,
            CancellationToken cancellationToken)
        {
            LastSavedSnapshots = sourceSnapshots;
            LastFullyReconciledSourceIds = fullyReconciledSourceIds.ToArray();
            var exception = Interlocked.Exchange(ref _nextSaveException, null);
            var save = exception is not null ? Task.FromException(exception) : DeferredSave ?? Task.CompletedTask;
            // Publish the captured save state before a polling test observes the new count.
            // The count signals that saving started, even when DeferredSave is still pending.
            Interlocked.Increment(ref _saveCount);
            return save;
        }
    }

    private sealed class MutableSourceProvider : IAppSourceProvider
    {
        public event EventHandler? Changed;

        private IReadOnlyList<IAppSource> _sources;

        public MutableSourceProvider(IReadOnlyList<IAppSource> sources)
        {
            _sources = sources;
        }

        public IReadOnlyList<IAppSource> GetSources()
        {
            return _sources;
        }

        public void SetSources(IReadOnlyList<IAppSource> sources)
        {
            _sources = sources;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            Changed = null;
        }
    }

    private sealed class TestAppSource : IAppSource
    {
        public event EventHandler<AppSourceInvalidatedEventArgs>? Invalidated;

        private IReadOnlyList<AppCatalogItem> _items;
        private Task? _initialization;
        private Task<IReadOnlyList<AppCatalogItem>>? _nextLoad;
        private Func<IReadOnlyList<AppCatalogItem>, IReadOnlyList<AppCatalogItem>>? _incrementalFactory;

        // Tests poll these start signals across threads. Capture each operation before incrementing;
        // the volatile getters then make that captured state visible to the waiting test.
        private int _loadCount;
        private int _initializeCount;
        private int _incrementalLoadCount;

        public string Id { get; }

        public string CacheKey => Id;

        public int LoadCount => Volatile.Read(ref _loadCount);

        public int InitializeCount => Volatile.Read(ref _initializeCount);

        public int IncrementalLoadCount => Volatile.Read(ref _incrementalLoadCount);

        public bool IsDisposed { get; private set; }

        public Exception? LoadFailure { get; set; }

        public bool LastLoadBackground { get; private set; }

        public CancellationToken LastLoadToken { get; private set; }

        public bool LastIncrementalBackground { get; private set; }

        public IReadOnlyList<AppCatalogItem> LastIncrementalBaseItems { get; private set; } = [];

        public IReadOnlyList<AppSourcePathChange> LastIncrementalChanges { get; private set; } = [];

        public TestAppSource(string id, IReadOnlyList<AppCatalogItem> items)
        {
            Id = id;
            _items = items;
        }

        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            var initialization = _initialization;
            Interlocked.Increment(ref _initializeCount);
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return initialization?.WaitAsync(cancellationToken) ?? Task.CompletedTask;
        }

        public void DeferInitialization(Task initialization)
        {
            _initialization = initialization;
        }

        public Task<IReadOnlyList<AppCatalogItem>> LoadAsync(CancellationToken cancellationToken, bool background = false, IReadOnlyList<AppSourcePathChange>? dirtyPaths = null)
        {
            LastLoadBackground = background;
            LastLoadToken = cancellationToken;
            // Consume this load's gate before signaling: the waiting test may immediately supply the next one.
            var load = LoadFailure is { } failure
                ? Task.FromException<IReadOnlyList<AppCatalogItem>>(failure)
                : Interlocked.Exchange(ref _nextLoad, null)?.WaitAsync(cancellationToken) ?? Task.FromResult(_items);
            Interlocked.Increment(ref _loadCount);
            return load;
        }

        public void DeferNextLoad(Task<IReadOnlyList<AppCatalogItem>> nextLoad)
        {
            Interlocked.Exchange(ref _nextLoad, nextLoad);
        }

        public void SetItems(IReadOnlyList<AppCatalogItem> items)
        {
            _items = items;
        }

        public void SetIncrementalFactory(Func<IReadOnlyList<AppCatalogItem>, IReadOnlyList<AppCatalogItem>> incrementalFactory)
        {
            _incrementalFactory = incrementalFactory;
        }

        public Task<IReadOnlyList<AppCatalogItem>> ApplyChangesAsync(
            IReadOnlyList<AppCatalogItem> currentItems,
            IReadOnlyList<AppSourcePathChange> changes,
            CancellationToken cancellationToken,
            bool background = false)
        {
            LastIncrementalBackground = background;
            LastIncrementalBaseItems = currentItems;
            LastIncrementalChanges = new List<AppSourcePathChange>(changes);
            var incrementalFactory = _incrementalFactory;
            Interlocked.Increment(ref _incrementalLoadCount);
            return Task.FromResult(incrementalFactory?.Invoke(currentItems) ?? currentItems);
        }

        public void Invalidate(AppSourceInvalidatedEventArgs? args = null)
        {
            Invalidated?.Invoke(this, args ?? AppSourceInvalidatedEventArgs.FullRefresh);
        }

        public void Dispose()
        {
            IsDisposed = true;
            Invalidated = null;
        }
    }
}
