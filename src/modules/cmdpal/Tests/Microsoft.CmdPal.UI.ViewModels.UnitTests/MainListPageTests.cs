// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
    private static readonly string[] DisabledAppsQueries = ["wt", "WT.EXE", "Terminal"];

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
                Subtitle = "DescriptionNeedle",
                AppTypeLabel = "Application",
                LaunchTarget = @"C:\Tools\LaunchNeedle.lnk",
                ResolvedTarget = @"C:\ResolvedTargetNeedle\Editor.exe",
                AppUserModelId = "Contoso.PackagedIdentityNeedle!Editor",
                PackageFamilyName = "Contoso.FamilyNeedle_publisher",
                MatchTerms = ["LegacyAliasNeedle", @"C:\Aliases\wt.exe", "vv"],
            },
            useThumbnails: false);
        var legacyCommandId = app.Command!.Id;
        app.App.CatalogId = "packaged:Contoso.PackagedIdentityNeedle!Editor";
        app = new AppListItem(app.App, useThumbnails: false);
        Assert.AreNotEqual(legacyCommandId, app.Command!.Id);
        app.Subtitle = string.Empty;
        var commandId = useSavedAlias ? legacyCommandId : app.Command!.Id;
        app.App.CommandIds = [legacyCommandId];
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
                LaunchTarget = @"C:\Start Menu\Command Prompt.lnk",
                ResolvedTarget = @"C:\Windows\System32\cmd.exe",
            },
            useThumbnails: false);
        var developerPrompt = new AppListItem(
            new AppItem
            {
                Name = "Developer Command Prompt for VS",
                LaunchTarget = @"C:\Start Menu\Developer Command Prompt.lnk",
                ResolvedTarget = @"C:\Windows\System32\cmd.exe",
                LaunchArguments = "/k setup.bat",
            },
            useThumbnails: false);
        var perlPrompt = new AppListItem(
            new AppItem
            {
                Name = "Perl (command line)",
                LaunchTarget = @"C:\Start Menu\Perl.lnk",
                ResolvedTarget = @"C:\Windows\SysWOW64\cmd.exe",
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
    [DataRow("cmd", false, false)]
    [DataRow("CMD.EXE", true, false)]
    [DataRow("cmd", false, true)]
    [DataRow("CMD.EXE", true, true)]
    public async Task Search_ExecutableModePublicationReranksActiveHomeQuery(string query, bool filenameMatches, bool pinned)
    {
        var apps = new[]
        {
            new AppItem
            {
                Name = "Command Prompt",
                LaunchTarget = @"C:\Start Menu\Command Prompt.lnk",
                ResolvedTarget = @"C:\Windows\System32\cmd.exe",
            },
            new AppItem
            {
                Name = "Developer Command Prompt",
                LaunchTarget = @"C:\Start Menu\Developer Command Prompt.lnk",
                ResolvedTarget = @"C:\Windows\System32\cmd.exe",
                LaunchArguments = "/k setup.bat",
            },
            new AppItem { Name = "cmd.exe helper", LaunchTarget = @"C:\Tools\Helper.exe" },
        };
        var rows = apps.Select(app => new AppListItem(app, useThumbnails: false)).ToArray();
        var snapshot = new AppListItemSnapshot(rows, []);
        var commandPromptId = snapshot.VisibleItems[0].Command!.Id;
        var helperId = snapshot.VisibleItems[2].Command!.Id;
        var originalIds = snapshot.VisibleItems.Select(item => item.Command!.Id).ToArray();
        var source = new Mock<IAppListItemSource>();
        source.Setup(service => service.GetSnapshot()).Returns(() => snapshot);
        source.SetupGet(service => service.TopLevelResultLimit).Returns(10);
        await WithSearchPages(snapshot, pinned, appListItemSource: source, check: async (home, allApps, _) =>
        {
            home.SearchText = allApps.SearchText = query;
            Assert.AreEqual(commandPromptId, home.GetItems().First(item => item is not Separator).Command!.Id);

            foreach (var (mode, expectedFirstId) in new[]
            {
                (ExecutableNameMatchMode.FilenameOnly, filenameMatches ? commandPromptId : helperId),
                (ExecutableNameMatchMode.Disabled, helperId),
                (ExecutableNameMatchMode.FilenameAndStem, commandPromptId),
            })
            {
                snapshot = new AppListItemSnapshot(rows, [], executableNameMatchMode: mode);
                source.Raise(service => service.Changed += null, source.Object, EventArgs.Empty);
                await WaitForConditionAsync(() => home.GetItems().First(item => item is not Separator).Command!.Id == expectedFirstId);

                Assert.AreEqual(query, home.SearchText);
                Assert.AreEqual(expectedFirstId, home.GetItems().First(item => item is not Separator).Command!.Id, mode.ToString());
                Assert.AreEqual(expectedFirstId, allApps.GetItems().OfType<AppListItem>().First().Command!.Id, mode.ToString());
                CollectionAssert.AreEquivalent(originalIds, home.GetItems().Where(item => originalIds.Contains(item.Command?.Id)).Select(item => item.Command!.Id).ToArray());
                CollectionAssert.AreEqual(originalIds, snapshot.VisibleItems.Select(item => item.Command!.Id).ToArray());
                Assert.AreSame(rows, snapshot.VisibleItems);
            }

            source.Verify(service => service.RefreshAsync(), Times.Never);
        });
    }

    [TestMethod]
    [DataRow("wt", false)]
    [DataRow("wt", true)]
    [DataRow("WT.EXE", false)]
    [DataRow("WT.EXE", true)]
    public async Task Search_ExecutionAliasOwnershipPublicationReranksUnchangedHomeQuery(string query, bool pinned)
    {
        var stable = new AppListItem(
            new AppItem { Name = "Terminal", AppUserModelId = "Terminal_123!App", IsPackaged = true, MatchTerms = ["wt.exe"] },
            useThumbnails: false);
        var preview = new AppListItem(
            new AppItem { Name = "Terminal Preview", AppUserModelId = "TerminalPreview_123!App", IsPackaged = true, MatchTerms = ["wt.exe"] },
            useThumbnails: false);
        AppListItem[] rows = [stable, preview];
        AppListItem[] hiddenRows = [];
        AppListItem[] patternHiddenRows = [];
        var owners = ImmutableDictionary.Create<string, string>(StringComparer.OrdinalIgnoreCase)
            .Add("wt.exe", stable.App.AppUserModelId);
        var initialSnapshot = new AppListItemSnapshot(
            rows,
            hiddenRows,
            patternHiddenRows,
            executableNameMatchMode: ExecutableNameMatchMode.Disabled,
            executionAliasOwners: owners);
        var snapshot = initialSnapshot;
        var originalIds = rows.Select(item => item.Command!.Id).ToArray();
        var source = new Mock<IAppListItemSource>();
        source.Setup(service => service.GetSnapshot()).Returns(() => snapshot);
        source.SetupGet(service => service.TopLevelResultLimit).Returns(10);
        await WithSearchPages(snapshot, pinned, appListItemSource: source, check: async (home, _, _) =>
        {
            home.SearchText = query;
            Assert.AreEqual(stable.Command!.Id, home.GetItems().First(item => item is not Separator).Command!.Id);
            source.Verify(service => service.RequestExecutionAliasRefresh(), Times.AtLeastOnce);

            foreach (var activeOwner in new[] { preview, stable })
            {
                var previousSnapshot = snapshot;
                owners = owners.SetItem("wt.exe", activeOwner.App.AppUserModelId);
                snapshot = new AppListItemSnapshot(
                    rows,
                    hiddenRows,
                    patternHiddenRows,
                    executableNameMatchMode: ExecutableNameMatchMode.Disabled,
                    executionAliasOwners: owners);
                Assert.AreSame(previousSnapshot.VisibleItems, snapshot.VisibleItems);
                Assert.AreSame(previousSnapshot.HiddenItems, snapshot.HiddenItems);
                Assert.AreSame(previousSnapshot.PatternHiddenItems, snapshot.PatternHiddenItems);
                Assert.AreNotSame(previousSnapshot.ExecutionAliasOwners, snapshot.ExecutionAliasOwners);
                source.Raise(service => service.Changed += null, source.Object, EventArgs.Empty);
                await WaitForConditionAsync(() => home.GetItems().FirstOrDefault(item => item is not Separator)?.Command?.Id == activeOwner.Command!.Id);

                Assert.AreEqual(query, home.SearchText);
                CollectionAssert.AreEquivalent(originalIds, home.GetItems().Where(item => originalIds.Contains(item.Command?.Id)).Select(item => item.Command!.Id).ToArray());
                Assert.AreSame(rows, snapshot.VisibleItems);
                CollectionAssert.AreEqual(rows, snapshot.VisibleItems.ToArray());
            }

            Assert.AreEqual(stable.App.AppUserModelId, initialSnapshot.ExecutionAliasOwners["wt.exe"], "Published ownership maps remain immutable.");
            source.Verify(service => service.RefreshAsync(), Times.Never);
        });
    }

    [TestMethod]
    public async Task Search_AppsDisabledDoesNotRequestExecutionAliasRefresh()
    {
        var terminal = new AppListItem(
            new AppItem { Name = "Terminal", AppUserModelId = "Terminal_123!App", IsPackaged = true, MatchTerms = ["wt.exe"] },
            useThumbnails: false);
        AppListItem[] rows = [terminal];
        var owners = ImmutableDictionary.Create<string, string>(StringComparer.OrdinalIgnoreCase)
            .Add("wt.exe", terminal.App.AppUserModelId);
        var snapshot = new AppListItemSnapshot(rows, [], executionAliasOwners: owners);
        var source = new Mock<IAppListItemSource>();
        source.Setup(service => service.GetSnapshot()).Returns(() => snapshot);
        source.SetupGet(service => service.TopLevelResultLimit).Returns(10);
        await WithSearchPages(snapshot, false, appListItemSource: source, appsEnabled: false, check: (home, _, manager) =>
        {
            Assert.IsFalse(manager.IsProviderActive(AllAppsCommandProvider.WellKnownId));
            foreach (var query in DisabledAppsQueries)
            {
                home.SearchText = query;
                Assert.IsFalse(home.GetItems().Any(item => item.Command?.Id == terminal.Command!.Id));
            }

            owners = owners.SetItem("wt.exe", "TerminalPreview_123!App");
            snapshot = new AppListItemSnapshot(rows, [], executionAliasOwners: owners);
            source.Raise(service => service.Changed += null, source.Object, EventArgs.Empty);

            Assert.AreEqual("Terminal", home.SearchText);
            Assert.IsFalse(home.GetItems().Any(item => item.Command?.Id == terminal.Command!.Id));
            source.Verify(service => service.RequestExecutionAliasRefresh(), Times.Never);
            source.Verify(service => service.RefreshAsync(), Times.Never);
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
            new AppItem { Name = "Command Prompt", LaunchTarget = @"C:\Windows\System32\cmd.exe" },
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
                LaunchTarget = shortcutPath,
                ResolvedTarget = targetPath,
                DirectoryPath = programs,
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
            new AppItem { Name = "Editor", MatchTerms = ["xxneedle", @"C:\Aliases\wt.exe"] },
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
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task HiddenPin_RemainsOnHomeAndSearchableUntilExplicitlyUnpinned(bool patternHidden, bool useSavedAlias)
    {
        var hidden = new AppListItem(new AppItem { Name = "Hidden Editor", MatchTerms = ["Needle"] }, useThumbnails: false);
        var id = useSavedAlias ? "Old editor_42" : hidden.Command!.Id;
        hidden.App.CommandIds = [id];
        var snapshot = new AppListItemSnapshot([CreateApp("Other")], patternHidden ? [] : [hidden], patternHidden ? [hidden] : []);
        await WithSearchPages(snapshot, true, pinnedCommandId: id, check: async (home, allApps, manager) =>
        {
            Assert.IsTrue(home.GetItems().Any(item => item.Command?.Id == id));
            home.SearchText = allApps.SearchText = "Needle";
            var pin = home.GetItems().OfType<TopLevelViewModel>().Single(item => item.Id == id);
            Assert.AreEqual(hidden.Title, pin.Title);
            Assert.IsTrue(pin.CommandViewModel.IsInvokableCommand);
            Assert.IsFalse(allApps.GetItems().Any(item => item is AppListItem));

            manager.Receive(new UnpinCommandItemMessage(AllAppsCommandProvider.WellKnownId, id));
            await WaitForConditionAsync(() => home.GetItems().All(item => item.Command?.Id != id));
            home.SearchText = string.Empty;
            Assert.IsFalse(home.GetItems().Any(item => item.Command?.Id == id));
            Assert.IsNull(snapshot.GetVisibleApp(id));
        });
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task PinAndUnpin_PreserveMetadataSearchWithoutDuplicates(bool useSavedAlias)
    {
        var app = new AppListItem(
            new AppItem { Name = "Editor", MatchTerms = ["Needle"] },
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
    [DataRow(false, false, false)]
    [DataRow(true, false, false)]
    [DataRow(true, true, false)]
    [DataRow(true, false, true)]
    public async Task Search_RetainsAndCombinesLegacyAppHistoryForVisibleAppsAndHiddenPins(bool pinned, bool manuallyHidden, bool patternHidden)
    {
        var habitual = CreateApp("Old Editor");
        var legacyId = habitual.Command!.Id;
        habitual.App.Name = "Editor Z";
        habitual.App.CatalogId = @"win32:C:\Tools\Old Editor.exe|args:";
        habitual = new AppListItem(habitual.App, useThumbnails: false);
        var primaryId = habitual.Command!.Id;
        var other = CreateApp("Editor A");
        var snapshot = new AppListItemSnapshot(
            manuallyHidden || patternHidden ? [other] : [habitual, other],
            manuallyHidden ? [habitual] : [],
            patternHidden ? [habitual] : [],
            commandAliases: new Dictionary<string, string> { [legacyId] = primaryId });
        var now = DateTimeOffset.UtcNow.AddHours(-1);
        var history = new RecentCommandsManager().WithHistoryItem(primaryId, now);
        for (var i = 0; i < 3; i++)
        {
            history = history.WithHistoryItem(legacyId, now).WithHistoryItem(other.Command!.Id, now);
        }

        var state = new AppStateModel { RecentCommands = history };
        var stateService = new Mock<IAppStateService>();
        stateService.SetupGet(service => service.State).Returns(() => state);
        stateService.Setup(service => service.UpdateState(It.IsAny<Func<AppStateModel, AppStateModel>>()))
            .Callback<Func<AppStateModel, AppStateModel>>(update => state = update(state));
        await WithSearchPages(snapshot, pinned, pinnedCommandId: pinned ? legacyId : null, appStateService: stateService, check: async (home, _, _) =>
        {
            var projected = home.GetAppHistory(history, snapshot);
            Assert.AreEqual(4, projected.History.Single(item => item.CommandId == primaryId).Uses);
            home.SearchText = "Editor";
            await WaitForConditionAsync(() => home.GetItems().Count(item => item is not Separator) == 2);
            var first = home.GetItems().First(item => item is not Separator);
            Assert.AreEqual("Editor Z", first.Title, "Three legacy uses and one current use must outrank three uses of another app.");
            Assert.AreSame(projected, home.GetAppHistory(history, snapshot), "Keystrokes must reuse the merged history view.");
            Assert.AreNotSame(projected, home.GetAppHistory(history.WithHistoryItem(other.Command!.Id, now), snapshot));
            Assert.AreNotSame(projected, home.GetAppHistory(history, new AppListItemSnapshot([habitual, other], [], commandAliases: new Dictionary<string, string> { [legacyId] = primaryId })));
            home.UpdateHistory(first);
            var entry = state.RecentCommands.History.Single(item => item.CommandId == primaryId);
            Assert.AreEqual(2, entry.Uses, "Selecting the app or its legacy pin must record new uses under the canonical ID.");
            Assert.IsTrue(entry.LastUsed > now);
            Assert.AreSame(history.History.Single(item => item.CommandId == legacyId), state.RecentCommands.History.Single(item => item.CommandId == legacyId), "Recording a use must leave stored legacy history untouched.");
            Assert.AreEqual(5, home.GetAppHistory(state.RecentCommands, snapshot).History.Single(item => item.CommandId == primaryId).Uses);
        });
    }

    [TestMethod]
    public async Task History_KeepsIdenticallyNamedPackagedAppsSeparate()
    {
        var apps = Enumerable.Range(1, 2).Select(index =>
        {
            var aumid = $"Contoso.App{index}!app";
            var app = new AppItem { Name = "Editor", Subtitle = "Text editor", AppUserModelId = aumid, IsPackaged = true };
            app.CommandIds = [new AppListItem(app, useThumbnails: false).Command!.Id];
            app.CatalogId = $"packaged:{aumid}";
            return new AppListItem(app, useThumbnails: false);
        }).ToArray();
        var legacyId = apps[0].App.CommandIds.Single();
        Assert.AreEqual(legacyId, apps[1].App.CommandIds.Single());
        var snapshot = new AppListItemSnapshot(apps, [], commandAliases: new Dictionary<string, string> { [legacyId] = string.Empty });
        var state = new AppStateModel();
        var stateService = new Mock<IAppStateService>();
        stateService.SetupGet(service => service.State).Returns(() => state);
        stateService.Setup(service => service.UpdateState(It.IsAny<Func<AppStateModel, AppStateModel>>()))
            .Callback<Func<AppStateModel, AppStateModel>>(update => state = update(state));
        await WithSearchPages(snapshot, false, appStateService: stateService, check: (home, _, _) =>
        {
            home.UpdateHistory(apps[0]);
            home.UpdateHistory(apps[1]);
            foreach (var app in apps)
            {
                Assert.AreEqual(1, state.RecentCommands.History.Single(item => item.CommandId == app.Command!.Id).Uses);
            }

            Assert.IsFalse(state.RecentCommands.History.Any(item => item.CommandId == legacyId), "Ambiguous legacy IDs must never merge unrelated applications.");
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    public void AppScores_KeepTitleTiersHistoryAndDescriptionAdmission()
    {
        var exact = CreateApp("Needle");
        var metadata = new AppListItem(
            new AppItem { Name = "Editor", MatchTerms = ["Needle"] },
            useThumbnails: false);
        var weakTitle = CreateApp("NxxOxxTxxE");
        var description = new AppListItem(
            new AppItem { Name = "Editor", Subtitle = "NxxOxxTxxE" },
            useThumbnails: false) { Subtitle = string.Empty };
        var history = new RecentCommandsManager();
        for (var i = 0; i < 100; i++)
        {
            history = history.WithHistoryItem(metadata.Command!.Id);
        }

        var matcher = new PrecomputedFuzzyMatcher();
        var query = matcher.PrecomputeQuery("Needle");
        var appSearch = new AppSearch(query.Original, matcher, ExecutableNameMatchMode.FilenameAndStem);
        var exactScore = MainListPage.ScoreTopLevelItem(query, exact, history, matcher, appSearch);
        var metadataScore = MainListPage.ScoreTopLevelItem(query, metadata, history, matcher, appSearch);
        Assert.AreEqual(RankTier.ExactTitle, MainListRanker.TierOf(exactScore));
        Assert.AreEqual(RankTier.Fuzzy, MainListRanker.TierOf(metadataScore));
        Assert.IsTrue(exactScore > metadataScore);
        query = matcher.PrecomputeQuery("zebra");
        appSearch = new AppSearch(query.Original, matcher, ExecutableNameMatchMode.FilenameAndStem);
        Assert.AreEqual(0, MainListPage.ScoreTopLevelItem(query, metadata, history, matcher, appSearch));

        query = matcher.PrecomputeQuery("note");
        appSearch = new AppSearch(query.Original, matcher, ExecutableNameMatchMode.FilenameAndStem);
        Assert.IsTrue(appSearch.Evaluate(weakTitle).HasMatch);
        Assert.IsTrue(MainListPage.ScoreTopLevelItem(query, weakTitle, history, matcher, appSearch) > 0);
        Assert.IsTrue(MainListPage.ScoreTopLevelItem(query, description, history, matcher, appSearch) > 0);
    }

    [TestMethod]
    [DataRow("cmd", ExecutableNameMatchMode.FilenameAndStem, RankTier.ExactMetadata)]
    [DataRow("cmd", ExecutableNameMatchMode.FilenameOnly, RankTier.Fuzzy)]
    [DataRow("cmd", ExecutableNameMatchMode.Disabled, RankTier.Fuzzy)]
    [DataRow("cmd.exe", ExecutableNameMatchMode.FilenameAndStem, RankTier.ExactMetadata)]
    [DataRow("cmd.exe", ExecutableNameMatchMode.FilenameOnly, RankTier.ExactMetadata)]
    [DataRow("cmd.exe", ExecutableNameMatchMode.Disabled, RankTier.Fuzzy)]
    public void AppScores_PreparedSearchControlsExecutableMode(string queryText, ExecutableNameMatchMode mode, RankTier expectedTier)
    {
        var app = new AppListItem(
            new AppItem
            {
                Name = "Shell",
                LaunchTarget = @"C:\Start Menu\Shell.lnk",
                ResolvedTarget = @"C:\Windows\System32\cmd.exe",
            },
            useThumbnails: false);
        var matcher = new PrecomputedFuzzyMatcher();
        var query = matcher.PrecomputeQuery(queryText);
        var appSearch = new AppSearch(queryText, matcher, mode);

        var score = MainListPage.ScoreTopLevelItem(query, app, new RecentCommandsManager(), matcher, appSearch);

        Assert.AreEqual(expectedTier, MainListRanker.TierOf(score));
        Assert.IsTrue(score > 0, "Executable metadata remains searchable when priority is disabled.");
    }

    [TestMethod]
    [DataRow("wt", ExecutableNameMatchMode.FilenameAndStem, RankTier.ExactMetadata)]
    [DataRow("wt", ExecutableNameMatchMode.FilenameOnly, RankTier.Fuzzy)]
    [DataRow("wt", ExecutableNameMatchMode.Disabled, RankTier.Fuzzy)]
    [DataRow("wt.exe", ExecutableNameMatchMode.FilenameAndStem, RankTier.ExactMetadata)]
    [DataRow("wt.exe", ExecutableNameMatchMode.FilenameOnly, RankTier.ExactMetadata)]
    [DataRow("WT.EXE", ExecutableNameMatchMode.Disabled, RankTier.Fuzzy)]
    public void AppScores_ActiveExecutionAliasBeatsOtherClaimantAndExecutableDespiteHistory(
        string queryText,
        ExecutableNameMatchMode mode,
        RankTier executableTier)
    {
        var stable = new AppListItem(
            new AppItem { Name = "Terminal", AppUserModelId = "Terminal_123!App", IsPackaged = true, MatchTerms = ["wt.exe"] },
            useThumbnails: false);
        var preview = new AppListItem(
            new AppItem { Name = "Terminal Preview", AppUserModelId = "TerminalPreview_123!App", IsPackaged = true, MatchTerms = ["wt.exe"] },
            useThumbnails: false);
        var executable = new AppListItem(
            new AppItem { Name = "Standalone Console", LaunchTarget = @"C:\Tools\wt.exe", ResolvedTarget = @"C:\Tools\wt.exe" },
            useThumbnails: false);
        var exactTitle = CreateApp(queryText);
        var history = new RecentCommandsManager();
        for (var i = 0; i < 100; i++)
        {
            history = history.WithHistoryItem(stable.Command!.Id).WithHistoryItem(executable.Command!.Id);
        }

        var matcher = new PrecomputedFuzzyMatcher();
        var query = matcher.PrecomputeQuery(queryText);
        var search = new AppSearch(queryText, matcher, mode, preview.App.AppUserModelId);
        var stableScore = MainListPage.ScoreTopLevelItem(query, stable, history, matcher, search);
        var previewScore = MainListPage.ScoreTopLevelItem(query, preview, history, matcher, search);
        var executableScore = MainListPage.ScoreTopLevelItem(query, executable, history, matcher, search);
        var exactTitleScore = MainListPage.ScoreTopLevelItem(query, exactTitle, history, matcher, search);

        Assert.IsTrue(stableScore > 0, "Inactive claimants remain searchable.");
        Assert.AreEqual(RankTier.PreferredExecutionAlias, MainListRanker.TierOf(previewScore));
        Assert.AreEqual(executableTier, MainListRanker.TierOf(executableScore));
        Assert.IsTrue(previewScore > stableScore, "The Windows-selected owner must beat prior use of the other claimant.");
        Assert.IsTrue(previewScore > executableScore, "The Windows-selected owner must beat prior use of an unrelated executable with the same name.");
        Assert.AreEqual(RankTier.ExactTitle, MainListRanker.TierOf(exactTitleScore));
        Assert.IsTrue(exactTitleScore > previewScore, "An exact title remains stronger than the Windows-selected alias owner.");
    }

    [TestMethod]
    [DataRow("python", ExecutableNameMatchMode.FilenameAndStem)]
    [DataRow("python.exe", ExecutableNameMatchMode.FilenameAndStem)]
    [DataRow("PYTHON.EXE", ExecutableNameMatchMode.FilenameOnly)]
    public void AppScores_ActiveStoreAliasBeatsExactPythonExecutableDespiteHistory(string queryText, ExecutableNameMatchMode mode)
    {
        var storePython = new AppListItem(
            new AppItem { Name = "Store Python", AppUserModelId = "StorePython_123!App", IsPackaged = true, MatchTerms = ["python.exe"] },
            useThumbnails: false);
        var pythonOrg = new AppListItem(
            new AppItem { Name = "Python.org", LaunchTarget = @"C:\Python\python.exe", ResolvedTarget = @"C:\Python\python.exe" },
            useThumbnails: false);
        var history = new RecentCommandsManager();
        for (var i = 0; i < 100; i++)
        {
            history = history.WithHistoryItem(pythonOrg.Command!.Id);
        }

        var matcher = new PrecomputedFuzzyMatcher();
        var query = matcher.PrecomputeQuery(queryText);
        var search = new AppSearch(queryText, matcher, mode, storePython.App.AppUserModelId);
        Assert.IsTrue(search.Evaluate(pythonOrg).IsExactExecutableMatch, "The competing executable must exercise ordinary exact-executable priority.");
        Assert.IsFalse(search.Evaluate(pythonOrg).IsPreferredExecutionAliasMatch);
        Assert.IsTrue(search.Evaluate(storePython).IsPreferredExecutionAliasMatch);

        var pythonOrgScore = MainListPage.ScoreTopLevelItem(query, pythonOrg, history, matcher, search);
        var storePythonScore = MainListPage.ScoreTopLevelItem(query, storePython, history, matcher, search);

        Assert.AreEqual(RankTier.ExactMetadata, MainListRanker.TierOf(pythonOrgScore));
        Assert.AreEqual(RankTier.PreferredExecutionAlias, MainListRanker.TierOf(storePythonScore));
        Assert.IsTrue(storePythonScore > pythonOrgScore, "The active Store alias owner must beat the unrelated python.org executable despite its usage history.");
    }

    [TestMethod]
    public void AppScores_NonemptyQueryRequiresPreparedSearch()
    {
        var app = CreateApp("Editor");
        var matcher = new PrecomputedFuzzyMatcher();
        var query = matcher.PrecomputeQuery("Editor");

        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => MainListPage.ScoreTopLevelItem(query, app, new RecentCommandsManager(), matcher, null));

        Assert.AreEqual("appSearch", exception.ParamName);
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

    private static AppListItem CreateApp(string name)
    {
        return new(
            new AppItem
            {
                Name = name,
                LaunchTarget = $@"C:\Tools\{name}.exe",
            },
            useThumbnails: false);
    }

    private static async Task WithSearchPages(AppListItemSnapshot snapshot, bool pinFirstApp, Func<MainListPage, AllAppsPage, TopLevelCommandManager, Task> check, string? pinnedCommandId = null, Mock<IAppStateService>? appStateService = null, Mock<IAppListItemSource>? appListItemSource = null, bool appsEnabled = true)
    {
        var settings = new SettingsModel();
        if (!appsEnabled)
        {
            settings = settings with
            {
                ProviderSettings = settings.ProviderSettings.SetItem(AllAppsCommandProvider.WellKnownId, new ProviderSettings(false)),
            };
        }

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
        var source = appListItemSource ?? new Mock<IAppListItemSource>();
        if (appListItemSource is null)
        {
            source.Setup(service => service.GetSnapshot()).Returns(snapshot);
            source.SetupGet(service => service.TopLevelResultLimit).Returns(10);
        }

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

        var stateService = appStateService ?? new Mock<IAppStateService>();
        if (appStateService is null)
        {
            stateService.SetupGet(service => service.State).Returns(new AppStateModel());
        }

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

        public override ICommandItem[] TopLevelCommands()
        {
            return _page is null ? [] : [new CommandItem(_page) { Title = "Browse apps" }];
        }

        public override ICommandItem? GetCommandItem(string id)
        {
            return _snapshot?.GetCommandItem(id);
        }
    }
}
