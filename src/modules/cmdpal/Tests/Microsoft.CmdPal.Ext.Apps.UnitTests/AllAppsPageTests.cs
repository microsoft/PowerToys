// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class AllAppsPageTests : AppsTestBase
{
    [TestMethod]
    public void AppListItemSource_ResultLimitChangeNotifiesConsumersAndReusesRows()
    {
        MockCatalog.AddWin32Program(TestDataHelper.CreateTestWin32Program("Notepad"));
        var original = AppListItemSource.GetSnapshot().VisibleItems.Single();
        var originalSnapshot = AppListItemSource.GetSnapshot();
        var notified = false;
        AppListItemSource.Changed += (_, _) => notified = true;
        var settingsForm = (SettingsForm)Settings.Settings.ToContent().Single();

        settingsForm.SubmitForm("{\"apps.SearchResultLimit\":\"1\"}", "{}");

        Assert.IsTrue(notified);
        Assert.AreEqual(1, AppListItemSource.TopLevelResultLimit);
        Assert.AreSame(original, AppListItemSource.GetSnapshot().VisibleItems.Single());
        Assert.AreSame(originalSnapshot, AppListItemSource.GetSnapshot());
        Assert.AreEqual(0, MockCatalog.RefreshCallCount);
    }

    [TestMethod]
    public void AppListItemSource_UnrelatedSettingDoesNotReprojectOrNotify()
    {
        MockCatalog.AddWin32Program(TestDataHelper.CreateTestWin32Program("Notepad"));
        var snapshot = AppListItemSource.GetSnapshot();
        var notifications = 0;
        AppListItemSource.Changed += (_, _) => notifications++;
        var settingsForm = (SettingsForm)Settings.Settings.ToContent().Single();

        settingsForm.SubmitForm("{\"apps.EnableCatalogDiagnostics\":\"true\"}", "{}");

        Assert.AreEqual(0, notifications);
        Assert.AreSame(snapshot, AppListItemSource.GetSnapshot());
    }

    [TestMethod]
    public void AppListItemSource_LateCatalogChangeUsesCurrentSnapshot()
    {
        using var catalog = new MockAppCatalog();
        catalog.Changed += (_, args) =>
        {
            if (args.Changes.Any(change => change.Kind == AppCatalogChangeKind.Added))
            {
                catalog.ClearAll();
            }
        };
        using var source = new AppListItemSource(catalog, Settings);

        catalog.AddWin32Program(TestDataHelper.CreateTestWin32Program("Removed"));

        Assert.AreEqual(0, source.GetSnapshot().VisibleItems.Count);
    }

    [TestMethod]
    public async Task AppListItemSource_LateVisibilityChangeUsesCurrentSnapshot()
    {
        using var catalog = new MockAppCatalog();
        catalog.AddWin32Program(TestDataHelper.CreateTestWin32Program("Notepad"));
        catalog.VisibilityChanged += (_, args) =>
        {
            if (args.Hidden)
            {
                catalog.SetAppHiddenAsync(args.CatalogId, false).GetAwaiter().GetResult();
            }
        };
        using var source = new AppListItemSource(catalog, Settings);
        var original = source.GetSnapshot().VisibleItems.Single();

        await catalog.SetAppHiddenAsync(original.App.CatalogId, true);

        Assert.AreSame(original, source.GetSnapshot().VisibleItems.Single());
        Assert.AreEqual(0, source.GetSnapshot().HiddenItems.Count);
    }

    [TestMethod]
    public async Task AppListItemSource_VisibilityNotificationCanChangeVisibilityAgain()
    {
        using var catalog = new MockAppCatalog();
        catalog.AddWin32Program(TestDataHelper.CreateTestWin32Program("Notepad"));
        using var source = new AppListItemSource(catalog, Settings);
        var original = source.GetSnapshot().VisibleItems.Single();
        var unhidden = false;
        original.PropChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(AppListItem.MoreCommands) && !unhidden)
            {
                unhidden = true;
                catalog.SetAppHiddenAsync(original.App.CatalogId, false).GetAwaiter().GetResult();
            }
        };

        await catalog.SetAppHiddenAsync(original.App.CatalogId, true);

        Assert.IsTrue(unhidden);
        Assert.AreSame(original, source.GetSnapshot().VisibleItems.Single());
        Assert.AreEqual(0, source.GetSnapshot().HiddenItems.Count);
        Assert.AreEqual(Properties.Resources.hide_app, original.MoreCommands.OfType<CommandContextItem>().Last().Command.Name);
    }

    [TestMethod]
    public void AllAppsPage_Constructor_ThrowsOnNullAppListItemSource()
    {
        Assert.ThrowsException<ArgumentNullException>(() => new AllAppsPage(null!, TestDataHelper.CreateFuzzyMatcherProvider()));
    }

    [TestMethod]
    public void AllAppsPage_WithMockCatalog_InitializesSuccessfully()
    {
        using var mockCatalog = new MockAppCatalog();
        using var itemSource = new AppListItemSource(mockCatalog, Settings);

        var page = new AllAppsPage(itemSource, TestDataHelper.CreateFuzzyMatcherProvider());

        Assert.IsNotNull(page.Name);
        Assert.IsNotNull(page.Icon);
        Assert.AreEqual(1, mockCatalog.InitializeCallCount);
    }

    [TestMethod]
    public async Task AllAppsPage_Constructor_DoesNotWaitForCatalogInitialization()
    {
        var initialization = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var mockCatalog = new MockAppCatalog();
        mockCatalog.DeferInitialization(initialization.Task);
        mockCatalog.SetRefreshing(true);
        using var itemSource = new AppListItemSource(mockCatalog, Settings);

        var page = new AllAppsPage(itemSource, TestDataHelper.CreateFuzzyMatcherProvider());

        Assert.IsTrue(page.IsLoading);
        Assert.AreEqual($"{Properties.Resources.all_apps} ({Properties.Resources.refreshing_page_title_suffix})", page.Title);
        var items = page.GetItems();
        Assert.AreEqual(1, items.Length);
        Assert.AreEqual(Properties.Resources.refreshing_app_list, items[0].Title);

        initialization.SetResult(true);
        mockCatalog.SetRefreshing(false);
        await WaitForPageInitializationAsync(page);
        Assert.AreEqual(Properties.Resources.all_apps, page.Title);
        Assert.AreEqual(Properties.Resources.no_apps_found, page.GetItems().Single().Title);
    }

    [TestMethod]
    public async Task AllAppsPage_GetItems_ReturnsPlaceholderWithEmptyCatalog()
    {
        await WaitForPageInitializationAsync();

        var items = Page.GetItems();

        Assert.AreEqual(1, items.Length);
        Assert.AreEqual(Properties.Resources.no_apps_found, items[0].Title);
    }

    [TestMethod]
    public async Task AllAppsPage_GetItems_ReturnsAppsFromCatalogAsync()
    {
        using var mockCatalog = new MockAppCatalog();
        mockCatalog.AddWin32Program(TestDataHelper.CreateTestWin32Program("Notepad", @"C:\Windows\System32\notepad.exe"));
        mockCatalog.AddUWPApplication(TestDataHelper.CreateTestUWPApplication("Calculator"));
        using var itemSource = new AppListItemSource(mockCatalog, Settings);
        var page = new AllAppsPage(itemSource, TestDataHelper.CreateFuzzyMatcherProvider());
        await WaitForPageInitializationAsync(page);

        var items = page.GetItems();

        Assert.AreEqual(2, items.Length);
        Assert.IsTrue(items.Any(item => item.Title == "Notepad"));
        Assert.IsTrue(items.Any(item => item.Title == "Calculator"));
    }

    [TestMethod]
    public async Task AllAppsPage_GetItems_UsesCultureAwareNumericTitleOrdering()
    {
        using var mockCatalog = new MockAppCatalog();
        mockCatalog.AddWin32Program(TestDataHelper.CreateTestWin32Program("Zulu", @"C:\Apps\Zulu.exe"));
        mockCatalog.AddWin32Program(TestDataHelper.CreateTestWin32Program("alpha", @"C:\Apps\alpha.exe"));
        mockCatalog.AddWin32Program(TestDataHelper.CreateTestWin32Program("App 10", @"C:\Apps\App10.exe"));
        mockCatalog.AddWin32Program(TestDataHelper.CreateTestWin32Program("app 2", @"C:\Apps\App2.exe"));
        using var itemSource = new AppListItemSource(mockCatalog, Settings);
        using var page = new AllAppsPage(itemSource, TestDataHelper.CreateFuzzyMatcherProvider());
        await WaitForPageInitializationAsync(page);

        var items = page.GetItems();

        Assert.AreEqual("alpha", items[0].Title);
        Assert.AreEqual("app 2", items[1].Title);
        Assert.AreEqual("App 10", items[2].Title);
        Assert.AreEqual("Zulu", items[3].Title);
    }

    [TestMethod]
    public async Task AppListItemSource_UnhiddenItemPreservesAlphabeticalOrderIgnoringCase()
    {
        using var mockCatalog = new MockAppCatalog();
        mockCatalog.AddWin32Program(TestDataHelper.CreateTestWin32Program("alpha", @"C:\Apps\alpha.exe"));
        mockCatalog.AddWin32Program(TestDataHelper.CreateTestWin32Program("Beta", @"C:\Apps\Beta.exe"));
        mockCatalog.AddWin32Program(TestDataHelper.CreateTestWin32Program("Zulu", @"C:\Apps\Zulu.exe"));
        using var itemSource = new AppListItemSource(mockCatalog, Settings);
        using var page = new AllAppsPage(itemSource, TestDataHelper.CreateFuzzyMatcherProvider());
        await WaitForPageInitializationAsync(page);
        var beta = (AppListItem)page.GetItems()[1];

        await mockCatalog.SetAppHiddenAsync(beta.App.CatalogId, hidden: true);
        await mockCatalog.SetAppHiddenAsync(beta.App.CatalogId, hidden: false);
        var items = page.GetItems();

        Assert.AreEqual("alpha", items[0].Title);
        Assert.AreEqual("Beta", items[1].Title);
        Assert.AreEqual("Zulu", items[2].Title);
    }

    [TestMethod]
    public async Task AllAppsPage_TryGetCurrentItemUsesPublishedCatalogWithoutReloading()
    {
        var mockCache = new MockAppCache();
        mockCache.AddWin32Program(TestDataHelper.CreateTestWin32Program("Notepad", "C:\\Windows\\System32\\notepad.exe"));
        var page = new AllAppsPage(mockCache);
        await Task.Delay(100);

        var expected = page.GetItems().OfType<AppListItem>().Single();

        Assert.IsTrue(page.TryGetCurrentItem(expected.Command.Id, out var actual));
        Assert.AreSame(expected, actual);
        Assert.IsFalse(page.TryGetCurrentItem("missing", out var missing));
        Assert.IsNull(missing);
    }

    [TestMethod]
    public async Task AllAppsPage_GetItems_HidesSubtitlesWhenSettingEnabled()
    {
        using var mockCatalog = new MockAppCatalog();
        mockCatalog.AddWin32Program(TestDataHelper.CreateTestWin32Program("Notepad", @"C:\Windows\System32\notepad.exe"));

        try
        {
            Settings.Settings.Update("{\"apps.HideAppDescriptions\": \"true\"}");
            using var itemSource = new AppListItemSource(mockCatalog, Settings);
            var page = new AllAppsPage(itemSource, TestDataHelper.CreateFuzzyMatcherProvider());
            await WaitForPageInitializationAsync(page);

            var appItem = page.GetItems().OfType<AppListItem>().Single();

            Assert.AreEqual(string.Empty, appItem.Subtitle);
        }
        finally
        {
            Settings.Settings.Update("{\"apps.HideAppDescriptions\": \"false\"}");
        }
    }

    [TestMethod]
    public async Task AllAppsPage_SearchesSubtitleWhenDescriptionDisplayIsDisabled()
    {
        var program = TestDataHelper.CreateTestWin32Program("Utility", @"C:\Apps\Utility.exe");
        program.Description = "ContainsHiddenNeedle";

        try
        {
            Settings.Settings.Update("{\"apps.HideAppDescriptions\": \"true\"}");
            using var mockCatalog = new MockAppCatalog();
            mockCatalog.AddWin32Program(program);
            using var itemSource = new AppListItemSource(mockCatalog, Settings);
            using var page = new AllAppsPage(itemSource, TestDataHelper.CreateFuzzyMatcherProvider());
            await WaitForPageInitializationAsync(page);
            page.SearchText = "HiddenNeedle";

            var item = (AppListItem)page.GetItems().Single();

            Assert.AreEqual(string.Empty, item.Subtitle);
            Assert.AreEqual("Utility", item.Title);
        }
        finally
        {
            Settings.Settings.Update("{\"apps.HideAppDescriptions\": \"false\"}");
        }
    }

    [TestMethod]
    public async Task AllAppsPage_GetItems_ShowsSubtitlesWhenSettingDisabled()
    {
        using var mockCatalog = new MockAppCatalog();
        mockCatalog.AddWin32Program(TestDataHelper.CreateTestWin32Program("Notepad", @"C:\Windows\System32\notepad.exe"));

        try
        {
            Settings.Settings.Update("{\"apps.HideAppDescriptions\": \"false\"}");
            using var itemSource = new AppListItemSource(mockCatalog, Settings);
            var page = new AllAppsPage(itemSource, TestDataHelper.CreateFuzzyMatcherProvider());
            await WaitForPageInitializationAsync(page);

            var appItem = page.GetItems().OfType<AppListItem>().Single();

            Assert.IsFalse(string.IsNullOrEmpty(appItem.Subtitle));
        }
        finally
        {
            Settings.Settings.Update("{\"apps.HideAppDescriptions\": \"false\"}");
        }
    }

    [TestMethod]
    public void AppListItem_DefersWin32RowAndHeroIconsToUiLoader()
    {
        var app = new AppItem
        {
            Name = "Test App",
            IcoPath = "C:\\Windows\\System32\\shell32.dll,1",
            JumboIconPath = "C:\\Windows\\System32\\imageres.dll,2",
            ExePath = "C:\\Program Files\\Example\\app.exe",
        };

        var item = new AppListItem(app, useThumbnails: true);

        var rowIcon = (IconInfo)item.Icon!;
        Assert.IsTrue(AppIconProtocol.TryParse(rowIcon.Light.Icon, out var rowCandidates, out var rowJumbo));
        Assert.IsFalse(rowJumbo);
        CollectionAssert.AreEqual(new[] { app.IcoPath, app.ExePath }, rowCandidates);
        Assert.AreSame(rowIcon, item.Command.Icon);

        var details = (Details)item.Details!;
        var heroIcon = (IconInfo)details.HeroImage;
        Assert.IsTrue(AppIconProtocol.TryParse(heroIcon.Light.Icon, out var heroCandidates, out var heroJumbo));
        Assert.IsTrue(heroJumbo);
        CollectionAssert.AreEqual(new[] { app.JumboIconPath, app.IcoPath, app.ExePath }, heroCandidates);
    }

    [TestMethod]
    [DataRow(".lnk")]
    [DataRow(".LNK")]
    public void AppListItem_ShortcutHeroPrefersShellRenderingAndKeepsResourceFallbacks(string extension)
    {
        var app = new AppItem
        {
            Name = "Java",
            IcoPath = @"C:\Program Files\Java\java.exe,0",
            ExePath = @"C:\Start Menu\Java" + extension,
            FullExecutablePath = @"C:\Program Files\Java\javacpl.exe",
        };
        var item = new AppListItem(app, useThumbnails: true);

        var rowIcon = (IconInfo)item.Icon!;
        Assert.IsTrue(AppIconProtocol.TryParse(rowIcon.Light.Icon, out var rowCandidates, out var rowJumbo));
        Assert.IsFalse(rowJumbo);
        CollectionAssert.AreEqual(new[] { app.IcoPath, app.FullExecutablePath }, rowCandidates);

        var heroIcon = (IconInfo)((Details)item.Details!).HeroImage;
        Assert.IsTrue(AppIconProtocol.TryParse(heroIcon.Light.Icon, out var heroCandidates, out var heroJumbo));
        Assert.IsTrue(heroJumbo);
        CollectionAssert.AreEqual(new[] { app.ExePath, app.IcoPath, app.FullExecutablePath }, heroCandidates);
    }

    [TestMethod]
    public void AppListItem_ShortcutHeroKeepsExplicitJumboSourceFirst()
    {
        var app = new AppItem
        {
            Name = "Custom App",
            IcoPath = @"C:\Icons\small.ico,0",
            JumboIconPath = @"C:\Icons\large.ico,0",
            ExePath = @"C:\Start Menu\Custom App.lnk",
            FullExecutablePath = @"C:\Apps\custom.exe",
        };
        var item = new AppListItem(app, useThumbnails: true);
        var heroIcon = (IconInfo)((Details)item.Details!).HeroImage;

        Assert.IsTrue(AppIconProtocol.TryParse(heroIcon.Light.Icon, out var candidates, out var jumbo));
        Assert.IsTrue(jumbo);
        CollectionAssert.AreEqual(new[] { app.JumboIconPath, app.IcoPath, app.FullExecutablePath }, candidates);
    }

    [TestMethod]
    public void AppListItem_NamespaceShortcutHeroUsesOneShellCandidate()
    {
        var shortcutPath = @"C:\Desktop\About Java - Shortcut.lnk";
        var app = new AppItem
        {
            Name = "About Java",
            IcoPath = shortcutPath,
            ExePath = shortcutPath,
        };
        var item = new AppListItem(app, useThumbnails: true);
        var heroIcon = (IconInfo)((Details)item.Details!).HeroImage;

        Assert.IsTrue(AppIconProtocol.TryParse(heroIcon.Light.Icon, out var candidates, out var jumbo));
        Assert.IsTrue(jumbo);
        CollectionAssert.AreEqual(new[] { shortcutPath }, candidates);
    }

    [TestMethod]
    public void AppListItem_KeepsPackagedIconAssetsAsDirectPaths()
    {
        var app = new AppItem
        {
            Name = "Test Packaged App",
            IcoPath = "C:\\Program Files\\WindowsApps\\Example\\small.png",
            JumboIconPath = "C:\\Program Files\\WindowsApps\\Example\\large.png",
            IsPackaged = true,
        };

        var item = new AppListItem(app, useThumbnails: true);

        var rowIcon = (IconInfo)item.Icon!;
        Assert.AreEqual(app.IcoPath, rowIcon.Light.Icon);

        var details = (Details)item.Details!;
        var heroIcon = (IconInfo)details.HeroImage;
        Assert.AreEqual(app.JumboIconPath, heroIcon.Light.Icon);
    }

    [TestMethod]
    public async Task AppListItemSource_DescriptionSettingReprojectsWithoutHoldingStateLock()
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), $"apps-settings-{Guid.NewGuid():N}.json");
        try
        {
            var settings = new AllAppsSettings(settingsPath);
            using var mockCatalog = new MockAppCatalog();
            mockCatalog.AddWin32Program(TestDataHelper.CreateTestWin32Program("Notepad"));
            using var itemSource = new AppListItemSource(mockCatalog, settings);
            using var page = new AllAppsPage(itemSource, TestDataHelper.CreateFuzzyMatcherProvider());
            await WaitForPageInitializationAsync(page);
            var originalItem = (AppListItem)page.GetItems().Single();
            var settingsForm = (SettingsForm)settings.Settings.ToContent().Single();
            var snapshotReadCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var readerReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var readRequested = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            // Start a dedicated reader before triggering the callback, so thread-pool starvation
            // cannot look like a held state lock.
            var snapshotRead = Task.Factory.StartNew(
                () =>
                {
                    readerReady.SetResult();
                    if (readRequested.Task.GetAwaiter().GetResult())
                    {
                        itemSource.GetSnapshot();
                    }
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
            try
            {
                await readerReady.Task.WaitAsync(TimeSpan.FromSeconds(5));
                originalItem.PropChanged += (_, args) =>
                {
                    if (string.Equals(args.PropertyName, nameof(AppListItem.Subtitle), StringComparison.Ordinal))
                    {
                        readRequested.TrySetResult(true);
                        // The read must finish during the callback; reading afterward would miss a held lock.
                        snapshotReadCompleted.TrySetResult(snapshotRead.Wait(TimeSpan.FromSeconds(5)));
                    }
                };

                settingsForm.SubmitForm("{\"apps.HideAppDescriptions\":\"true\"}", "{}");

                Assert.IsTrue(await snapshotReadCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5)), "The snapshot read must finish during the property notification.");
                var updatedItem = (AppListItem)page.GetItems().Single();
                Assert.AreSame(originalItem, updatedItem);
                Assert.AreEqual(string.Empty, updatedItem.Subtitle);
                Assert.AreEqual(0, mockCatalog.RefreshCallCount);
            }
            finally
            {
                // Release the reader if setup failed before a property notification requested a read.
                readRequested.TrySetResult(false);
                await snapshotRead.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [TestMethod]
    public async Task AllAppsPage_BackgroundRefreshKeepsExistingRowsWithoutLoadingBanner()
    {
        using var mockCatalog = new MockAppCatalog();
        mockCatalog.AddWin32Program(TestDataHelper.CreateTestWin32Program("Notepad"));
        using var itemSource = new AppListItemSource(mockCatalog, Settings);
        var page = new AllAppsPage(itemSource, TestDataHelper.CreateFuzzyMatcherProvider());
        await WaitForPageInitializationAsync(page);

        mockCatalog.SetRefreshing(true);
        var items = page.GetItems();

        Assert.IsFalse(page.IsLoading);
        Assert.AreEqual(Properties.Resources.all_apps, page.Title);
        Assert.AreEqual(1, items.Length);
        Assert.AreEqual("Notepad", items[0].Title);
    }

    [TestMethod]
    public async Task AllAppsPage_GetItems_RefreshesAppsWhenIndexingCompletes()
    {
        using var mockCatalog = new MockAppCatalog();
        mockCatalog.AddWin32Program(TestDataHelper.CreateTestWin32Program("Notepad"));
        using var itemSource = new AppListItemSource(mockCatalog, Settings);
        var page = new AllAppsPage(itemSource, TestDataHelper.CreateFuzzyMatcherProvider());
        await WaitForPageInitializationAsync(page);
        mockCatalog.SetRefreshing(true);
        mockCatalog.AddWin32Program(TestDataHelper.CreateTestWin32Program("Paint"));

        mockCatalog.SetRefreshing(false);
        await WaitForPageInitializationAsync(page);
        var items = page.GetItems();

        Assert.IsFalse(page.IsLoading);
        Assert.AreEqual(Properties.Resources.all_apps, page.Title);
        Assert.AreEqual(2, items.Length);
        Assert.IsTrue(items.Any(item => item.Title == "Notepad"));
        Assert.IsTrue(items.Any(item => item.Title == "Paint"));
    }

    [TestMethod]
    public async Task AllAppsPage_GetItems_AppliesCatalogDeltaWhileIndexing()
    {
        using var mockCatalog = new MockAppCatalog();
        mockCatalog.AddWin32Program(TestDataHelper.CreateTestWin32Program("Notepad"));
        using var itemSource = new AppListItemSource(mockCatalog, Settings);
        var page = new AllAppsPage(itemSource, TestDataHelper.CreateFuzzyMatcherProvider());
        await WaitForPageInitializationAsync(page);
        var notepadItem = page.GetItems().Single(item => item.Title == "Notepad");
        mockCatalog.SetRefreshing(true);

        mockCatalog.AddWin32Program(TestDataHelper.CreateTestWin32Program("Paint"));
        var items = page.GetItems();

        Assert.IsFalse(page.IsLoading);
        Assert.AreEqual(2, items.Length);
        Assert.IsTrue(items.Any(item => item.Title == "Paint"));
        Assert.AreSame(notepadItem, items.Single(item => item.Title == "Notepad"));

        mockCatalog.SetRefreshing(false);
        await WaitForPageInitializationAsync(page);
    }

    [TestMethod]
    public async Task AllAppsPage_MoreCommands_RefreshesCatalogInBackground()
    {
        using var mockCatalog = new MockAppCatalog();
        using var itemSource = new AppListItemSource(mockCatalog, Settings);
        var page = new AllAppsPage(itemSource, TestDataHelper.CreateFuzzyMatcherProvider());
        await WaitForPageInitializationAsync(page);
        var refreshItem = page.MoreCommands.OfType<CommandContextItem>().Single();
        var refreshCommand = (AnonymousCommand)refreshItem.Command;

        var result = refreshCommand.Invoke();
        await WaitForPageInitializationAsync(page);

        Assert.AreEqual(1, mockCatalog.RefreshCallCount);
        Assert.AreEqual(CommandResult.KeepOpen().Kind, result.Kind);
    }

    [TestMethod]
    public async Task AllAppsPage_UserRefreshRemainsLoadingUntilAllRequestsFinish()
    {
        MockCatalog.AddWin32Program(TestDataHelper.CreateTestWin32Program("Notepad"));
        var firstCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        MockCatalog.RefreshCompletion = firstCompletion.Task;
        var first = AppListItemSource.RefreshAsync();
        MockCatalog.RefreshCompletion = secondCompletion.Task;
        var second = AppListItemSource.RefreshAsync();

        Assert.IsTrue(Page.IsLoading);
        Assert.AreEqual(Properties.Resources.refreshing_app_list, Page.GetItems()[0].Title);
        Assert.IsTrue(Page.GetItems().Any(item => item.Title == "Notepad"));

        firstCompletion.SetResult();
        await first.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(Page.IsLoading);
        secondCompletion.SetResult();
        await second.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.IsFalse(Page.IsLoading);
        Assert.AreEqual(Properties.Resources.all_apps, Page.Title);
        Assert.AreEqual("Notepad", Page.GetItems().Single().Title);
    }

    [TestMethod]
    public void AllAppsPage_IsDynamicAndExposesExpectedFilters()
    {
        Assert.IsInstanceOfType<DynamicListPage>(Page);

        var filters = Page.Filters!.GetFilters();
        Assert.AreEqual(6, filters.Length);
        Assert.AreEqual(AllAppsFilters.AllFilterId, ((IFilter)filters[0]).Id);
        Assert.IsInstanceOfType<Separator>(filters[1]);
        Assert.AreEqual(AllAppsFilters.Win32FilterId, ((IFilter)filters[2]).Id);
        Assert.AreEqual(AllAppsFilters.PackagedFilterId, ((IFilter)filters[3]).Id);
        Assert.IsInstanceOfType<Separator>(filters[4]);
        Assert.AreEqual(AllAppsFilters.HiddenFilterId, ((IFilter)filters[5]).Id);
    }

    [TestMethod]
    public async Task AllAppsPage_SearchesApplicationMetadata()
    {
        MockCatalog.AddWin32Program(TestDataHelper.CreateTestWin32Program("Editor", @"C:\Portable\NeedleTool.exe"));
        await WaitForPageInitializationAsync();

        Page.SearchText = "NeedleTool";
        var items = Page.GetItems();

        Assert.AreEqual(1, items.Length);
        Assert.AreEqual("Editor", items[0].Title);
    }

    [TestMethod]
    public void AllAppsPage_SearchDoesNotMatchGenericApplicationType()
    {
        foreach (var name in new[] { "Editor", "Apple" })
        {
            var program = TestDataHelper.CreateTestWin32Program(name, $@"C:\Tools\{name}.exe");
            program.ParentDirectory = @"C:\Tools";
            program.ExecutableName = $"{name}.exe";
            MockCatalog.AddWin32Program(program);
        }

        Page.SearchText = "app";

        Assert.AreEqual("Apple", Page.GetItems().Single().Title);
    }

    [TestMethod]
    [DataRow("LegacyAliasNeedle")]
    [DataRow("ResolvedTargetNeedle")]
    [DataRow("PackagedIdentityNeedle")]
    public async Task AllAppsPage_SearchesAggregatedAndDirectMetadata(string query)
    {
        using var mockCatalog = new MockAppCatalog();
        mockCatalog.AddWin32Program(TestDataHelper.CreateTestWin32Program("Editor", @"C:\Apps\Editor.exe"));
        mockCatalog.Items[0].MatchTerms = ["LegacyAliasNeedle"];
        mockCatalog.Items[0].FullExecutablePath = @"C:\ResolvedTargetNeedle\Editor.exe";
        mockCatalog.Items[0].UserModelId = "Contoso.PackagedIdentityNeedle!Editor";
        using var itemSource = new AppListItemSource(mockCatalog, Settings);
        using var page = new AllAppsPage(itemSource, TestDataHelper.CreateFuzzyMatcherProvider());
        await WaitForPageInitializationAsync(page);

        page.SearchText = query;

        Assert.AreEqual("Editor", page.GetItems().Single().Title);
    }

    [TestMethod]
    [DataRow("camra", "Camera")]
    [DataRow("clculator", "Calculator")]
    [DataRow("ntpad", "Notepad")]
    [DataRow("cptr", "Capture")]
    public async Task AllAppsPage_MissingLettersMatchNamesAndDescriptions(string query, string name)
    {
        MockCatalog.AddWin32Program(TestDataHelper.CreateTestWin32Program(query, $@"C:\Apps\{query}.exe"));
        MockCatalog.AddWin32Program(TestDataHelper.CreateTestWin32Program(name, $@"C:\Apps\{name}.exe"));
        var describedApp = TestDataHelper.CreateTestWin32Program("Utility", @"C:\Tools\Utility.exe");
        describedApp.Description = name;
        MockCatalog.AddWin32Program(describedApp);
        var unrelatedApp = TestDataHelper.CreateTestWin32Program("Unrelated", @"C:\Tools\Unrelated.exe");
        unrelatedApp.Description = string.Empty;
        MockCatalog.AddWin32Program(unrelatedApp);
        await WaitForPageInitializationAsync();

        Page.SearchText = query;

        CollectionAssert.AreEqual(new[] { query, name, "Utility" }, Page.GetItems().Select(item => item.Title).ToArray());
    }

    [TestMethod]
    public async Task AllAppsPage_SearchRanksStrongNameMatchesFirstAndExcludesWeakMetadata()
    {
        MockCatalog.AddWin32Program(TestDataHelper.CreateTestWin32Program("Notepad", @"C:\Apps\Notepad.exe"));
        MockCatalog.AddWin32Program(TestDataHelper.CreateTestWin32Program("NxxOxxTxxE", @"C:\Apps\NxxOxxTxxE.exe"));
        var metadataOnly = TestDataHelper.CreateTestWin32Program("Editor", @"C:\Metadata\NxxOxxTxxE.exe");
        metadataOnly.Description = string.Empty;
        MockCatalog.AddWin32Program(metadataOnly);
        await WaitForPageInitializationAsync();

        Page.SearchText = "note";
        var items = Page.GetItems();

        Assert.AreEqual(2, items.Length);
        Assert.AreEqual("Notepad", items[0].Title);
        Assert.AreEqual("NxxOxxTxxE", items[1].Title);
    }

    [TestMethod]
    public async Task AllAppsPage_TypeFiltersOnlyReturnMatchingApplications()
    {
        MockCatalog.AddWin32Program(TestDataHelper.CreateTestWin32Program("Notepad"));
        MockCatalog.AddUWPApplication(TestDataHelper.CreateTestUWPApplication("Calculator"));
        await WaitForPageInitializationAsync();

        Page.Filters!.CurrentFilterId = AllAppsFilters.Win32FilterId;
        var win32Items = Page.GetItems();
        Assert.AreEqual(1, win32Items.Length);
        Assert.IsFalse(((AppListItem)win32Items[0]).App.IsPackaged);

        Page.Filters.CurrentFilterId = AllAppsFilters.PackagedFilterId;
        var packagedItems = Page.GetItems();
        Assert.AreEqual(1, packagedItems.Length);
        Assert.IsTrue(((AppListItem)packagedItems[0]).App.IsPackaged);
    }

    [TestMethod]
    public async Task AllAppsPage_HideAndUnhideCommandsMoveApplicationBetweenFilters()
    {
        MockCatalog.AddWin32Program(TestDataHelper.CreateTestWin32Program("Notepad"));
        await WaitForPageInitializationAsync();

        var visibleItem = (AppListItem)Page.GetItems().Single();
        var hideCommand = visibleItem.MoreCommands
            .OfType<CommandContextItem>()
            .Single(item => item.Command.Name == Properties.Resources.hide_app)
            .Command;

        ((AnonymousCommand)hideCommand).Invoke();
        await WaitForConditionAsync(() => AppListItemSource.GetSnapshot().VisibleItems.Count == 0);

        Assert.AreEqual(Properties.Resources.no_apps_found, Page.GetItems().Single().Title);
        Page.Filters!.CurrentFilterId = AllAppsFilters.HiddenFilterId;
        var hiddenItem = Page.GetItems().OfType<AppListItem>().Single();
        Assert.AreSame(visibleItem, hiddenItem);
        var unhideCommand = hiddenItem.MoreCommands
            .OfType<CommandContextItem>()
            .Single(item => item.Command.Name == Properties.Resources.unhide_app)
            .Command;

        ((AnonymousCommand)unhideCommand).Invoke();
        await WaitForConditionAsync(() => AppListItemSource.GetSnapshot().HiddenItems.Count == 0);

        Assert.AreEqual(Properties.Resources.no_apps_found, Page.GetItems().Single().Title);
        Page.Filters.CurrentFilterId = AllAppsFilters.AllFilterId;
        Assert.AreSame(visibleItem, Page.GetItems().Single());
    }

    [TestMethod]
    public async Task AllAppsPage_HideCommandMovesApplicationWhileCatalogIsRefreshing()
    {
        MockCatalog.AddWin32Program(TestDataHelper.CreateTestWin32Program("Notepad"));
        await WaitForPageInitializationAsync();
        MockCatalog.SetRefreshing(true);

        var visibleItem = AppListItemSource.GetSnapshot().VisibleItems.Single();
        var hideCommand = visibleItem.MoreCommands
            .OfType<CommandContextItem>()
            .Single(item => item.Command.Name == Properties.Resources.hide_app)
            .Command;

        ((AnonymousCommand)hideCommand).Invoke();
        await WaitForConditionAsync(() => AppListItemSource.GetSnapshot().VisibleItems.Count == 0);

        Assert.IsTrue(MockCatalog.IsRefreshing);
        Assert.AreSame(visibleItem, AppListItemSource.GetSnapshot().HiddenItems.Single());

        MockCatalog.SetRefreshing(false);
        await WaitForPageInitializationAsync();
    }

    [TestMethod]
    public async Task AppListItemSource_CatalogVisibilityUpdatePreservesExistingListItem()
    {
        MockCatalog.AddWin32Program(TestDataHelper.CreateTestWin32Program("Notepad"));
        await WaitForPageInitializationAsync();
        var originalItem = AppListItemSource.GetSnapshot().VisibleItems.Single();

        MockCatalog.PublishVisibilityAsCatalogChange(originalItem.App.CatalogId, hidden: true);

        var hiddenItem = AppListItemSource.GetSnapshot().HiddenItems.Single();
        Assert.AreSame(originalItem, hiddenItem);
        Assert.IsTrue(hiddenItem.MoreCommands
            .OfType<CommandContextItem>()
            .Any(item => item.Command.Name == Properties.Resources.unhide_app));
    }
}
