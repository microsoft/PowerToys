// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.Ext.Apps;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.UI.ViewModels.Commands;
using Microsoft.CmdPal.UI.ViewModels.MainPage;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public sealed partial class EarlyFrameRelevanceTests
{
    public TestContext TestContext { get; set; } = null!;

    private sealed partial class CatalogItem : ListItem, IPrecomputedListItem
    {
        private FuzzyTargetCache _titleCache;
        private FuzzyTargetCache _subtitleCache;

        public CatalogItem(string title, string subtitle, string id)
            : base(new NoOpCommand() { Id = id })
        {
            Title = title;
            Subtitle = subtitle;
            Id = id;
        }

        public string Id { get; }

        public FuzzyTarget GetTitleTarget(IPrecomputedFuzzyMatcher matcher) => _titleCache.GetOrUpdate(matcher, Title);

        public FuzzyTarget GetSubtitleTarget(IPrecomputedFuzzyMatcher matcher) => _subtitleCache.GetOrUpdate(matcher, Subtitle);
    }

    private static IPrecomputedFuzzyMatcher CreateMatcher() => new PrecomputedFuzzyMatcher(new PrecomputedFuzzyMatcherOptions());

    private static ScoringFunction<IListItem> BuildScoringFunction(IRecentCommandsManager history, IPrecomputedFuzzyMatcher matcher, AppSearch? appSearch)
    {
        return (in FuzzyQuery query, IListItem item) => MainListPage.ScoreTopLevelItem(query, item, history, matcher, appSearch);
    }

    private static RoScored<IListItem>[] Score(IReadOnlyList<CatalogItem> apps, string rawQuery, IRecentCommandsManager history, IPrecomputedFuzzyMatcher matcher)
    {
        var query = matcher.PrecomputeQuery(rawQuery);
        var fn = BuildScoringFunction(history, matcher, null);
        return InternalListHelpers.FilterListWithScores(apps.Cast<IListItem>().ToArray(), query, fn);
    }

    private static RecentCommandsManager SeedUses(RecentCommandsManager history, string commandId, int uses)
    {
        for (var i = 0; i < uses; i++)
        {
            history = history.WithHistoryItem(commandId);
        }

        return history;
    }

    // Every app matches "x" only at the Fuzzy tier.
    private static CatalogItem[] BuildFuzzyOnlyCatalogForX() =>
    [
        new CatalogItem("Galaxy Store", "Shop for apps", "app.galaxy"),
        new CatalogItem("Nexus Mods", "Manage game mods", "app.nexus"),
        new CatalogItem("Toolbox Companion", "Developer tools", "app.toolbox"),
        new CatalogItem("Max Cleaner", "Free up disk space", "app.max"),
        new CatalogItem("Voxel Editor", "Edit voxel art", "app.voxel"),
    ];

    // Apps spanning multiple tiers for "c": some titles start with it, some have a non-leading word
    // that does, and some only contain it mid-word.
    private static CatalogItem[] BuildMixedCatalogForC() =>
    [
        new CatalogItem("Calculator", "Perform calculations", "app.calc"),
        new CatalogItem("Calendar", "View your schedule", "app.cal"),
        new CatalogItem("Visual Studio Code", "Code editor", "app.vscode"),
        new CatalogItem("Windows Camera", "Take photos", "app.camera"),
        new CatalogItem("Microsoft Edge", "Browse the web", "app.edge"),
        new CatalogItem("Office Hub", "Productivity apps", "app.office"),
        new CatalogItem("Discord", "Chat with friends", "app.discord"),
    ];

    /// <summary>
    /// The mechanism, locked as a test: when a 1-char query only matches mid-word, every result is
    /// Fuzzy, so seeding frecency on any one of them floats it straight to rank 1.
    /// </summary>
    [TestMethod]
    public void ShortQuery_FrecencyFloatsWeakFuzzyMatchToTop()
    {
        var matcher = CreateMatcher();
        var apps = BuildFuzzyOnlyCatalogForX();

        // With no history, some app is at rank 1 purely on lexical quality.
        var noHistory = Score(apps, "x", new RecentCommandsManager(), matcher);
        Assert.IsTrue(noHistory.Length > 0, "The fuzzy-only catalog must still match 'x'.");
        foreach (var s in noHistory)
        {
            Assert.AreEqual(
                RankTier.Fuzzy,
                MainListRanker.TierOf(s.Score),
                $"Every 'x' match must be Fuzzy tier; '{s.Item.Title}' was {MainListRanker.TierOf(s.Score)}.");
        }

        // Seed heavy frecency on an app that was NOT already at the top, and confirm it floats up.
        var seededId = noHistory[^1].Item is CatalogItem last ? last.Id : throw new InvalidOperationException();
        var seededTitle = noHistory[^1].Item.Title;
        var history = SeedUses(new RecentCommandsManager(), seededId, 40);

        var withHistory = Score(apps, "x", history, matcher);

        TestContext.WriteLine($"no-history rank1='{noHistory[0].Item.Title}', seeded '{seededTitle}' -> rank1='{withHistory[0].Item.Title}'.");

        Assert.AreEqual(RankTier.Fuzzy, MainListRanker.TierOf(withHistory[0].Score), "The floated rank-1 item is still only a Fuzzy match.");
        Assert.AreEqual(seededTitle, withHistory[0].Item.Title, "Frecency should float the seeded weak match to rank 1 within the Fuzzy tier.");
    }

    [TestMethod]
    public void ShortQuery_Admission_RemovesFuzzyOnlyAppsBeforeCounting()
    {
        var matcher = CreateMatcher();
        var apps = BuildFuzzyOnlyCatalogForX();
        var history = SeedUses(new RecentCommandsManager(), apps[0].Id, 40);
        var scored = ScoreApps(apps, "x", history, matcher);

        Assert.AreEqual(0, scored.Length);
        Assert.AreEqual(0, MainListPage.GetVisibleAppCount(scored, 10));
    }

    [TestMethod]
    public void ShortQuery_Admission_KeepsStrongTitleMatches()
    {
        var matcher = CreateMatcher();
        var apps = BuildMixedCatalogForC();
        var history = new RecentCommandsManager();
        var ungated = Score(apps, "c", history, matcher);
        var expected = ungated.Where(s => MainListRanker.TierOf(s.Score) >= RankTier.AcronymWordBoundary).ToArray();
        Assert.IsTrue(expected.Length > 0 && expected.Length < ungated.Length);

        var scored = ScoreApps(apps, "c", history, matcher);

        CollectionAssert.AreEqual(expected.Select(s => s.Item.Title).ToArray(), scored.Select(s => s.Item.Title).ToArray());
        Assert.AreEqual(scored.Length, MainListPage.GetVisibleAppCount(scored, 1000));
        Assert.AreEqual(2, MainListPage.GetVisibleAppCount(scored, 2));
    }

    [TestMethod]
    [DataRow("x", 0)]
    [DataRow("xy", 0)]
    [DataRow("xyz", 1)]
    public void ShortQuery_Admission_StopsAtThreeCharacters(string query, int expected)
    {
        var apps = new[] { new CatalogItem("Axyz", string.Empty, "app") };
        var scored = ScoreApps(apps, query, new RecentCommandsManager(), CreateMatcher());

        Assert.AreEqual(expected, scored.Length);
    }

    [TestMethod]
    public void VisibleAppCount_HandlesNullAndEmptyResults()
    {
        Assert.AreEqual(0, MainListPage.GetVisibleAppCount(null, 10));
        Assert.AreEqual(0, MainListPage.GetVisibleAppCount([], 10));
    }

    private static RoScored<IListItem>[] ScoreApps(IReadOnlyList<CatalogItem> apps, string rawQuery, IRecentCommandsManager history, IPrecomputedFuzzyMatcher matcher)
    {
        var items = apps.Select(app => new AppListItem(
            new AppItem { Name = app.Title, Subtitle = app.Subtitle },
            useThumbnails: false)).ToArray();
        var appSearch = new AppSearch(rawQuery, matcher, ExecutableNameMatchMode.FilenameAndStem);
        return InternalListHelpers.FilterListWithScores<IListItem>(items, matcher.PrecomputeQuery(rawQuery), BuildScoringFunction(history, matcher, appSearch));
    }
}
