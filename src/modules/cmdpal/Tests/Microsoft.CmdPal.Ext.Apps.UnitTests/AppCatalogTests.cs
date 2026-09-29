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

using MEL = Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class AppCatalogTests
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

        Assert.AreEqual(1, catalog.Items.Count);
        Assert.AreEqual("Fresh", catalog.Items[0].Name);
    }

    [TestMethod]
    public async Task InitializeAsync_ValidCache_DoesNotLoadSources()
    {
        var cachedItem = CreateCatalogItem("Cached");
        var cache = new TestCache(new AppCatalogCacheFile
        {
            Sources = [new AppCatalogSourceSnapshot { SourceId = "test", Items = [cachedItem] }],
        });
        using var source = new TestAppSource("test", [CreateCatalogItem("Fresh")]);
        using var catalog = CreateCatalog([source], cache);

        await catalog.InitializeAsync();

        Assert.AreEqual(0, source.LoadCount);
        Assert.AreEqual("Cached", catalog.Items[0].Name);
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

        Assert.AreEqual(2, catalog.Items.Count);
        Assert.IsTrue(ContainsApp(catalog.Items, "FreshFirst"));
        Assert.IsTrue(ContainsApp(catalog.Items, "CachedSecond"));
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
        Assert.AreEqual(0, catalog.Items.Count);

        source.Invalidate(AppSourceInvalidatedEventArgs.ForPath(new AppSourcePathChange(WatcherChangeTypes.Changed, @"C:\Apps\First.exe")));
        await WaitForConditionAsync(() => !catalog.IsRefreshing);

        Assert.AreEqual(2, source.LoadCount);
        Assert.AreEqual(0, source.IncrementalLoadCount);
        Assert.AreEqual(2, catalog.Items.Count);
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
        catalog.Changed += (_, _) => publishedObsolete |= ContainsApp(catalog.Items, "Obsolete");
        var refresh = catalog.RefreshAsync();
        await WaitForConditionAsync(() => second.LoadCount == 2);
        provider.SetSources([replacement, second]);
        await WaitForConditionAsync(() => first.IsDisposed);
        secondLoad.SetResult([]);
        await refresh.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitForConditionAsync(() => ContainsApp(catalog.Items, "Replacement"));

        Assert.IsFalse(publishedObsolete);
    }

    [TestMethod]
    public async Task InitializeAsync_NoCache_LoadsAndPublishesSources()
    {
        var cache = new TestCache(null);
        using var source = new TestAppSource("test", [CreateCatalogItem("Fresh")]);
        using var catalog = CreateCatalog([source], cache);

        await catalog.InitializeAsync();

        Assert.AreEqual(1, source.LoadCount);
        Assert.AreEqual("Fresh", catalog.Items[0].Name);
        Assert.AreEqual(1, cache.SaveCount);
    }

    [TestMethod]
    public async Task GetSnapshot_ReusesTheCurrentAtomicPublication()
    {
        using var source = new TestAppSource("test", [CreateCatalogItem("First")]);
        using var catalog = CreateCatalog([source], new TestCache(null));
        await catalog.RefreshAsync();

        var firstSnapshot = catalog.GetSnapshot();

        Assert.AreSame(firstSnapshot, catalog.GetSnapshot());
        Assert.AreSame(firstSnapshot.Items, catalog.Items);
        Assert.AreSame(firstSnapshot.HiddenItems, catalog.HiddenItems);

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

        Assert.AreEqual(1, source.LoadCount);
        Assert.AreEqual(0, catalog.Items.Count);
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

        Assert.AreEqual(1, catalog.Items.Count);
        Assert.AreEqual("Preferred", catalog.Items[0].Name);
        Assert.AreEqual(2, catalog.Items[0].MatchTerms.Count);
        Assert.IsTrue(ContainsString(catalog.Items[0].MatchTerms, "Preferred"));
        Assert.IsTrue(ContainsString(catalog.Items[0].MatchTerms, "Duplicate"));
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

        Assert.AreEqual(1, catalog.Items.Count);
        Assert.IsTrue(logger.HasEvent(10));
    }

    [TestMethod]
    public async Task RefreshAsync_AppExecutionAlias_CoalescesPackagedAndExecutableRepresentations()
    {
        const string aumid = "CanonicalGroupLimited.Ubuntu_79rhkp1fndgsc!ubuntu";
        const string aliasPath = @"C:\Users\test\AppData\Local\Microsoft\WindowsApps\ubuntu.exe";
        const string targetPath = @"C:\Program Files\WindowsApps\CanonicalGroupLimited.Ubuntu_1.0.0.0_x64__79rhkp1fndgsc\ubuntu.exe";
        var aliasProgram = CreateAppExecutionAliasProgram("ubuntu", aliasPath, targetPath, aumid);
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

        Assert.AreEqual(1, catalog.Items.Count);
        Assert.AreEqual("Ubuntu", catalog.Items[0].Name);
        Assert.AreEqual(packagedIdentity, catalog.Items[0].CatalogId);
        Assert.IsTrue(catalog.Items[0].IsPackaged);
        Assert.IsTrue(ContainsString(catalog.Items[0].MatchTerms, "ubuntu.exe"));
        Assert.IsTrue(ContainsString(catalog.Items[0].MatchTerms, "Ubuntu"));
        var row = new AppListItem(catalog.Items[0], useThumbnails: false);
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

        Assert.AreEqual(1, catalog.Items.Count);
        Assert.AreEqual("ubuntu", catalog.Items[0].Name);
        Assert.AreEqual(aliasPath, catalog.Items[0].ExePath);
        Assert.AreEqual(targetPath, catalog.Items[0].FullExecutablePath);
        Assert.AreEqual(aumid, catalog.Items[0].UserModelId);
        Assert.AreEqual(packagedIdentity, catalog.Items[0].CatalogId);
        Assert.IsFalse(catalog.Items[0].IsPackaged);

        var legacyCommand = new AppCommand(new AppItem
        {
            Name = aliasProgram.Name,
            Subtitle = aliasProgram.Description,
            ExePath = aliasPath,
        });
        Assert.AreEqual(legacyCommand.Id, new AppCommand(catalog.Items[0]).Id);
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

        Assert.AreEqual(1, catalog.Items.Count);
        Assert.AreEqual("3D Viewer", catalog.Items[0].Name);
        Assert.AreEqual(packagedIdentity, catalog.Items[0].CatalogId);
        Assert.IsTrue(catalog.Items[0].IsPackaged);
        Assert.IsTrue(ContainsString(catalog.Items[0].MatchTerms, "3D Viewer - Shortcut"));
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

        Assert.AreEqual(2, catalog.Items.Count);
        Assert.IsTrue(ContainsApp(catalog.Items, "app"));
        Assert.IsTrue(ContainsApp(catalog.Items, "App profile"));
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

        Assert.AreEqual(3, catalog.Items.Count);
        Assert.IsTrue(ContainsApp(catalog.Items, "First alias"));
        Assert.IsTrue(ContainsApp(catalog.Items, "Second alias"));
        Assert.IsTrue(ContainsApp(catalog.Items, "Shared executable"));
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
        Assert.AreEqual("Old", catalog.Items[0].Name);

        completion.SetResult([CreateCatalogItem("New")]);
        await refresh;

        Assert.IsFalse(catalog.IsRefreshing);
        Assert.AreEqual("New", catalog.Items[0].Name);
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
    public async Task RefreshFailure_DrainsRefreshQueuedByFailingSubscriber()
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

        Assert.AreSame(refresh, queuedRefresh);
        Assert.AreEqual(2, source.LoadCount);
        Assert.AreEqual("Second", catalog.Items[0].Name);
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

        Assert.AreEqual("First", catalog.Items[0].Name);
        Assert.IsFalse(catalog.IsRefreshing);

        source.SetItems([CreateCatalogItem("Second")]);
        await catalog.RefreshAsync();

        Assert.AreEqual(2, source.LoadCount);
        Assert.AreEqual(2, cache.SaveCount);
        Assert.AreEqual("Second", catalog.Items[0].Name);
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
        Assert.IsTrue(ContainsApp(catalog.Items, "A2"));
        Assert.IsTrue(ContainsApp(catalog.Items, "B"));
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
        Assert.IsTrue(ContainsApp(catalog.Items, "A2"));
        Assert.IsTrue(ContainsApp(catalog.Items, "B"));

        provider.SetSources([unchanged]);
        await WaitForConditionAsync(() => !ContainsApp(catalog.Items, "A2"));
        Assert.IsTrue(ContainsApp(catalog.Items, "B"));
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
        var originalApp = catalog.Items[0];
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
        Assert.AreSame(originalApp, catalog.Items[0]);
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

        Assert.AreEqual(1, source.IncrementalLoadCount);
        Assert.IsFalse(catalog.IsRefreshing);
        Assert.AreEqual("Full", (source.LastIncrementalBaseItems[0].Payload as Win32AppPayload)?.Name);
        Assert.IsTrue(ContainsApp(catalog.Items, "Full"));
        Assert.IsTrue(ContainsApp(catalog.Items, "Incremental"));
        Assert.IsFalse(ContainsApp(catalog.Items, "Initial"));
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

        Assert.AreEqual(3, sourceA.LoadCount);
        Assert.AreEqual(0, sourceA.IncrementalLoadCount);
        Assert.AreEqual(2, sourceB.LoadCount);
        Assert.IsTrue(ContainsApp(catalog.Items, "After flood"));
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
        var originalItem = catalog.Items[0];
        var catalogId = originalItem.CatalogId;
        var catalogChangedCount = 0;
        var visibilityChangedCount = 0;
        catalog.Changed += (_, _) => catalogChangedCount++;
        catalog.VisibilityChanged += (_, _) => visibilityChangedCount++;

        await catalog.SetAppHiddenAsync(catalogId, hidden: true);

        Assert.AreEqual(1, source.LoadCount);
        Assert.AreEqual(0, catalog.Items.Count);
        Assert.AreSame(originalItem, catalog.HiddenItems[0]);
        Assert.AreEqual(0, catalogChangedCount);
        Assert.AreEqual(1, visibilityChangedCount);
        Assert.AreEqual(1, visibility.PersistCount);

        await catalog.SetAppHiddenAsync(catalogId, hidden: false);

        Assert.AreEqual(1, source.LoadCount);
        Assert.AreSame(originalItem, catalog.Items[0]);
        Assert.AreEqual(0, catalog.HiddenItems.Count);
        Assert.AreEqual(0, catalogChangedCount);
        Assert.AreEqual(2, visibilityChangedCount);
        Assert.AreEqual(2, visibility.PersistCount);
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
            File.Delete(settingsPath);
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
        await WaitForConditionAsync(() => catalog.Items.Count == 1 && changedCount == 1);

        Assert.AreEqual("Visible", catalog.Items[0].Name);
        Assert.AreEqual(1, source.LoadCount);
        Assert.AreEqual(1, cache.SaveCount);
        Assert.AreEqual(1, changedCount);
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

        Assert.AreEqual(1, catalog.Items.Count);
        Assert.AreEqual("Visible", catalog.Items[0].Name);
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
            var legacyId = new AppCommand(legacy.ToAppItem()).Id;
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
            Assert.IsNull(provider.GetCommandItem(legacyId));
            Assert.AreSame(canonical.MoreCommands, resolved.MoreCommands);
            Assert.AreEqual(Properties.Resources.unhide_app, resolved.MoreCommands.OfType<CommandContextItem>().Last().Command!.Name);
            await catalog.SetAppHiddenAsync(preferred.Identity, hidden: false);
            Assert.AreEqual(legacyId, provider.GetCommandItem(legacyId)?.Command?.Id);

            form.SubmitForm("{\"apps.ExcludedAppNames\":\"[\\\"*Editor*\\\"]\"}", string.Empty);
            await WaitForConditionAsync(() => list.GetSnapshot().PatternHiddenItems.Count == 1);
            Assert.IsNull(provider.GetCommandItem(legacyId));
            form.SubmitForm("{\"apps.ExcludedAppNames\":\"[]\"}", string.Empty);
            await WaitForConditionAsync(() => provider.GetCommandItem(legacyId) is not null);
        }
        finally
        {
            File.Delete(settingsPath);
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
            Assert.IsNull(provider.GetCommandItem(rows["Utility Updater"].Command!.Id));

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
            File.Delete(settingsPath);
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
            await WaitForConditionAsync(() => catalog.Items.Count == 1 && catalog.HiddenItems.Count == 0);

            Assert.AreEqual("Visible", catalog.Items[0].Name);
            Assert.AreEqual(1, source.LoadCount);
            Assert.AreEqual(1, cache.SaveCount);

            settingsForm.SubmitForm("{\"apps.HideUninstallers\":\"false\"}", string.Empty);
            await WaitForConditionAsync(() => catalog.HiddenItems.Count == 1);

            Assert.AreEqual("Uninstall Contoso", catalog.HiddenItems[0].Name);
            Assert.AreEqual(1, source.LoadCount);
            Assert.AreEqual(1, cache.SaveCount);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    private static AppCatalog CreateCatalog(
        IReadOnlyList<IAppSource> sources,
        IAppCatalogCache cache,
        IAppVisibilityStore? visibilityStore = null,
        IReadOnlyList<IAppCatalogFilter>? filters = null,
        MEL.ILogger<AppCatalog>? logger = null)
        => new(
            new MutableSourceProvider(sources),
            cache,
            visibilityStore ?? new VisibleApps(),
            filters,
            timeProvider: TestTimeProvider,
            invalidationDelay: TimeSpan.Zero,
            logger: logger);

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
        => new(
            identity,
            priority,
            new AppCatalogSourceReference(sourceId, program.FullPath),
            [program.Name],
            Win32AppPayload.From(program));

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

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
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

    private sealed class VisibleApps : IAppVisibilityStore
    {
        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        public AppVisibility GetVisibility(AppCatalogItem item) => AppVisibility.Visible;

        public bool SetHidden(AppCatalogItem item, bool hidden) => false;

        public void Persist()
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class MutableVisibilityStore : IAppVisibilityStore
    {
        private readonly HashSet<string> _hiddenIds = new(StringComparer.OrdinalIgnoreCase);

        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        public AppVisibility GetVisibility(AppCatalogItem item)
            => _hiddenIds.Contains(item.Identity) ? AppVisibility.Hidden : AppVisibility.Visible;

        public int PersistCount { get; private set; }

        public bool SetHidden(AppCatalogItem item, bool hidden)
        {
            return hidden ? _hiddenIds.Add(item.Identity) : _hiddenIds.Remove(item.Identity);
        }

        public void Persist() => PersistCount++;

        public void Dispose()
        {
        }
    }

    private sealed class MutableCatalogFilter : IAppCatalogFilter
    {
        private readonly HashSet<string> _excludedIds = new(StringComparer.OrdinalIgnoreCase);

        public event EventHandler? Changed;

        public bool Includes(AppCatalogItem item) => !_excludedIds.Contains(item.Identity);

        public void Exclude(string identity)
        {
            if (_excludedIds.Add(identity))
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Dispose() => Changed = null;
    }

    private sealed class ThrowingCatalogFilter : IAppCatalogFilter
    {
        private bool _fail;

        public event EventHandler? Changed;

        public bool Includes(AppCatalogItem item)
            => !_fail ? true : throw new InvalidOperationException("Test reprojection failure.");

        public void FailAndRaiseChanged()
        {
            _fail = true;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose() => Changed = null;
    }

    private sealed class RecordingLogger<T> : MEL.ILogger<T>
    {
        private readonly Lock _eventLock = new();
        private readonly HashSet<int> _eventIds = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

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
            }
        }

        public bool HasEvent(int eventId)
        {
            lock (_eventLock)
            {
                return _eventIds.Contains(eventId);
            }
        }
    }

    private sealed class TestCache : IAppCatalogCache
    {
        private readonly AppCatalogCacheFile? _cache;
        private Exception? _nextSaveException;
        private int _saveCount;

        public TestCache(AppCatalogCacheFile? cache)
        {
            _cache = cache;
        }

        public int SaveCount => Volatile.Read(ref _saveCount);

        public Task<AppCatalogCacheFile?>? DeferredLoad { get; init; }

        public IReadOnlyDictionary<string, IReadOnlyList<AppCatalogItem>>? LastSavedSnapshots { get; private set; }

        public Task<AppCatalogCacheFile?> LoadAsync(AppCatalogCacheContext context, CancellationToken cancellationToken)
            => DeferredLoad ?? Task.FromResult(_cache);

        public void FailNextSave(Exception exception)
            => _nextSaveException = exception;

        public Task SaveAsync(
            IReadOnlyDictionary<string, IReadOnlyList<AppCatalogItem>> sourceSnapshots,
            IReadOnlyCollection<string> fullyReconciledSourceIds,
            AppCatalogCacheContext context,
            CancellationToken cancellationToken)
        {
            LastSavedSnapshots = sourceSnapshots;
            var exception = Interlocked.Exchange(ref _nextSaveException, null);
            var save = exception is not null ? Task.FromException(exception) : Task.CompletedTask;
            // Publish the captured save state before a polling test observes the new count.
            Interlocked.Increment(ref _saveCount);
            return save;
        }
    }

    private sealed class MutableSourceProvider : IAppSourceProvider
    {
        private IReadOnlyList<IAppSource> _sources;

        public MutableSourceProvider(IReadOnlyList<IAppSource> sources)
        {
            _sources = sources;
        }

        public event EventHandler? Changed;

        public IReadOnlyList<IAppSource> GetSources() => _sources;

        public void SetSources(IReadOnlyList<IAppSource> sources)
        {
            _sources = sources;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose() => Changed = null;
    }

    private sealed class TestAppSource : IAppSource
    {
        private IReadOnlyList<AppCatalogItem> _items;
        private Task<IReadOnlyList<AppCatalogItem>>? _nextLoad;
        private Func<IReadOnlyList<AppCatalogItem>, IReadOnlyList<AppCatalogItem>>? _incrementalFactory;

        // Tests poll these start signals across threads. Capture each operation before incrementing;
        // the volatile getters then make that captured state visible to the waiting test.
        private int _loadCount;
        private int _incrementalLoadCount;

        public TestAppSource(string id, IReadOnlyList<AppCatalogItem> items)
        {
            Id = id;
            _items = items;
        }

        public event EventHandler<AppSourceInvalidatedEventArgs>? Invalidated;

        public string Id { get; }

        public string CacheKey => Id;

        public int LoadCount => Volatile.Read(ref _loadCount);

        public int IncrementalLoadCount => Volatile.Read(ref _incrementalLoadCount);

        public bool IsDisposed { get; private set; }

        public IReadOnlyList<AppCatalogItem> LastIncrementalBaseItems { get; private set; } = [];

        public IReadOnlyList<AppSourcePathChange> LastIncrementalChanges { get; private set; } = [];

        public Task<IReadOnlyList<AppCatalogItem>> LoadAsync(CancellationToken cancellationToken)
        {
            // Consume this load's gate before signaling: the waiting test may immediately supply the next one.
            var load = Interlocked.Exchange(ref _nextLoad, null) ?? Task.FromResult(_items);
            Interlocked.Increment(ref _loadCount);
            return load;
        }

        public void DeferNextLoad(Task<IReadOnlyList<AppCatalogItem>> nextLoad) => Interlocked.Exchange(ref _nextLoad, nextLoad);

        public void SetItems(IReadOnlyList<AppCatalogItem> items) => _items = items;

        public void SetIncrementalFactory(Func<IReadOnlyList<AppCatalogItem>, IReadOnlyList<AppCatalogItem>> incrementalFactory)
            => _incrementalFactory = incrementalFactory;

        public Task<IReadOnlyList<AppCatalogItem>> ApplyChangesAsync(
            IReadOnlyList<AppCatalogItem> currentItems,
            IReadOnlyList<AppSourcePathChange> changes,
            CancellationToken cancellationToken)
        {
            LastIncrementalBaseItems = currentItems;
            LastIncrementalChanges = new List<AppSourcePathChange>(changes);
            var incrementalFactory = _incrementalFactory;
            Interlocked.Increment(ref _incrementalLoadCount);
            return Task.FromResult(incrementalFactory?.Invoke(currentItems) ?? currentItems);
        }

        public void Invalidate(AppSourceInvalidatedEventArgs? args = null)
            => Invalidated?.Invoke(this, args ?? AppSourceInvalidatedEventArgs.FullRefresh);

        public void Dispose()
        {
            IsDisposed = true;
            Invalidated = null;
        }
    }
}
