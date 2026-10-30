// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.Ext.Apps.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class AppSearchTests
{
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
        var owner = preview.App.UserModelId.ToUpperInvariant();
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

        owner = stable.App.UserModelId;
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
        var search = new AppSearch("w", new PrecomputedFuzzyMatcher(), ExecutableNameMatchMode.FilenameAndStem, item.App.UserModelId);

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
                ExePath = @"C:\Tools\LaunchNeedle.lnk",
                FullExecutablePath = @"C:\ResolvedTargetNeedle\Editor.exe",
                UserModelId = "Contoso.PackagedIdentityNeedle!Editor",
                PackageFamilyName = "Contoso.FamilyNeedle_publisher",
                MatchTerms = ["LegacyAliasNeedle"],
            },
            useThumbnails: false);
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
            new AppItem
            {
                Name = "Command Prompt",
                ExePath = @"C:\Start Menu\Command Prompt.lnk",
                FullExecutablePath = @"C:\Windows\System32\cmd.exe",
                Arguments = arguments,
            },
            useThumbnails: false);
        var matcher = new PrecomputedFuzzyMatcher();
        var match = new AppSearch(query, matcher, ExecutableNameMatchMode.FilenameAndStem).Evaluate(item);

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
            FullPath = @"C:\Windows\System32\cmd.exe",
            Arguments = arguments,
        }.ToAppItem();
        var item = new AppListItem(app, useThumbnails: false);
        var defaultItem = new AppListItem(app, useThumbnails: false);
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
        Assert.AreEqual(arguments, item.App.Arguments);
        Assert.AreEqual(defaultItem.Command!.Id, item.Command!.Id);
    }

    [TestMethod]
    public void Evaluate_ExecutableNameMatchingStaysWithinPerAppAllocationBudget()
    {
        const int iterations = 10000;
        const int warmupIterations = 1000;
        const long extraBytesPerAppBudget = 16;
        var item = new AppListItem(
            new AppItem { Name = "Shell", ExePath = @"C:\Tools\cmd.exe" },
            useThumbnails: false);
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
            new AppItem { Name = "Terminal", ExecutableSourcePaths = [@"C:\Aliases\wt.exe"] },
            useThumbnails: false);
        var match = new AppSearch(query, new PrecomputedFuzzyMatcher(), ExecutableNameMatchMode.FilenameAndStem).Evaluate(item);

        Assert.IsTrue(match.IsExactExecutableMatch);
        Assert.IsTrue(match.HasMatch);
        Assert.IsTrue(match.LexicalScore > 0);
    }

    [TestMethod]
    public void Evaluate_ExecutableExtensionRequiresTheCompleteFilename()
    {
        var item = new AppListItem(
            new AppItem { Name = "Another shell", ExePath = @"C:\Tools\cmd.exe.exe" },
            useThumbnails: false);

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
                ExePath = @"C:\Aliases\wt.exe",
                FullExecutablePath = @"C:\WindowsApps\Microsoft.WindowsTerminal\WindowsTerminal.exe",
            },
            useThumbnails: false);

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
            new AppItem { Name = "Editor", ExePath = path, DirPath = Path.GetDirectoryName(path)!, MatchTerms = [path] },
            useThumbnails: false);
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

    private static AppListItem CreateExecutionAliasItem(string name, string aumid)
    {
        return new(
            new AppItem { Name = name, UserModelId = aumid, IsPackaged = true, MatchTerms = ["wt.exe"] },
            useThumbnails: false);
    }

    private static AppListItem CreateItem(params string[] terms) => new(
        new AppItem
        {
            Name = "Editor",
            AppIdentifier = "editor",
            MatchTerms = terms,
        },
        useThumbnails: false);
}
