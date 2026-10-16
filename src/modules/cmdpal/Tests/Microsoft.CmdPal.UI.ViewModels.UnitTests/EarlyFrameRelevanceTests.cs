// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Common.Text;
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

    private static ScoringFunction<IListItem> BuildScoringFunction(IRecentCommandsManager history, IPrecomputedFuzzyMatcher matcher)
        => (in FuzzyQuery query, IListItem item) => MainListPage.ScoreTopLevelItem(query, item, history, matcher, null);

    private static RoScored<IListItem>[] Score(IReadOnlyList<CatalogItem> apps, string rawQuery, IRecentCommandsManager history, IPrecomputedFuzzyMatcher matcher)
    {
        var query = matcher.PrecomputeQuery(rawQuery);
        var fn = BuildScoringFunction(history, matcher);
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

    /// <summary>
    /// A short query filters every fuzzy-only app match.
    /// </summary>
    [TestMethod]
    public void ShortQuery_Filter_RemovesEveryFuzzyMatch()
    {
        var matcher = CreateMatcher();
        var apps = BuildFuzzyOnlyCatalogForX();

        var seededId = apps[0].Id;
        var history = SeedUses(new RecentCommandsManager(), seededId, 40);

        var scored = Score(apps, "x", history, matcher);
        Assert.IsTrue(scored.Length > 0, "Precondition: the ungated result surfaces weak fuzzy matches.");

        var filtered = MainListPage.FilterAppsForShortQueries(scored, queryLength: 1);

        Assert.IsNotNull(filtered);
        Assert.AreEqual(0, filtered!.Count, "A one-character query should filter fuzzy-only app matches.");
    }

    /// <summary>
    /// The filter keeps high-confidence matches and removes fuzzy matches.
    /// </summary>
    [TestMethod]
    public void ShortQuery_Filter_KeepsHighConfidenceMatches()
    {
        var matcher = CreateMatcher();
        var apps = BuildMixedCatalogForC();

        var scored = Score(apps, "c", new RecentCommandsManager(), matcher);

        var fuzzyCount = scored.Count(s => MainListRanker.TierOf(s.Score) == RankTier.Fuzzy);
        var confidentCount = scored.Count(s => (int)MainListRanker.TierOf(s.Score) >= (int)RankTier.AcronymWordBoundary);

        Assert.IsTrue(fuzzyCount > 0, "Precondition: the mixed catalog produces some fuzzy-tail matches for 'c'.");
        Assert.IsTrue(confidentCount > 0, "Precondition: the mixed catalog produces some confident matches for 'c'.");

        var filtered = MainListPage.FilterAppsForShortQueries(scored, queryLength: 1);
        Assert.IsNotNull(filtered);

        Assert.AreEqual(confidentCount, filtered!.Count, "Only word-boundary or stronger matches should remain.");
        foreach (var s in filtered)
        {
            Assert.IsTrue(
                (int)MainListRanker.TierOf(s.Score) >= (int)RankTier.AcronymWordBoundary,
                $"Filtered item '{s.Item.Title}' must be word-boundary tier or higher, was {MainListRanker.TierOf(s.Score)}.");
        }

        // Filtering preserves the scored order.
        for (var i = 0; i < filtered.Count; i++)
        {
            Assert.AreSame(scored[i].Item, filtered[i].Item, $"Filtered item at index {i} must keep its scored position.");
        }
    }

    /// <summary>
    /// Queries longer than two characters return the original array.
    /// </summary>
    [TestMethod]
    public void LongerQuery_Filter_ReturnsInputUnchanged()
    {
        var matcher = CreateMatcher();
        var apps = BuildMixedCatalogForC();

        foreach (var raw in new[] { "cal", "calc", "code" })
        {
            var scored = Score(apps, raw, new RecentCommandsManager(), matcher);
            var filtered = MainListPage.FilterAppsForShortQueries(scored, raw.Length);

            Assert.AreSame(scored, filtered, $"A {raw.Length}-character query ('{raw}') should return the original array.");
        }
    }

    /// <summary>
    /// The filter applies to query lengths one and two.
    /// </summary>
    [TestMethod]
    public void Filter_LengthBoundary_AppliesToOneAndTwo_NotThree()
    {
        var matcher = CreateMatcher();
        var apps = BuildFuzzyOnlyCatalogForX();
        var scored = Score(apps, "x", new RecentCommandsManager(), matcher);
        Assert.IsTrue(scored.Length > 0, "Precondition: 'x' matches fuzzily.");

        Assert.AreEqual(0, MainListPage.FilterAppsForShortQueries(scored, 1)!.Count, "Length 1 should be filtered.");
        Assert.AreEqual(0, MainListPage.FilterAppsForShortQueries(scored, 2)!.Count, "Length 2 should be filtered.");
        Assert.AreSame(scored, MainListPage.FilterAppsForShortQueries(scored, 3), "Length 3 should not be filtered.");
    }

    /// <summary>
    /// Null, empty, and default-view inputs are unchanged.
    /// </summary>
    [TestMethod]
    public void Filter_NullEmptyAndZeroLength_AreNoOps()
    {
        Assert.IsNull(MainListPage.FilterAppsForShortQueries(null, 1));

        var empty = Array.Empty<RoScored<IListItem>>();
        Assert.AreSame(empty, MainListPage.FilterAppsForShortQueries(empty, 1));

        var matcher = CreateMatcher();
        var scored = Score(BuildFuzzyOnlyCatalogForX(), "x", new RecentCommandsManager(), matcher);
        Assert.AreSame(scored, MainListPage.FilterAppsForShortQueries(scored, 0), "A zero-length query should return the original array.");
    }

    /// <summary>
    /// Counts the leading entries at or above the requested tier.
    /// </summary>
    [TestMethod]
    public void GetHighConfidenceAppsCount_CountsLeadingHighTierEntries()
    {
        RoScored<IListItem> Make(RankTier tier, int within)
            => new(new CatalogItem($"{tier}", string.Empty, $"{tier}.{within}"), MainListRanker.Pack(tier, within));

        // Sorted descending by packed score: Exact(5) > Prefix(4) > WordBoundary(3) > Fuzzy(2) > Fallback(1).
        var scored = new[]
        {
            Make(RankTier.ExactTitle, 100),
            Make(RankTier.Prefix, 50),
            Make(RankTier.AcronymWordBoundary, 20),
            Make(RankTier.Fuzzy, 9000),
            Make(RankTier.FallbackFloor, 5),
        };

        Assert.AreEqual(5, MainListPage.GetHighConfidenceAppsCount(scored, RankTier.FallbackFloor));
        Assert.AreEqual(4, MainListPage.GetHighConfidenceAppsCount(scored, RankTier.Fuzzy));
        Assert.AreEqual(3, MainListPage.GetHighConfidenceAppsCount(scored, RankTier.AcronymWordBoundary));
        Assert.AreEqual(2, MainListPage.GetHighConfidenceAppsCount(scored, RankTier.Prefix));
        Assert.AreEqual(1, MainListPage.GetHighConfidenceAppsCount(scored, RankTier.ExactTitle));
        Assert.AreEqual(0, MainListPage.GetHighConfidenceAppsCount(scored, RankTier.AliasExact));
    }

    /// <summary>
    /// Uses the query length published with the scored array.
    /// </summary>
    [TestMethod]
    public void Filter_UsesSuppliedPublishedLength()
    {
        RoScored<IListItem> Fuzzy(int within)
            => new(new CatalogItem($"fuzzy.{within}", string.Empty, $"fuzzy.{within}"), MainListRanker.Pack(RankTier.Fuzzy, within));

        var scored = new[] { Fuzzy(30), Fuzzy(20), Fuzzy(10) };

        var filteredByPublished = MainListPage.FilterAppsForShortQueries(scored, queryLength: 2);
        Assert.IsNotNull(filteredByPublished);
        Assert.AreEqual(0, filteredByPublished!.Count, "The published short length should filter fuzzy matches.");

        Assert.AreSame(scored, MainListPage.FilterAppsForShortQueries(scored, queryLength: 5), "A longer published length should return the original array.");
    }

    /// <summary>
    /// Telemetry counts only visible apps after filtering.
    /// </summary>
    [TestMethod]
    public void GetVisibleAppCount_ShortQuery_CountsOnlyFilteredApps()
    {
        var matcher = CreateMatcher();
        var apps = BuildMixedCatalogForC();
        var scored = Score(apps, "c", new RecentCommandsManager(), matcher);

        var full = scored.Length;
        var confident = scored.Count(s => (int)MainListRanker.TierOf(s.Score) >= (int)RankTier.AcronymWordBoundary);
        Assert.IsTrue(confident < full, "Precondition: fuzzy matches make the filtered count smaller.");

        const int NoCap = 1000;

        Assert.AreEqual(confident, MainListPage.GetVisibleAppCount(scored, queryLength: 1, appResultLimit: NoCap));
        Assert.AreEqual(confident, MainListPage.GetVisibleAppCount(scored, queryLength: 2, appResultLimit: NoCap));

        Assert.AreEqual(full, MainListPage.GetVisibleAppCount(scored, queryLength: 3, appResultLimit: NoCap));
        Assert.AreEqual(full, MainListPage.GetVisibleAppCount(scored, queryLength: 0, appResultLimit: NoCap), "A zero-length query should count the full set.");
    }

    /// <summary>
    /// The count respects the app limit and handles empty input.
    /// </summary>
    [TestMethod]
    public void GetVisibleAppCount_RespectsCap_AndEmptyInput()
    {
        var matcher = CreateMatcher();

        var fuzzyOnly = Score(BuildFuzzyOnlyCatalogForX(), "x", new RecentCommandsManager(), matcher);
        Assert.IsTrue(fuzzyOnly.Length > 0, "Precondition: 'x' matches fuzzily.");
        Assert.AreEqual(0, MainListPage.GetVisibleAppCount(fuzzyOnly, queryLength: 1, appResultLimit: 1000));

        var mixed = Score(BuildMixedCatalogForC(), "c", new RecentCommandsManager(), matcher);
        Assert.IsTrue(mixed.Length > 2, "Precondition: the mixed catalog has more than two matches so the cap bites.");
        Assert.AreEqual(2, MainListPage.GetVisibleAppCount(mixed, queryLength: 3, appResultLimit: 2), "The app result limit should cap the count.");

        Assert.AreEqual(0, MainListPage.GetVisibleAppCount(null, 1, 1000));
        Assert.AreEqual(0, MainListPage.GetVisibleAppCount(Array.Empty<RoScored<IListItem>>(), 1, 1000));
    }
}
