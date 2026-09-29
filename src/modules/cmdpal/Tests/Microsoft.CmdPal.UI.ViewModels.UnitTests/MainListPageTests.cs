// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.Ext.Apps;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.UI.ViewModels.MainPage;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public partial class MainListPageTests
{
    private static readonly string[] MetadataQueries = ["LegacyAliasNeedle", "ResolvedTargetNeedle", "LaunchNeedle", "PackagedIdentityNeedle", "FamilyNeedle", "DescriptionNeedle", "wt", "wt.exe", "vv"];
    private static readonly string[] NonAppQueries = ["c", "Application", "Apps"];
    private static readonly string[] InfrastructureQueries = ["start", "program", "windows", "micro", "apps"];

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task Search_FindsCanonicalAppMetadataOnBothPages(bool pinned, bool useSavedAlias)
    {
        var app = new AppListItem(
            new AppItem
            {
                Name = "Editor",
                AppIdentifier = "editor",
                Subtitle = "DescriptionNeedle",
                Type = "Application",
                ExePath = @"C:\Tools\LaunchNeedle.lnk",
                FullExecutablePath = @"C:\ResolvedTargetNeedle\Editor.exe",
                UserModelId = "Contoso.PackagedIdentityNeedle!Editor",
                PackageFamilyName = "Contoso.FamilyNeedle_publisher",
                MatchTerms = ["LegacyAliasNeedle", @"C:\Aliases\wt.exe", "vv"],
            },
            useThumbnails: false);
        app.Subtitle = string.Empty;
        var commandId = useSavedAlias ? CreateApp("Legacy Editor").Command!.Id : app.Command!.Id;
        app.App.CommandIds = [commandId];
        await WithSearchPages(new AppListItemSnapshot([app, CreateApp("Other")], []), pinned, pinnedCommandId: commandId, check: (home, allApps, _) =>
        {
            foreach (var query in MetadataQueries)
            {
                home.SearchText = allApps.SearchText = query;
                var result = home.GetItems().Where(item => item is not Separator).Single();
                Assert.AreEqual(commandId, result.Command!.Id, query);
                Assert.AreEqual(pinned, result is TopLevelViewModel, query);
                Assert.AreEqual(string.Empty, result.Subtitle);
                Assert.AreSame(app, allApps.GetItems().OfType<AppListItem>().Single(), query);
            }

            foreach (var query in NonAppQueries)
            {
                home.SearchText = query;
                Assert.IsFalse(home.GetItems().Any(item => item.Command?.Id == commandId), query);
            }

            Assert.IsTrue(home.GetItems().Any(item => item.Title == "Browse apps"), "The navigation command keeps ordinary command matching.");
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Search_MissingLetterFindsAppOnBothPagesIncludingPins(bool pinned)
    {
        var app = CreateApp("Camera");
        await WithSearchPages(new AppListItemSnapshot([app, CreateApp("Other")], []), pinned, (home, allApps, _) =>
        {
            home.SearchText = allApps.SearchText = "camra";

            Assert.AreEqual(1, home.GetItems().Count(item => item.Command?.Id == app.Command!.Id));
            Assert.AreSame(app, allApps.GetItems().OfType<AppListItem>().Single());
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    [DataRow("cmd", false)]
    [DataRow("cmd.exe", false)]
    [DataRow("CMD.EXE", false)]
    [DataRow("cmd", true)]
    [DataRow("cmd.exe", true)]
    public async Task Search_ExecutableNamesPreferPlainCommandPromptOnBothPages(string query, bool pinned)
    {
        var commandPrompt = new AppListItem(
            new AppItem
            {
                Name = "Command Prompt",
                ExePath = @"C:\Start Menu\Command Prompt.lnk",
                FullExecutablePath = @"C:\Windows\System32\cmd.exe",
            },
            useThumbnails: false);
        var developerPrompt = new AppListItem(
            new AppItem
            {
                Name = "Developer Command Prompt for VS",
                ExePath = @"C:\Start Menu\Developer Command Prompt.lnk",
                FullExecutablePath = @"C:\Windows\System32\cmd.exe",
                Arguments = "/k setup.bat",
            },
            useThumbnails: false);
        var perlPrompt = new AppListItem(
            new AppItem
            {
                Name = "Perl (command line)",
                ExePath = @"C:\Start Menu\Perl.lnk",
                FullExecutablePath = @"C:\Windows\SysWOW64\cmd.exe",
            },
            useThumbnails: false);
        var cmdPal = CreateApp("CmdPalCatFunExtension");
        var metadata = new AppListItem(
            new AppItem { Name = "A Metadata Only", MatchTerms = ["cmd", "cmd.exe"] },
            useThumbnails: false);
        var history = new RecentCommandsManager();
        for (var i = 0; i < 100; i++)
        {
            history = history.WithHistoryItem(cmdPal.Command!.Id).WithHistoryItem(developerPrompt.Command!.Id);
        }

        var stateService = new Mock<IAppStateService>();
        stateService.SetupGet(service => service.State).Returns(new AppStateModel { RecentCommands = history });
        var snapshot = new AppListItemSnapshot([commandPrompt, perlPrompt, developerPrompt, cmdPal, metadata, CreateApp("Cosmos DB Explorer"), CreateApp("ECP3: CmdPal extension")], []);
        await WithSearchPages(snapshot, pinned, appStateService: stateService, check: (home, allApps, _) =>
        {
            home.SearchText = allApps.SearchText = query;
            Assert.AreEqual(commandPrompt.Command!.Id, home.GetItems().First(item => item is not Separator).Command!.Id);
            Assert.AreSame(commandPrompt, allApps.GetItems().OfType<AppListItem>().First());
            Assert.IsTrue(home.GetItems().Any(item => item.Command?.Id == developerPrompt.Command!.Id), "Argument-bearing profiles must remain searchable.");
            Assert.IsTrue(allApps.GetItems().Contains(developerPrompt));
            Assert.AreEqual(1, home.GetItems().Count(item => item.Command?.Id == commandPrompt.Command!.Id));
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    [DataRow("cmd")]
    [DataRow("cmd.exe")]
    public async Task Search_ExactTitleKeepsPrecedenceOverExecutableOnBothPages(string query)
    {
        var title = CreateApp(query);
        var executable = new AppListItem(
            new AppItem { Name = "Command Prompt", ExePath = @"C:\Windows\System32\cmd.exe" },
            useThumbnails: false);
        await WithSearchPages(new AppListItemSnapshot([executable, title], []), false, (home, allApps, _) =>
        {
            home.SearchText = allApps.SearchText = query;
            Assert.AreEqual(title.Command!.Id, home.GetItems().First(item => item is not Separator).Command!.Id);
            Assert.AreSame(title, allApps.GetItems().OfType<AppListItem>().First());
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Search_SharedPathPrefixesDoNotAdmitAppsOnEitherPage(bool pinned)
    {
        var programs = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
        var shortcutPath = Path.Combine(programs, "Acme", "Editor.lnk");
        var targetPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps", "Acme.Editor_1.0_x64__publisher", "Editor.exe");
        var app = new AppListItem(
            new AppItem
            {
                Name = "Editor",
                AppIdentifier = "editor",
                ExePath = shortcutPath,
                FullExecutablePath = targetPath,
                DirPath = programs,
                MatchTerms = [shortcutPath, targetPath, programs],
            },
            useThumbnails: false);
        await WithSearchPages(new AppListItemSnapshot([app], []), pinned, (home, allApps, _) =>
        {
            foreach (var query in InfrastructureQueries)
            {
                home.SearchText = allApps.SearchText = query;
                Assert.IsFalse(home.GetItems().Any(item => item.Command?.Id == app.Command!.Id), query);
                Assert.IsFalse(allApps.GetItems().Any(item => item is AppListItem), query);
            }

            foreach (var query in new[] { "Acme", "Editor.exe", shortcutPath, targetPath, @"Acme\Editor.lnk" })
            {
                home.SearchText = allApps.SearchText = query;
                Assert.AreEqual(1, home.GetItems().Count(item => item.Command?.Id == app.Command!.Id), query);
                Assert.AreSame(app, allApps.GetItems().OfType<AppListItem>().Single(), query);
            }

            return Task.CompletedTask;
        });
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExtendingQuery_ReconsidersAppsRejectedByMetadataAndShortQueryRules(bool pinned)
    {
        var app = new AppListItem(
            new AppItem { Name = "Editor", AppIdentifier = "editor", MatchTerms = ["xxneedle", @"C:\Aliases\wt.exe"] },
            useThumbnails: false);
        var companion = CreateApp("Needle Word");
        await WithSearchPages(new AppListItemSnapshot([app, companion], []), pinned, (home, _, _) =>
        {
            foreach (var (shorter, longer) in new[] { ("nee", "need"), ("w", "wt") })
            {
                home.SearchText = shorter;
                Assert.IsTrue(home.GetItems().Any(item => item.Command?.Id == companion.Command!.Id), "A prior match keeps the narrowing optimization active.");
                Assert.IsFalse(home.GetItems().Any(item => item.Command?.Id == app.Command!.Id), shorter);

                home.SearchText = longer;
                Assert.AreEqual(1, home.GetItems().Count(item => item.Command?.Id == app.Command!.Id), longer);
            }

            return Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task Search_UsesVisibilityPolicyAndHiddenSections()
    {
        var manual = CreateApp("Manual Needle");
        var excluded = CreateApp("Excluded Needle");
        var snapshot = new AppListItemSnapshot([CreateApp("Editor")], [manual], [excluded]);
        await WithSearchPages(snapshot, false, (home, allApps, _) =>
        {
            home.SearchText = allApps.SearchText = "Needle";
            Assert.IsFalse(home.GetItems().Any(item => item is AppListItem));
            Assert.IsFalse(allApps.GetItems().Any(item => item is AppListItem));
            allApps.Filters!.CurrentFilterId = allApps.Filters!.GetFilters().OfType<IFilter>().Last().Id;

            CollectionAssert.AreEqual(new[] { manual, excluded }, allApps.GetItems().OfType<AppListItem>().ToArray());
            Assert.AreEqual(2, allApps.GetItems().OfType<Separator>().Count());
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task PinAndUnpin_PreserveMetadataSearchWithoutDuplicates(bool useSavedAlias)
    {
        var app = new AppListItem(
            new AppItem { Name = "Editor", AppIdentifier = "editor", MatchTerms = ["Needle"] },
            useThumbnails: false);
        var commandId = useSavedAlias ? CreateApp("Legacy Editor").Command!.Id : app.Command!.Id;
        app.App.CommandIds = [commandId];
        await WithSearchPages(new AppListItemSnapshot([app], []), false, async (home, _, manager) =>
        {
            home.SearchText = "Needle";
            Assert.AreSame(app, home.GetItems().OfType<AppListItem>().Single());

            manager.Receive(new PinCommandItemMessage(AllAppsCommandProvider.WellKnownId, commandId));
            await WaitForConditionAsync(() => home.GetItems().Any(item => item is TopLevelViewModel && item.Command?.Id == commandId));
            var pin = home.GetItems().Where(item => item is not Separator).Single();
            Assert.IsInstanceOfType<TopLevelViewModel>(pin);
            Assert.AreEqual(commandId, pin.Command!.Id);

            manager.Receive(new UnpinCommandItemMessage(AllAppsCommandProvider.WellKnownId, commandId));
            await WaitForConditionAsync(() => home.GetItems().Any(item => ReferenceEquals(item, app)));
            Assert.AreSame(app, home.GetItems().Where(item => item is not Separator).Single());
        });
    }

    [TestMethod]
    public void AppScores_KeepTitleTiersHistoryAndDescriptionAdmission()
    {
        var exact = CreateApp("Needle");
        var metadata = new AppListItem(
            new AppItem { Name = "Editor", AppIdentifier = "editor", MatchTerms = ["Needle"] },
            useThumbnails: false);
        var weakTitle = CreateApp("NxxOxxTxxE");
        var description = new AppListItem(
            new AppItem { Name = "Editor", AppIdentifier = "description", Subtitle = "NxxOxxTxxE" },
            useThumbnails: false) { Subtitle = string.Empty };
        var history = new RecentCommandsManager();
        for (var i = 0; i < 100; i++)
        {
            history = history.WithHistoryItem(metadata.Command!.Id);
        }

        var matcher = new PrecomputedFuzzyMatcher();
        var query = matcher.PrecomputeQuery("Needle");
        var exactScore = MainListPage.ScoreTopLevelItem(query, exact, history, matcher);
        var metadataScore = MainListPage.ScoreTopLevelItem(query, metadata, history, matcher);
        Assert.AreEqual(RankTier.ExactTitle, MainListRanker.TierOf(exactScore));
        Assert.AreEqual(RankTier.Fuzzy, MainListRanker.TierOf(metadataScore));
        Assert.IsTrue(exactScore > metadataScore);
        Assert.AreEqual(0, MainListPage.ScoreTopLevelItem(matcher.PrecomputeQuery("zebra"), metadata, history, matcher));

        query = matcher.PrecomputeQuery("note");
        Assert.IsTrue(new AppSearch("note", matcher).Evaluate(weakTitle).HasMatch);
        Assert.IsTrue(MainListPage.ScoreTopLevelItem(query, weakTitle, history, matcher) > 0);
        Assert.IsTrue(MainListPage.ScoreTopLevelItem(query, description, history, matcher) > 0);
    }

    [TestMethod]
    public async Task CatalogChange_PreservesPublishedResultsWhileRescoring()
    {
        var settings = new SettingsModel();
        var settingsService = new Mock<ISettingsService>();
        settingsService.SetupGet(service => service.Settings).Returns(() => settings);
        settingsService.Setup(service => service.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), It.IsAny<bool>()))
            .Callback<Func<SettingsModel, SettingsModel>, bool>((update, _) => settings = update(settings));
        using var services = new ServiceCollection()
            .AddSingleton(TaskScheduler.Default)
            .AddSingleton(settingsService.Object)
            .BuildServiceProvider();
        var wrapper = new CommandProviderWrapper(new AppsProvider(), TaskScheduler.Default);
        var extensionService = new Mock<IExtensionService>();
        extensionService.Setup(service => service.LoadProvidersAsync(It.IsAny<CancellationToken>())).ReturnsAsync([wrapper]);
        using var manager = new TopLevelCommandManager(services, [extensionService.Object]);
        await manager.LoadExternalProvidersAsync();

        var original = CreateApp("Editor One");
        var snapshot = new AppListItemSnapshot([original], []);
        var source = new Mock<IAppListItemSource>();
        source.Setup(service => service.GetSnapshot()).Returns(() => snapshot);
        source.SetupGet(service => service.TopLevelResultLimit).Returns(10);
        var stateService = new Mock<IAppStateService>();
        stateService.SetupGet(service => service.State).Returns(new AppStateModel());
        var matcher = new PrecomputedFuzzyMatcher(new PrecomputedFuzzyMatcherOptions());
        var matcherProvider = new Mock<IFuzzyMatcherProvider>();
        var pauseScoring = 0;
        var scoringStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseScoring = new ManualResetEventSlim();
        matcherProvider.SetupGet(provider => provider.Current).Returns(() =>
        {
            if (Interlocked.Exchange(ref pauseScoring, 0) == 1)
            {
                scoringStarted.TrySetResult();
                Assert.IsTrue(releaseScoring.Wait(TimeSpan.FromSeconds(5)), "Scoring was not released.");
            }

            return matcher;
        });
        using var page = new MainListPage(manager, new AliasManager(manager, settingsService.Object), matcherProvider.Object, settingsService.Object, stateService.Object, source.Object);
        page.SearchText = "Editor";
        Assert.AreSame(original, page.GetItems().OfType<AppListItem>().Single());

        var added = CreateApp("Editor Two");
        snapshot = new AppListItemSnapshot([original, added], []);
        Interlocked.Exchange(ref pauseScoring, 1);
        source.Raise(service => service.Changed += null, source.Object, EventArgs.Empty);
        try
        {
            await scoringStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.AreSame(original, page.GetItems().OfType<AppListItem>().Single());
        }
        finally
        {
            releaseScoring.Set();
        }

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (page.GetItems().OfType<AppListItem>().Count() != 2 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        CollectionAssert.AreEquivalent(new[] { original, added }, page.GetItems().OfType<AppListItem>().ToArray());
    }

    private static AppListItem CreateApp(string name) => new(
        new AppItem
        {
            Name = name,
            AppIdentifier = name,
            ExePath = $@"C:\Tools\{name}.exe",
        },
        useThumbnails: false);

    private static async Task WithSearchPages(AppListItemSnapshot snapshot, bool pinFirstApp, Func<MainListPage, AllAppsPage, TopLevelCommandManager, Task> check, string? pinnedCommandId = null)
    {
        var settings = new SettingsModel();
        if (pinFirstApp)
        {
            settings = settings.TryPinCommand(AllAppsCommandProvider.WellKnownId, pinnedCommandId ?? snapshot.VisibleItems[0].Command!.Id);
        }

        var settingsService = new Mock<ISettingsService>();
        settingsService.SetupGet(service => service.Settings).Returns(() => settings);
        settingsService.Setup(service => service.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), It.IsAny<bool>()))
            .Callback<Func<SettingsModel, SettingsModel>, bool>((update, _) => settings = update(settings));
        using var services = new ServiceCollection()
            .AddSingleton(TaskScheduler.Default)
            .AddSingleton(settingsService.Object)
            .BuildServiceProvider();
        var source = new Mock<IAppListItemSource>();
        source.Setup(service => service.GetSnapshot()).Returns(snapshot);
        source.SetupGet(service => service.TopLevelResultLimit).Returns(10);
        var matcherProvider = new FuzzyMatcherProvider(new());
        using var allApps = new AllAppsPage(source.Object, matcherProvider);
        var wrapper = new CommandProviderWrapper(new AppsProvider(snapshot, allApps), TaskScheduler.Default);
        var extensionService = new Mock<IExtensionService>();
        extensionService.Setup(service => service.LoadProvidersAsync(It.IsAny<CancellationToken>())).ReturnsAsync([wrapper]);
        using var manager = new TopLevelCommandManager(services, [extensionService.Object]);
        await manager.LoadExternalProvidersAsync();
        if (pinnedCommandId is not null)
        {
            var resolved = wrapper.ResolveCommandItem(pinnedCommandId, services);
            Assert.IsNotNull(resolved);
            Assert.AreEqual(pinnedCommandId, resolved.Id);
            resolved.Cleanup();
        }

        var stateService = new Mock<IAppStateService>();
        stateService.SetupGet(service => service.State).Returns(new AppStateModel());
        using var home = new MainListPage(manager, new AliasManager(manager, settingsService.Object), matcherProvider, settingsService.Object, stateService.Object, source.Object);

        await check(home, allApps, manager);
    }

    private static async Task WaitForConditionAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private sealed partial class AppsProvider : CommandProvider
    {
        private readonly AppListItemSnapshot? _snapshot;
        private readonly AllAppsPage? _page;

        public AppsProvider(AppListItemSnapshot? snapshot = null, AllAppsPage? page = null)
        {
            _snapshot = snapshot;
            _page = page;
            Id = AllAppsCommandProvider.WellKnownId;
            DisplayName = "Apps";
        }

        public override ICommandItem[] TopLevelCommands() => _page is null ? [] : [new CommandItem(_page) { Title = "Browse apps" }];

        public override ICommandItem? GetCommandItem(string id) => _snapshot?.GetCommandItem(id);
    }
}
