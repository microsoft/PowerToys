// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.Ext.Apps.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class AppSearchTests
{
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
        var search = new AppSearch(query, new PrecomputedFuzzyMatcher());
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
        var match = new AppSearch("wt", new PrecomputedFuzzyMatcher()).Evaluate(item);

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
        var match = new AppSearch(query, matcher).Evaluate(item);

        Assert.AreEqual(expected, match.IsExactExecutableMatch);
        if (expected)
        {
            Assert.IsTrue(match.LexicalScore >= match.MetadataScore, "Exact executable names must not receive the metadata penalty.");
        }

        Assert.IsFalse(new AppSearch(query, matcher).Evaluate(CreateItem(query)).IsExactExecutableMatch, "Arbitrary retained metadata is not the launch target.");
    }

    [TestMethod]
    [DataRow("wt")]
    [DataRow("wt.exe")]
    public void Evaluate_SearchesExecutableSourcesWithoutOtherLaunchMetadata(string query)
    {
        var item = new AppListItem(
            new AppItem { Name = "Terminal", ExecutableSourcePaths = [@"C:\Aliases\wt.exe"] },
            useThumbnails: false);
        var match = new AppSearch(query, new PrecomputedFuzzyMatcher()).Evaluate(item);

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

        Assert.IsFalse(new AppSearch("cmd.exe", new PrecomputedFuzzyMatcher()).Evaluate(item).IsExactExecutableMatch);
    }

    [TestMethod]
    [DataRow("wt")]
    [DataRow("wt.exe")]
    [DataRow("WindowsTerminal.exe")]
    public void Evaluate_RecognizesExecutionAliasAndResolvedExecutable(string query)
    {
        var item = new AppListItem(
            new AppItem
            {
                Name = "Terminal",
                ExePath = @"C:\Aliases\wt.exe",
                FullExecutablePath = @"C:\WindowsApps\Microsoft.WindowsTerminal\WindowsTerminal.exe",
            },
            useThumbnails: false);

        Assert.IsTrue(new AppSearch(query, new PrecomputedFuzzyMatcher()).Evaluate(item).IsExactExecutableMatch);
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

        Assert.IsFalse(new AppSearch(commonWord, matcher).Evaluate(item).HasMetadataMatch, commonWord);
        Assert.IsTrue(new AppSearch(path, matcher).Evaluate(item).IsExactMetadataMatch, "Explicit paths remain searchable.");
        Assert.IsTrue(new AppSearch("editor", matcher).Evaluate(item).HasMatch);
    }

    [TestMethod]
    public void Evaluate_AppSpecificDirectoriesRemainSearchableWithoutPathSyntax()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "Needle", "Editor.lnk");
        var item = CreateItem(path);

        Assert.IsTrue(new AppSearch("Needle", new PrecomputedFuzzyMatcher()).Evaluate(item).HasMetadataMatch);
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
        var search = new AppSearch("needle", new PrecomputedFuzzyMatcher());
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
        var match = new AppSearch(query, provider.Current).Evaluate(CreateItem(term));

        Assert.IsTrue(match.HasMetadataMatch);
    }

    [TestMethod]
    public void Targets_AreReusedAndRemainCoherentAcrossConcurrentSchemas()
    {
        var foldedMatcher = new PrecomputedFuzzyMatcher(new() { RemoveDiacritics = true });
        var literalMatcher = new PrecomputedFuzzyMatcher(new() { RemoveDiacritics = false });
        Assert.AreNotEqual(foldedMatcher.SchemaId, literalMatcher.SchemaId);
        var searches = new[] { new AppSearch("cafe", foldedMatcher), new AppSearch("cafe", literalMatcher) };
        var item = CreateItem("Café");
        var expected = searches.Select(search => search.Evaluate(CreateItem("Café"))).ToArray();
        Assert.IsTrue(expected[0].HasMetadataMatch);
        Assert.IsFalse(expected[1].HasMetadataMatch);
        Assert.AreSame(item.GetSearchTargets(foldedMatcher), item.GetSearchTargets(foldedMatcher));

        Parallel.For(0, 2000, i => Assert.AreEqual(expected[i % 2], searches[i % 2].Evaluate(item)));
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
