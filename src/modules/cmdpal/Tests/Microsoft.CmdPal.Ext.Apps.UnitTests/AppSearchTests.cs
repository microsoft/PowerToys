// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.Ext.Apps.AppList;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.CmdPal.Ext.Apps.Catalog.Payloads;
using Microsoft.CmdPal.Ext.Apps.Pages;
using Microsoft.CmdPal.Ext.Apps.Utils;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class AppSearchTests
{
    [TestMethod]
    [DataRow("On Level 3")]
    [DataRow("on level")]
    public void ShortcutSearch_ShowsMatchedNameWithoutChangingCanonicalRow(string query)
    {
        var item = CreateShortcutItem();
        var search = new AppSearch(query, new PrecomputedFuzzyMatcher(), ExecutableNameMatchMode.FilenameOnly);
        var result = AllAppsPage.FilterAppItems([item], AllAppsFilters.AllFilterId, search).Single();

        Assert.AreEqual("On Level 3", result.Title);
        Assert.AreEqual("Also listed as Deeps • Prints a message", result.Subtitle);
        Assert.AreEqual(item.Command!.Id, result.Command!.Id);
        Assert.AreSame(item.App, result.App);
        Assert.AreEqual("Deeps", item.Title);
        Assert.AreEqual("Prints a message", item.Subtitle);
        Assert.AreSame(result, item.GetSearchResult(search.Evaluate(item)), "Repeating a search must retain row identity.");
        var details = (Details)result.Details!;
        Assert.AreEqual(((Details)item.Details!).HeroImage?.Light?.Icon, details.HeroImage?.Light?.Icon, "Shortcut presentation must keep the app's themed icon request.");
        var path = details.Metadata.Single(element => element.Key == "Path");
        Assert.AreEqual(item.App.Shortcuts[1].Path, ((DetailsLink)path.Data!).Text);

        item.Subtitle = string.Empty;
        Assert.AreEqual("Also listed as Deeps", item.GetSearchResult(search.Evaluate(item)).Subtitle, "Hiding descriptions must keep the alternate-name explanation.");
        item.Subtitle = "Prints a message";
        Assert.AreEqual("Also listed as Deeps • Prints a message", item.GetSearchResult(search.Evaluate(item)).Subtitle);
        item.Title = "Printer";
        Assert.AreEqual("Also listed as Printer • Prints a message", item.GetSearchResult(search.Evaluate(item)).Subtitle, "The explanation must follow the preferred name.");
    }

    [TestMethod]
    public void ShortcutSearch_ReusesTargetsAndPresentationWithinAllocationBudget()
    {
        const int iterations = 1000;
        var item = CreateShortcutItem();
        var search = new AppSearch("On Level 3", new PrecomputedFuzzyMatcher(), ExecutableNameMatchMode.FilenameOnly);
        var match = search.Evaluate(item);
        var result = item.GetSearchResult(match);
        for (var i = 0; i < iterations; i++)
        {
            _ = item.GetSearchResult(search.Evaluate(item));
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < iterations; i++)
        {
            _ = item.GetSearchResult(search.Evaluate(item));
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.IsTrue(allocated <= 16 * iterations, $"Repeated shortcut matching and presentation allocated {(double)allocated / iterations:F2} bytes per query.");
        Assert.AreSame(result, item.GetSearchResult(match));
    }

    [TestMethod]
    [DataRow("Need", 1)]
    [DataRow("Needle.exe", -1)]
    [DataRow("Editor", -1)]
    public void ShortcutSearch_ScoresEachDistinctNameOnce(string query, int expectedShortcutIndex)
    {
        var item = new AppListItem(new AppItem
        {
            Name = "Editor",
            Subtitle = "Writes notes",
            MatchTerms = ["Editor", "Needle.exe", "Writes notes"],
            Shortcuts =
            [
                new("Editor", @"C:\Start Menu\Editor.lnk"),
                new("Needle.exe", @"C:\Desktop\Needle.lnk"),
                new("Needle.exe", @"C:\Desktop\Another Needle.lnk"),
            ],
        });
        var matcher = new CountingMatcher();
        var search = new AppSearch(query, matcher, ExecutableNameMatchMode.FilenameOnly);
        matcher.Counts.Clear();

        var match = search.Evaluate(item);

        Assert.AreEqual(1, matcher.Counts["Editor"]);
        Assert.AreEqual(1, matcher.Counts["Writes notes"]);
        Assert.AreEqual(1, matcher.Counts["Needle.exe"]);
        Assert.AreEqual(expectedShortcutIndex, match.ShortcutIndex ?? -1);
    }

    [TestMethod]
    public void ShortcutSearch_ProjectionSharesImmutableInputsAndDeferredIcon()
    {
        var item = CreateShortcutItem();
        var search = new AppSearch("On Level 3", new PrecomputedFuzzyMatcher(), ExecutableNameMatchMode.FilenameOnly);
        var result = item.GetSearchResult(search.Evaluate(item));

        Assert.AreSame(item.SearchTerms, result.SearchTerms);
        Assert.AreSame(item.ExecutableNames, result.ExecutableNames);
        Assert.AreSame(item.Icon, result.Icon);
        Assert.AreSame(item.App, result.App);
        Assert.AreEqual(item.Command!.Id, result.Command!.Id);
    }

    [TestMethod]
    [DataRow("ed", true)]
    [DataRow("editor", false)]
    public void ShortcutSearch_ScoreReusePreservesCasingBonusAndExactPreferredName(string query, bool promoteShortcut)
    {
        var item = new AppListItem(new AppItem
        {
            Name = "EDITOR",
            MatchTerms = ["editor"],
            Shortcuts = [new("editor", @"C:\Desktop\Editor.lnk")],
        });
        var matcher = new PrecomputedFuzzyMatcher(new() { IgnoreSameCaseBonusIfQueryIsAllLowercase = false });
        var search = new AppSearch(query, matcher, ExecutableNameMatchMode.FilenameOnly);
        var fuzzyQuery = matcher.PrecomputeQuery(query);
        var preferredScore = matcher.Score(fuzzyQuery, matcher.PrecomputeTarget(item.Title));
        var shortcutScore = matcher.Score(fuzzyQuery, matcher.PrecomputeTarget("editor"));

        Assert.IsTrue(shortcutScore > preferredScore, "The fixture must distinguish casing-sensitive scores.");
        var match = search.Evaluate(item);
        var expectedScore = promoteShortcut ? shortcutScore : preferredScore;
        Assert.AreEqual(expectedScore, match.TitleScore);
        Assert.AreEqual(expectedScore, match.MetadataScore);
        Assert.AreEqual(promoteShortcut ? 0 : -1, match.ShortcutIndex ?? -1);
    }

    [TestMethod]
    [DataRow("cmd", true)]
    [DataRow("cmd.exe", false)]
    [DataRow(@"C:\Desktop\cmd.lnk", false)]
    public void ShortcutSearch_NameOnlyTargetsRespectFilenameAndPathQueryRules(string query, bool expectedMatch)
    {
        var item = new AppListItem(new AppItem
        {
            Name = "Editor",
            Shortcuts = [new("cmd.exe", @"C:\Desktop\cmd.lnk")],
        });
        var search = new AppSearch(query, new PrecomputedFuzzyMatcher(), ExecutableNameMatchMode.FilenameOnly);

        var match = search.Evaluate(item);

        Assert.AreEqual(expectedMatch, match.HasMatch);
        Assert.AreEqual(expectedMatch, match.HasMetadataMatch);
        Assert.AreEqual(expectedMatch ? 0 : -1, match.ShortcutIndex ?? -1);
    }

    [TestMethod]
    [DataRow("Outlook.com")]
    [DataRow("Tools.bin")]
    [DataRow("Notes.ws")]
    [DataRow("Build.job")]
    [DataRow("Admin.msc")]
    public void ShortcutSearch_DottedDisplayNamesKeepShortcutPresentation(string name)
    {
        var item = new AppListItem(new AppItem
        {
            Name = "Browser",
            LaunchTarget = @"C:\Tools\browser.exe",
            MatchTerms = [name],
            Shortcuts = [new(name, $@"C:\Desktop\{name}.lnk")],
        });
        var matcher = new PrecomputedFuzzyMatcher();
        foreach (var query in new[] { name[..^1], name })
        {
            var match = new AppSearch(query, matcher, ExecutableNameMatchMode.FilenameOnly).Evaluate(item);
            var result = item.GetSearchResult(match);

            Assert.IsTrue(match.HasMatch);
            Assert.AreEqual(name, result.Title, "Completing a dotted display name must not switch to another shortcut.");
            Assert.AreEqual(item.Command!.Id, result.Command!.Id);
        }
    }

    [TestMethod]
    [DataRow(ExecutableNameMatchMode.Disabled, "tool.com", "")]
    [DataRow(ExecutableNameMatchMode.FilenameOnly, "tool.com", "")]
    [DataRow(ExecutableNameMatchMode.FilenameAndStem, "tool.com", "")]
    [DataRow(ExecutableNameMatchMode.Disabled, "tool.com", "/special")]
    [DataRow(ExecutableNameMatchMode.FilenameOnly, "tool.com", "/special")]
    [DataRow(ExecutableNameMatchMode.FilenameAndStem, "tool.com", "/special")]
    [DataRow(ExecutableNameMatchMode.Disabled, "run.bat", "/special")]
    [DataRow(ExecutableNameMatchMode.FilenameOnly, "run.bat", "/special")]
    [DataRow(ExecutableNameMatchMode.FilenameAndStem, "run.bat", "/special")]
    public void ShortcutSearch_ActualExecutableFilenameKeepsCanonicalPresentation(ExecutableNameMatchMode mode, string filename, string arguments)
    {
        var item = new AppListItem(new AppItem
        {
            Name = "Utility",
            LaunchTarget = @"C:\Desktop\Utility.lnk",
            ResolvedTarget = $@"C:\Tools\{filename}",
            LaunchArguments = arguments,
            MatchTerms = [filename],
            Shortcuts = [new(filename, $@"C:\Desktop\{filename}.lnk")],
        });
        var match = new AppSearch(filename, new PrecomputedFuzzyMatcher(), mode).Evaluate(item);

        Assert.IsTrue(match.HasMatch);
        Assert.AreEqual(mode != ExecutableNameMatchMode.Disabled && string.IsNullOrWhiteSpace(arguments), match.IsExactExecutableMatch);
        Assert.AreSame(item, item.GetSearchResult(match), "Filename intent must not depend on executable-match priority.");
    }

    [TestMethod]
    public void ShortcutSearch_EqualScoresSortByDisplayedNamesWithNumericOrdering()
    {
        var first = new AppListItem(new AppItem
        {
            Name = "Zeta",
            LaunchTarget = @"C:\Tools\Zeta.exe",
            Shortcuts = [new("Needle 02", @"C:\Desktop\Needle 02.lnk")],
        });
        var second = new AppListItem(new AppItem
        {
            Name = "Alpha",
            LaunchTarget = @"C:\Tools\Alpha.exe",
            Shortcuts = [new("Needle 10", @"C:\Desktop\Needle 10.lnk")],
        });
        var search = new AppSearch("Needle", new PrecomputedFuzzyMatcher(), ExecutableNameMatchMode.FilenameOnly);
        Assert.AreEqual(search.Evaluate(first).LexicalScore, search.Evaluate(second).LexicalScore, "The fixture must exercise the title tie-break.");

        var results = AllAppsPage.FilterAppItems([second, first], AllAppsFilters.AllFilterId, search);

        Assert.AreEqual(2, results.Length);
        Assert.AreEqual("Needle 02", results[0].Title);
        Assert.AreEqual("Needle 10", results[1].Title);
        Assert.AreEqual(first.Command!.Id, results[0].Command!.Id);
        Assert.AreEqual(second.Command!.Id, results[1].Command!.Id);
        Assert.AreEqual("Zeta", first.Title);
        Assert.AreEqual("Alpha", second.Title);
    }

    [TestMethod]
    [DataRow("Deeps")]
    [DataRow("_PrintMe.exe")]
    [DataRow("Prints a message")]
    [DataRow(@"C:\Desktop\On Level 3.lnk")]
    public void ShortcutSearch_OtherFieldsKeepCanonicalPresentation(string query)
    {
        var item = CreateShortcutItem();
        var search = new AppSearch(query, new PrecomputedFuzzyMatcher(), ExecutableNameMatchMode.FilenameOnly);

        Assert.AreSame(item, AllAppsPage.FilterAppItems([item], AllAppsFilters.AllFilterId, search).Single());
    }

    [TestMethod]
    public void ShortcutSearch_ConcurrentQueriesKeepIndependentStableRows()
    {
        var item = CreateShortcutItem();
        var matcher = new PrecomputedFuzzyMatcher();
        var first = new AppSearch("On Level 3", matcher, ExecutableNameMatchMode.FilenameOnly);
        var second = new AppSearch("Xilbaba", matcher, ExecutableNameMatchMode.FilenameOnly);
        var firstResult = item.GetSearchResult(first.Evaluate(item));
        var secondResult = item.GetSearchResult(second.Evaluate(item));

        Parallel.For(0, 100, index =>
        {
            var search = index % 2 == 0 ? first : second;
            Assert.AreSame(index % 2 == 0 ? firstResult : secondResult, item.GetSearchResult(search.Evaluate(item)));
        });
        Assert.AreEqual("On Level 3", firstResult.Title);
        Assert.AreEqual("Xilbaba", secondResult.Title);
        Assert.AreEqual("Deeps", item.Title);
    }

    [TestMethod]
    public void ShortcutSearch_PackagedLocationActionsFollowShortcutAndKeepCopyPath()
    {
        const string shortcutPath = @"C:\Desktop\Renamed Store app.lnk";
        var app = new PackagedAppPayload
        {
            Name = "Store app",
            AppUserModelId = "Contoso.App_123!app",
            PackageLocation = @"C:\Program Files\WindowsApps\Contoso.App",
            IsNonRemovable = true,
        }.ToAppItem();
        app.CatalogId = AppIdentity.ForPackaged(app.AppUserModelId);
        app.Shortcuts = [new("Renamed Store app", shortcutPath)];
        var item = new AppListItem(app);
        var locationSource = item.MoreCommands.OfType<CommandContextItem>().Single(context => context.Command is OpenFileCommand);
        var originalLocation = (OpenFileCommand)locationSource.Command!;
        originalLocation.Result = CommandResult.KeepOpen();
        var copySource = item.MoreCommands.Single(context => context is CommandContextItem { Command: CopyPathCommand });
        var search = new AppSearch("Renamed Store app", new PrecomputedFuzzyMatcher(), ExecutableNameMatchMode.FilenameOnly);

        var result = item.GetSearchResult(search.Evaluate(item));

        var location = result.MoreCommands.OfType<CommandContextItem>().Single(context => context.Command is ShowFileInFolderCommand);
        var show = (ShowFileInFolderCommand)location.Command!;
        Assert.AreEqual(originalLocation.Name, show.Name);
        Assert.AreSame(originalLocation.Result, show.Result);
        Assert.AreEqual(locationSource.RequestedShortcut, location.RequestedShortcut);
        Assert.AreSame(copySource, result.MoreCommands.Single(context => context is CommandContextItem { Command: CopyPathCommand }));
        Assert.AreEqual(item.Command!.Id, result.Command!.Id);

        // Inspect location targets without opening Explorer, a console or the clipboard during the test.
        Assert.AreEqual(shortcutPath, typeof(ShowFileInFolderCommand).GetField("_path", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(show));
        var console = result.MoreCommands.OfType<CommandContextItem>().Select(context => context.Command).OfType<OpenInConsoleCommand>().Single();
        Assert.AreEqual(Path.GetDirectoryName(shortcutPath), typeof(OpenInConsoleCommand).GetField("_path", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(console));
        Assert.AreEqual(true, typeof(OpenInConsoleCommand).GetField("_isDirectory", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(console));
        var details = (Details)result.Details!;
        Assert.AreEqual(shortcutPath, ((DetailsLink)details.Metadata.Single(element => element.Key == "Path").Data!).Text);
    }

    [TestMethod]
    public async Task ShortcutSearch_ConcurrentPresentationUpdatesDrainWithoutBlockingReaders()
    {
        var item = CreateShortcutItem();
        var search = new AppSearch("On Level 3", new PrecomputedFuzzyMatcher(), ExecutableNameMatchMode.FilenameOnly);
        var match = search.Evaluate(item);
        var result = item.GetSearchResult(match);
        var firstNotificationEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseNotification = new ManualResetEventSlim();
        var notificationCount = 0;
        var notificationReleased = false;
        result.PropChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(result.Subtitle) && Interlocked.Increment(ref notificationCount) == 1)
            {
                firstNotificationEntered.TrySetResult();
                notificationReleased = releaseNotification.Wait(TimeSpan.FromSeconds(10));
            }
        };

        item.Subtitle = "First description";
        var firstUpdate = Task.Run(() => item.GetSearchResult(match));
        var latestCommand = new CommandContextItem(new AnonymousCommand(() => { }));
        try
        {
            await firstNotificationEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            item.Subtitle = "Latest description";
            item.MoreCommands = [latestCommand];

            // Readers can revisit a row from a notification without waiting for its publishing thread.
            var secondResult = await Task.Run(() => item.GetSearchResult(match)).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.AreSame(result, secondResult);
            Assert.AreEqual(1, Volatile.Read(ref notificationCount), "The pending update must be drained by the current writer.");
        }
        finally
        {
            releaseNotification.Set();
            await firstUpdate.WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.IsTrue(notificationReleased, "The notification must be released by the test, rather than its timeout.");
        Assert.AreEqual("Also listed as Deeps • Latest description", result.Subtitle);
        Assert.AreSame(latestCommand, result.MoreCommands.Single());
        Assert.AreSame(result, item.GetSearchResult(match));
    }

    [TestMethod]
    public async Task ShortcutSearch_ConcurrentFirstAccessPublishesCompletePresentation()
    {
        var item = CreateShortcutItem();
        var match = new AppSearch("On Level 3", new PrecomputedFuzzyMatcher(), ExecutableNameMatchMode.FilenameOnly).Evaluate(item);
        var firstCommandReadEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseCommandRead = new ManualResetEventSlim();
        var commandReadCount = 0;
        var commandReadReleased = false;
        var show = new ShowFileInFolderCommand(item.App.LaunchTarget);
        var context = new Mock<ICommandContextItem>();
        context.SetupGet(value => value.Command).Returns(() =>
        {
            if (Interlocked.Increment(ref commandReadCount) == 1)
            {
                firstCommandReadEntered.TrySetResult();
                commandReadReleased = releaseCommandRead.Wait(TimeSpan.FromSeconds(30));
            }

            return show;
        });
        item.MoreCommands = [context.Object];

        // Hold the first creator inside command projection while another caller requests the same row.
        var firstCreation = Task.Run(() => item.GetSearchResult(match));
        AppListItem secondResult;
        try
        {
            await firstCommandReadEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            secondResult = await Task.Run(() => item.GetSearchResult(match)).WaitAsync(TimeSpan.FromSeconds(10));

            Assert.AreEqual("Also listed as Deeps • Prints a message", secondResult.Subtitle);
            var location = (ShowFileInFolderCommand)((ICommandContextItem)secondResult.MoreCommands.Single()).Command;
            Assert.AreEqual(item.App.Shortcuts[1].Path, typeof(ShowFileInFolderCommand).GetField("_path", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(location));
        }
        finally
        {
            releaseCommandRead.Set();
            await firstCreation.WaitAsync(TimeSpan.FromSeconds(10));
        }

        Assert.IsTrue(commandReadReleased, "The creator must be released by the test, rather than its timeout.");
        Assert.AreSame(secondResult, await firstCreation);
        Assert.AreSame(secondResult, item.GetSearchResult(match));
    }

    [TestMethod]
    public void ShortcutSearch_ContextActionsPreserveApplicationActionsAndFollowVisibilityUpdates()
    {
        var item = CreateShortcutItem();
        var show = new CommandContextItem(new ShowFileInFolderCommand(item.App.LaunchTarget) { Result = CommandResult.KeepOpen() })
        {
            Title = "Show shortcut",
            RequestedShortcut = KeyChords.OpenFileLocation,
        };
        var hide = new CommandContextItem(new AnonymousCommand(() => { }));
        item.MoreCommands = [show, hide];
        var search = new AppSearch("On Level 3", new PrecomputedFuzzyMatcher(), ExecutableNameMatchMode.FilenameOnly);
        var result = item.GetSearchResult(search.Evaluate(item));

        Assert.AreNotSame(show, result.MoreCommands[0], "Location actions belong to the matched shortcut.");
        Assert.AreEqual(show.Title, ((CommandContextItem)result.MoreCommands[0]).Title);
        Assert.AreEqual(show.RequestedShortcut, ((CommandContextItem)result.MoreCommands[0]).RequestedShortcut);
        Assert.AreSame(hide, result.MoreCommands[1], "Application and host actions must retain their existing identity.");
        Assert.AreSame(show, item.MoreCommands[0]);

        var notifications = 0;
        result.PropChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(result.MoreCommands) && ++notifications == 1)
            {
                item.GetSearchResult(search.Evaluate(item));
            }
        };
        var unhide = new CommandContextItem(new AnonymousCommand(() => { }));
        item.MoreCommands = [show, unhide];
        Assert.AreSame(result, item.GetSearchResult(search.Evaluate(item)));
        Assert.AreSame(unhide, result.MoreCommands[1], "Cached shortcut rows must use the latest visibility action.");
        Assert.AreEqual(1, notifications, "A listener that rereads the result must not repeat the command update.");
    }

    [TestMethod]
    [DataRow("wt", ExecutableNameMatchMode.FilenameAndStem)]
    [DataRow("wt", ExecutableNameMatchMode.FilenameOnly)]
    [DataRow("wt", ExecutableNameMatchMode.Disabled)]
    [DataRow("wt.exe", ExecutableNameMatchMode.FilenameAndStem)]
    [DataRow("wt.exe", ExecutableNameMatchMode.FilenameOnly)]
    [DataRow("WT.EXE", ExecutableNameMatchMode.Disabled)]
    public void Evaluate_CapturedExecutionAliasOwnerRanksFirst(string query, ExecutableNameMatchMode mode)
    {
        var stable = CreateExecutionAliasItem("Terminal", "Terminal_123!App");
        var preview = CreateExecutionAliasItem("Terminal Preview", "TerminalPreview_123!App");
        var owner = preview.App.AppUserModelId.ToUpperInvariant();
        var matcher = new PrecomputedFuzzyMatcher();
        var search = new AppSearch(query, matcher, mode, owner);
        for (var i = 0; i < 5; i++)
        {
            Assert.IsTrue(search.Evaluate(stable).HasMatch);
            Assert.IsTrue(search.Evaluate(preview).HasMatch);
            Assert.IsFalse(search.Evaluate(stable).IsPreferredExecutionAliasMatch);
            Assert.IsTrue(search.Evaluate(preview).IsPreferredExecutionAliasMatch);
        }

        var results = AllAppsPage.FilterAppItems([stable, preview], AllAppsFilters.AllFilterId, search);
        Assert.AreEqual(2, results.Length);
        Assert.AreSame(preview, results[0], "The active alias owner must beat ordinary title and metadata tie-breaks.");

        owner = stable.App.AppUserModelId;
        Assert.IsTrue(search.Evaluate(preview).IsPreferredExecutionAliasMatch, "One scoring pass must keep one captured owner.");
        var nextSearch = new AppSearch(query, matcher, mode, owner);
        results = AllAppsPage.FilterAppItems([stable, preview], AllAppsFilters.AllFilterId, nextSearch);
        Assert.AreSame(stable, results[0], "The next search must pick up a Windows alias preference change.");
    }

    [TestMethod]
    public void Evaluate_UnavailableExecutionAliasKeepsBothClaimantsSearchableWithoutPreference()
    {
        var stable = CreateExecutionAliasItem("Terminal", "Terminal_123!App");
        var preview = CreateExecutionAliasItem("Terminal Preview", "TerminalPreview_123!App");
        var search = new AppSearch("wt", new PrecomputedFuzzyMatcher(), ExecutableNameMatchMode.Disabled);

        Assert.IsTrue(search.Evaluate(stable).HasMatch);
        Assert.IsTrue(search.Evaluate(preview).HasMatch);
        Assert.IsFalse(search.Evaluate(stable).IsPreferredExecutionAliasMatch);
        Assert.IsFalse(search.Evaluate(preview).IsPreferredExecutionAliasMatch);
        Assert.AreEqual(2, AllAppsPage.FilterAppItems([stable, preview], AllAppsFilters.AllFilterId, search).Length);
    }

    [TestMethod]
    public void Evaluate_ExecutionAliasPreferenceRequiresExactMetadata()
    {
        var item = CreateExecutionAliasItem("Terminal", "Terminal_123!App");
        var search = new AppSearch("w", new PrecomputedFuzzyMatcher(), ExecutableNameMatchMode.FilenameAndStem, item.App.AppUserModelId);

        Assert.IsFalse(search.Evaluate(item).IsPreferredExecutionAliasMatch);
    }

    [TestMethod]
    [DataRow("wt")]
    [DataRow("wt.exe")]
    [DataRow(" WT.EXE ")]
    public void ExecutionAliasSnapshot_MatchesFilenameAndStemWithoutIo(string query)
    {
        var owners = ImmutableDictionary.Create<string, string>(StringComparer.OrdinalIgnoreCase)
            .Add("wt.exe", "Terminal_123!App");
        var snapshot = new AppListItemSnapshot([], [], executionAliasOwners: owners);

        Assert.AreEqual("Terminal_123!App", snapshot.GetExecutionAliasOwner(query));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow(@"C:\Tools\wt.exe")]
    [DataRow(@"..\wt.exe")]
    [DataRow("folder/wt.exe")]
    [DataRow("wt:exe")]
    [DataRow("wt*")]
    public void ExecutionAliasSnapshot_RejectsNonFilenameQueries(string query)
    {
        var alias = query.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? query : query + ".exe";
        var owners = ImmutableDictionary.Create<string, string>(StringComparer.OrdinalIgnoreCase).Add(alias, "Untrusted_123!App");
        var snapshot = new AppListItemSnapshot([], [], executionAliasOwners: owners);

        Assert.IsNull(snapshot.GetExecutionAliasOwner(query));
    }

    [TestMethod]
    [DataRow("LegacyAliasNeedle")]
    [DataRow("ResolvedTargetNeedle")]
    [DataRow("LaunchNeedle")]
    [DataRow("PackagedIdentityNeedle")]
    [DataRow("FamilyNeedle")]
    [DataRow("DescriptionNeedle")]
    public void Evaluate_SearchesOriginalApplicationFields(string query)
    {
        var item = new AppListItem(
            new AppItem
            {
                Name = "Editor",
                Subtitle = "DescriptionNeedle",
                LaunchTarget = @"C:\Tools\LaunchNeedle.lnk",
                ResolvedTarget = @"C:\ResolvedTargetNeedle\Editor.exe",
                AppUserModelId = "Contoso.PackagedIdentityNeedle!Editor",
                PackageFamilyName = "Contoso.FamilyNeedle_publisher",
                MatchTerms = ["LegacyAliasNeedle"],
            });
        var search = new AppSearch(query, new PrecomputedFuzzyMatcher(), ExecutableNameMatchMode.FilenameAndStem);
        var withDescription = search.Evaluate(item);
        item.Subtitle = string.Empty;

        Assert.IsTrue(withDescription.HasMatch);
        Assert.AreEqual(withDescription, search.Evaluate(item), "Display settings must not change search data.");
    }

    [TestMethod]
    [DataRow("WT", true)]
    [DataRow(@"C:\Tools\wt.exe", true)]
    [DataRow(@"C:\Tools\wt.log", false)]
    [DataRow(@"C:\Tools\Something.exe", false)]
    public void Evaluate_RecognizesExplicitShortMetadata(string term, bool expected)
    {
        var item = CreateItem(term);
        var match = new AppSearch("wt", new PrecomputedFuzzyMatcher(), ExecutableNameMatchMode.FilenameAndStem).Evaluate(item);

        Assert.AreEqual(expected, match.IsExactMetadataMatch);
    }

    [TestMethod]
    [DataRow("cmd", "", true)]
    [DataRow("cmd.exe", "", true)]
    [DataRow("CMD.ExE", " ", true)]
    [DataRow("cmd.cmd", "", false)]
    [DataRow("cm", "", false)]
    [DataRow("cmd.exe", "/k setup.bat", false)]
    [DataRow(@"C:\Windows\System32\cmd.exe", "", false)]
    public void Evaluate_PromotesOnlyExactArgumentFreeExecutableNames(string query, string arguments, bool expected)
    {
        var item = new AppListItem(
            new Win32AppPayload
            {
                Name = "Command Prompt",
                LnkFilePath = @"C:\Start Menu\Command Prompt.lnk",
                TargetPath = @"C:\Windows\System32\cmd.exe",
                Arguments = arguments,
            }.ToAppItem());
        var matcher = new PrecomputedFuzzyMatcher();
        var match = new AppSearch(query, matcher, ExecutableNameMatchMode.FilenameAndStem).Evaluate(item);

        Assert.AreEqual(arguments, item.App.LaunchArguments);
        Assert.AreEqual(expected, match.IsExactExecutableMatch);
        if (expected)
        {
            Assert.IsTrue(match.LexicalScore >= match.MetadataScore, "Exact executable names must not receive the metadata penalty.");
        }

        Assert.IsFalse(new AppSearch(query, matcher, ExecutableNameMatchMode.FilenameAndStem).Evaluate(CreateItem(query)).IsExactExecutableMatch, "Arbitrary retained metadata is not the launch target.");
    }

    [TestMethod]
    [DataRow(ExecutableNameMatchMode.FilenameAndStem, "CMD", "", true)]
    [DataRow(ExecutableNameMatchMode.FilenameOnly, "cmd", "", false)]
    [DataRow(ExecutableNameMatchMode.FilenameOnly, "cmd.exe", "", true)]
    [DataRow(ExecutableNameMatchMode.FilenameOnly, "CMD.EXE", " ", true)]
    [DataRow(ExecutableNameMatchMode.Disabled, "cmd", "", false)]
    [DataRow(ExecutableNameMatchMode.Disabled, "CMD.EXE", "", false)]
    [DataRow(ExecutableNameMatchMode.FilenameAndStem, "cmd", "/k setup.bat", false)]
    [DataRow(ExecutableNameMatchMode.FilenameOnly, "cmd.exe", "/k setup.bat", false)]
    [DataRow(ExecutableNameMatchMode.Disabled, @"C:\Windows\System32\cmd.exe", "", false)]
    [DataRow(ExecutableNameMatchMode.FilenameOnly, @"C:\Windows\System32\cmd.exe", "", false)]
    public void Evaluate_ExecutableNameModeChangesPriorityWithoutRemovingMetadataMatches(
        ExecutableNameMatchMode mode, string query, string arguments, bool expected)
    {
        var app = new Win32AppPayload
        {
            Name = "Command Prompt",
            LnkFilePath = @"C:\Start Menu\Command Prompt.lnk",
            TargetPath = @"C:\Windows\System32\cmd.exe",
            Arguments = arguments,
        }.ToAppItem();
        var item = new AppListItem(app);
        var defaultItem = new AppListItem(app);
        var matcher = new PrecomputedFuzzyMatcher();
        var search = new AppSearch(query, matcher, mode);
        var match = search.Evaluate(item);
        var defaultMatch = new AppSearch(query, matcher, ExecutableNameMatchMode.FilenameAndStem).Evaluate(defaultItem);

        Assert.AreEqual(expected, match.IsExactExecutableMatch);
        Assert.IsTrue(match.HasMetadataMatch);
        Assert.IsTrue(match.HasMatch);
        Assert.AreEqual(defaultMatch.TitleScore, match.TitleScore);
        Assert.AreEqual(defaultMatch.DescriptionScore, match.DescriptionScore);
        Assert.AreEqual(defaultMatch.MetadataScore, match.MetadataScore);
        Assert.AreEqual(defaultMatch.IsExactMetadataMatch, match.IsExactMetadataMatch);
        Assert.AreEqual(arguments, item.App.LaunchArguments);
        Assert.AreEqual(defaultItem.Command!.Id, item.Command!.Id);
    }

    [TestMethod]
    public void Evaluate_ExecutableNameMatchingStaysWithinPerAppAllocationBudget()
    {
        const int iterations = 10000;
        const int warmupIterations = 1000;
        const long extraBytesPerAppBudget = 16;
        var item = new AppListItem(
            new AppItem { Name = "Shell", LaunchTarget = @"C:\Tools\cmd.exe" });
        var matcher = new PrecomputedFuzzyMatcher();
        var enabledSearch = new AppSearch("cmd", matcher, ExecutableNameMatchMode.FilenameAndStem);
        var disabledSearch = new AppSearch("cmd", matcher, ExecutableNameMatchMode.Disabled);
        _ = item.GetSearchTargets(matcher);
        for (var i = 0; i < warmupIterations; i++)
        {
            _ = enabledSearch.Evaluate(item);
            _ = disabledSearch.Evaluate(item);
        }

        var disabledMatchCount = 0;
        var beforeDisabled = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < iterations; i++)
        {
            disabledMatchCount += disabledSearch.Evaluate(item).IsExactExecutableMatch ? 1 : 0;
        }

        var disabledAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - beforeDisabled;
        var enabledMatchCount = 0;
        var beforeEnabled = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < iterations; i++)
        {
            enabledMatchCount += enabledSearch.Evaluate(item).IsExactExecutableMatch ? 1 : 0;
        }

        var enabledAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - beforeEnabled;
        var extraAllocatedBytes = enabledAllocatedBytes - disabledAllocatedBytes;
        Assert.AreEqual(0, disabledMatchCount);
        Assert.AreEqual(iterations, enabledMatchCount);
        Assert.IsTrue(
            extraAllocatedBytes <= extraBytesPerAppBudget * iterations,
            $"Executable-name matching allocated {(double)extraAllocatedBytes / iterations:F2} extra bytes per app; the budget is {extraBytesPerAppBudget}. Enabled total: {enabledAllocatedBytes}; disabled total: {disabledAllocatedBytes}; evaluations per mode: {iterations}.");
    }

    [TestMethod]
    [DataRow("wt")]
    [DataRow("wt.exe")]
    public void Evaluate_SearchesExecutableSourcesWithoutOtherLaunchMetadata(string query)
    {
        var item = new AppListItem(
            new AppItem { Name = "Terminal", ExecutableSourcePaths = [@"C:\Aliases\wt.exe"] });
        var match = new AppSearch(query, new PrecomputedFuzzyMatcher(), ExecutableNameMatchMode.FilenameAndStem).Evaluate(item);

        Assert.IsTrue(match.IsExactExecutableMatch);
        Assert.IsTrue(match.HasMatch);
        Assert.IsTrue(match.LexicalScore > 0);
    }

    [TestMethod]
    public void Evaluate_ExecutableExtensionRequiresTheCompleteFilename()
    {
        var item = new AppListItem(
            new AppItem { Name = "Another shell", LaunchTarget = @"C:\Tools\cmd.exe.exe" });

        Assert.IsFalse(new AppSearch("cmd.exe", new PrecomputedFuzzyMatcher(), ExecutableNameMatchMode.FilenameAndStem).Evaluate(item).IsExactExecutableMatch);
    }

    [TestMethod]
    [DataRow("wt", ExecutableNameMatchMode.FilenameAndStem, true)]
    [DataRow("wt.exe", ExecutableNameMatchMode.FilenameAndStem, true)]
    [DataRow("WindowsTerminal.exe", ExecutableNameMatchMode.FilenameAndStem, true)]
    [DataRow("WT", ExecutableNameMatchMode.FilenameOnly, false)]
    [DataRow("WT.EXE", ExecutableNameMatchMode.FilenameOnly, true)]
    [DataRow("WindowsTerminal.exe", ExecutableNameMatchMode.Disabled, false)]
    public void Evaluate_RecognizesExecutionAliasAndResolvedExecutable(string query, ExecutableNameMatchMode mode, bool expected)
    {
        var item = new AppListItem(
            new AppItem
            {
                Name = "Terminal",
                LaunchTarget = @"C:\Aliases\wt.exe",
                ResolvedTarget = @"C:\WindowsApps\Microsoft.WindowsTerminal\WindowsTerminal.exe",
            });

        var match = new AppSearch(query, new PrecomputedFuzzyMatcher(), mode).Evaluate(item);

        Assert.AreEqual(expected, match.IsExactExecutableMatch);
        Assert.IsTrue(match.HasMetadataMatch, "Execution aliases and resolved targets stay searchable in every mode.");
    }

    [TestMethod]
    [DataRow(Environment.SpecialFolder.CommonPrograms, @"Acme\Editor.lnk", "start")]
    [DataRow(Environment.SpecialFolder.CommonPrograms, @"Acme\Editor.lnk", "program")]
    [DataRow(Environment.SpecialFolder.CommonPrograms, @"Acme\Editor.lnk", "windows")]
    [DataRow(Environment.SpecialFolder.CommonPrograms, @"Acme\Editor.lnk", "micro")]
    [DataRow(Environment.SpecialFolder.ProgramFiles, @"WindowsApps\Acme.Editor_1.0_x64__publisher\Editor.exe", "apps")]
    [DataRow(Environment.SpecialFolder.LocalApplicationData, @"Microsoft\WindowsApps\editor.exe", "windows")]
    public void Evaluate_SharedDirectoryNamesRequirePathSyntax(Environment.SpecialFolder folder, string relativePath, string commonWord)
    {
        var path = Path.Combine(Environment.GetFolderPath(folder), relativePath);
        var item = new AppListItem(
            new AppItem { Name = "Editor", LaunchTarget = path, DirectoryPath = Path.GetDirectoryName(path)!, MatchTerms = [path] });
        var matcher = new PrecomputedFuzzyMatcher();

        Assert.IsFalse(new AppSearch(commonWord, matcher, ExecutableNameMatchMode.FilenameAndStem).Evaluate(item).HasMetadataMatch, commonWord);
        Assert.IsTrue(new AppSearch(path, matcher, ExecutableNameMatchMode.FilenameAndStem).Evaluate(item).IsExactMetadataMatch, "Explicit paths remain searchable.");
        Assert.IsTrue(new AppSearch("editor", matcher, ExecutableNameMatchMode.FilenameAndStem).Evaluate(item).HasMatch);
    }

    [TestMethod]
    public void Evaluate_AppSpecificDirectoriesRemainSearchableWithoutPathSyntax()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "Needle", "Editor.lnk");
        var item = CreateItem(path);

        Assert.IsTrue(new AppSearch("Needle", new PrecomputedFuzzyMatcher(), ExecutableNameMatchMode.FilenameAndStem).Evaluate(item).HasMetadataMatch);
    }

    [TestMethod]
    public void SearchPaths_RespectDirectoryBoundariesAndIgnoreDiscoveryRootItself()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var path = Path.Combine(programFiles, "WindowsApps-extra", "editor.exe");

        Assert.AreEqual(@"WindowsApps-extra\editor.exe", PathHelpers.GetAppSearchPath(path));
        Assert.AreEqual(string.Empty, PathHelpers.GetAppSearchPath(Path.Combine(programFiles, "WindowsApps")));
    }

    [TestMethod]
    public void Evaluate_DuplicateAliasesDoNotBoostScores()
    {
        var search = new AppSearch("needle", new PrecomputedFuzzyMatcher(), ExecutableNameMatchMode.FilenameAndStem);
        var single = CreateItem("needle");
        var duplicates = CreateItem("needle", "NEEDLE", "needle");

        Assert.AreEqual(search.Evaluate(single), search.Evaluate(duplicates));
        Assert.AreEqual(1, duplicates.SearchTerms.Count);
    }

    [TestMethod]
    [DataRow("cafe", "Café")]
    [DataRow("jisuanqi", "计算器")]
    public void Evaluate_UsesConfiguredLinguisticMatcher(string query, string term)
    {
        var provider = new FuzzyMatcherProvider(new(), new() { Mode = PinyinMode.On });
        var match = new AppSearch(query, provider.Current, ExecutableNameMatchMode.FilenameAndStem).Evaluate(CreateItem(term));

        Assert.IsTrue(match.HasMetadataMatch);
    }

    [TestMethod]
    public void Targets_AreReusedAndRemainCoherentAcrossConcurrentSchemas()
    {
        var foldedMatcher = new PrecomputedFuzzyMatcher(new() { RemoveDiacritics = true });
        var literalMatcher = new PrecomputedFuzzyMatcher(new() { RemoveDiacritics = false });
        Assert.AreNotEqual(foldedMatcher.SchemaId, literalMatcher.SchemaId);
        var searches = new[] { new AppSearch("cafe", foldedMatcher, ExecutableNameMatchMode.FilenameAndStem), new AppSearch("cafe", literalMatcher, ExecutableNameMatchMode.FilenameAndStem) };
        var item = CreateItem("Café");
        var expected = searches.Select(search => search.Evaluate(CreateItem("Café"))).ToArray();
        Assert.IsTrue(expected[0].HasMetadataMatch);
        Assert.IsFalse(expected[1].HasMetadataMatch);
        Assert.AreSame(item.GetSearchTargets(foldedMatcher), item.GetSearchTargets(foldedMatcher));

        Parallel.For(0, 2000, i => Assert.AreEqual(expected[i % 2], searches[i % 2].Evaluate(item)));
    }

    private static AppListItem CreateShortcutItem()
    {
        return new(new AppItem
        {
            Name = "Deeps",
            Subtitle = "Prints a message",
            CatalogId = @"win32:C:\Commands\_PrintMe.exe|args:",
            LaunchTarget = @"C:\Desktop\Deeps.lnk",
            ResolvedTarget = @"C:\Commands\_PrintMe.exe",
            MatchTerms = [@"C:\Desktop\On Level 3.lnk"],
            Shortcuts =
            [
                new("Deeps", @"C:\Desktop\Deeps.lnk"),
                new("On Level 3", @"C:\Desktop\On Level 3.lnk"),
                new("Xilbaba", @"C:\Desktop\Xilbaba.lnk"),
                new("_PrintMe.exe diagnostic shortcut", @"C:\Desktop\Diagnostic.lnk"),
            ],
        });
    }

    private static AppListItem CreateExecutionAliasItem(string name, string aumid)
    {
        return new(
            new AppItem { Name = name, AppUserModelId = aumid, IsPackaged = true, MatchTerms = ["wt.exe"] });
    }

    private static AppListItem CreateItem(params string[] terms)
    {
        return new(
            new AppItem
            {
                Name = "Editor",
                MatchTerms = terms,
            });
    }

    private sealed class CountingMatcher : IPrecomputedFuzzyMatcher
    {
        private readonly PrecomputedFuzzyMatcher _matcher = new();

        public uint SchemaId => _matcher.SchemaId;

        public Dictionary<string, int> Counts { get; } = new(StringComparer.Ordinal);

        public FuzzyQuery PrecomputeQuery(string input)
        {
            return _matcher.PrecomputeQuery(input);
        }

        public FuzzyTarget PrecomputeTarget(string input)
        {
            return _matcher.PrecomputeTarget(input);
        }

        public int Score(scoped in FuzzyQuery query, scoped in FuzzyTarget target)
        {
            Counts[target.Original] = Counts.GetValueOrDefault(target.Original) + 1;
            return _matcher.Score(query, target);
        }
    }
}
