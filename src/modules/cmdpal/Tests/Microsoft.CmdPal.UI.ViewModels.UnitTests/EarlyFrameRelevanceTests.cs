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
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

/// <summary>
/// GUARDRAIL for the Phase 7c early-frame relevance change. Phase 7c stops the "completely wrong
/// when I start typing" flash by withholding, on the render path, the large low-confidence fuzzy
/// app tail for ultra-short (1-2 char) queries. The change is confined to the transient early
/// frame: it slices the already-scored, already-sorted app array and never re-scores or reorders,
/// so the settled order of a discriminating (3+ char) query is byte-identical to before.
///
/// These tests lock two things:
///  1. The confirmed mechanism (Mechanism A): a 1-char query drops a huge fraction of the catalog
///     into the single Fuzzy tier, where within-tier order is decided almost entirely by frecency,
///     so the most-frecent app floats to rank 1 on a weak, mid-word subsequence match.
///  2. The fix: for an ultra-short query the gate withholds that Fuzzy tail (keeping only the
///     letter-relevant word-boundary/prefix/exact matches), while for a 3+ char query the scored
///     apps are passed through unchanged (same array reference).
///
/// Everything here is deterministic (index-driven catalog, injected frecency, no wall-clock
/// ordering dependence), so it is CI-safe and never flakes.
/// </summary>
[TestClass]
public sealed partial class EarlyFrameRelevanceTests
{
    public TestContext TestContext { get; set; } = null!;

    private sealed partial class CatalogItem : IListItem, IPrecomputedListItem
    {
        private FuzzyTargetCache _titleCache;
        private FuzzyTargetCache _subtitleCache;

        public CatalogItem(string title, string subtitle, string id)
        {
            Title = title;
            Subtitle = subtitle;
            Id = id;
            Command = new NoOpCommand() { Id = id };
        }

        public string Title { get; }

        public string Subtitle { get; }

        public string Id { get; }

        public ICommand Command { get; }

        public IDetails? Details => null;

        public IIconInfo? Icon => null;

        public string Section => string.Empty;

        public ITag[] Tags => [];

        public string TextToSuggest => string.Empty;

        public IContextItem[] MoreCommands => [];

#pragma warning disable CS0067 // The event is never used
        public event TypedEventHandler<object, IPropChangedEventArgs>? PropChanged;
#pragma warning restore CS0067

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

    // Apps that match the query "x" ONLY as a mid-word subsequence: no title starts with "x" and no
    // word inside a title starts with "x", so every match classifies at the Fuzzy tier. This is the
    // pathological ultra-short-query shape - a big weak-match set with no confident matches at all.
    private static CatalogItem[] BuildFuzzyOnlyCatalogForX() =>
    [
        new CatalogItem("Galaxy Store", "Shop for apps", "app.galaxy"),
        new CatalogItem("Nexus Mods", "Manage game mods", "app.nexus"),
        new CatalogItem("Toolbox Companion", "Developer tools", "app.toolbox"),
        new CatalogItem("Max Cleaner", "Free up disk space", "app.max"),
        new CatalogItem("Voxel Editor", "Edit voxel art", "app.voxel"),
    ];

    // Apps for the query "c" spanning multiple tiers: some titles start with "c" (Prefix), some have
    // a non-leading word starting with "c" (word boundary), and some only contain "c" mid-word
    // (Fuzzy). Used to prove the gate keeps the confident head and drops only the fuzzy tail.
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
    /// Investigation evidence, locked as a test: for a 1-char query whose only matches are mid-word
    /// subsequences, EVERY result classifies at the Fuzzy tier - so whatever sits at rank 1 is a
    /// weak match, and seeding frecency on one such app floats it to the top of that weak tier. This
    /// is Mechanism A that Phase 7c targets.
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
    /// The fix: for an ultra-short (1-char) query the gate withholds the ENTIRE Fuzzy tail, so the
    /// frecency-floated weak match can no longer be surfaced at rank 1 while the user is still
    /// typing. Here every match is Fuzzy, so the gated app set is empty (commands/fallbacks, added
    /// by the result factory, still render).
    /// </summary>
    [TestMethod]
    public void ShortQuery_Gate_WithholdsEntireFuzzyTail()
    {
        var matcher = CreateMatcher();
        var apps = BuildFuzzyOnlyCatalogForX();

        var seededId = apps[0].Id;
        var history = SeedUses(new RecentCommandsManager(), seededId, 40);

        var scored = Score(apps, "x", history, matcher);
        Assert.IsTrue(scored.Length > 0, "Precondition: the ungated result surfaces weak fuzzy matches.");

        var gated = MainListPage.ApplyShortQueryAppGate(scored, queryLength: 1);

        Assert.IsNotNull(gated);
        Assert.AreEqual(0, gated!.Count, "For a 1-char query with only fuzzy matches, the whole low-confidence tail is withheld.");
    }

    /// <summary>
    /// The gate keeps the confident head (word-boundary / prefix / exact matches) and drops only the
    /// Fuzzy tail for an ultra-short query. Verified against a mixed catalog for "c".
    /// </summary>
    [TestMethod]
    public void ShortQuery_Gate_KeepsConfidentTiers_DropsFuzzyTail()
    {
        var matcher = CreateMatcher();
        var apps = BuildMixedCatalogForC();

        var scored = Score(apps, "c", new RecentCommandsManager(), matcher);

        var fuzzyCount = scored.Count(s => MainListRanker.TierOf(s.Score) == RankTier.Fuzzy);
        var confidentCount = scored.Count(s => (int)MainListRanker.TierOf(s.Score) >= (int)RankTier.AcronymWordBoundary);

        Assert.IsTrue(fuzzyCount > 0, "Precondition: the mixed catalog produces some fuzzy-tail matches for 'c'.");
        Assert.IsTrue(confidentCount > 0, "Precondition: the mixed catalog produces some confident matches for 'c'.");

        var gated = MainListPage.ApplyShortQueryAppGate(scored, queryLength: 1);
        Assert.IsNotNull(gated);

        Assert.AreEqual(confidentCount, gated!.Count, "Only the confident (tier >= word-boundary) head is kept.");
        foreach (var s in gated)
        {
            Assert.IsTrue(
                (int)MainListRanker.TierOf(s.Score) >= (int)RankTier.AcronymWordBoundary,
                $"Gated item '{s.Item.Title}' must be word-boundary tier or higher, was {MainListRanker.TierOf(s.Score)}.");
        }

        // The gated head is a contiguous prefix of the scored array, in the exact same order.
        for (var i = 0; i < gated.Count; i++)
        {
            Assert.AreSame(scored[i].Item, gated[i].Item, $"Gated item at index {i} must be the same instance and position as the scored array.");
        }
    }

    /// <summary>
    /// INVARIANT: for a discriminating (3+ char) query the gate is a no-op - it returns the exact
    /// same array instance, so the settled order of a meaningful query is byte-identical to the
    /// pre-7c behavior and the Phase 7b equivalence guarantee is untouched.
    /// </summary>
    [TestMethod]
    public void DiscriminatingQuery_Gate_ReturnsInputUnchanged()
    {
        var matcher = CreateMatcher();
        var apps = BuildMixedCatalogForC();

        foreach (var raw in new[] { "cal", "calc", "code" })
        {
            var scored = Score(apps, raw, new RecentCommandsManager(), matcher);
            var gated = MainListPage.ApplyShortQueryAppGate(scored, raw.Length);

            Assert.AreSame(scored, gated, $"A {raw.Length}-char query ('{raw}') must pass the scored apps through unchanged.");
        }
    }

    /// <summary>
    /// The gate fires for query lengths 1 and 2 (ultra-short) but not for length 3+. Confirmed
    /// against the fuzzy-only catalog, where firing empties the set and not-firing passes it through.
    /// </summary>
    [TestMethod]
    public void Gate_LengthBoundary_FiresForOneAndTwo_NotThree()
    {
        var matcher = CreateMatcher();
        var apps = BuildFuzzyOnlyCatalogForX();
        var scored = Score(apps, "x", new RecentCommandsManager(), matcher);
        Assert.IsTrue(scored.Length > 0, "Precondition: 'x' matches fuzzily.");

        Assert.AreEqual(0, MainListPage.ApplyShortQueryAppGate(scored, 1)!.Count, "Length 1 must gate.");
        Assert.AreEqual(0, MainListPage.ApplyShortQueryAppGate(scored, 2)!.Count, "Length 2 must gate.");
        Assert.AreSame(scored, MainListPage.ApplyShortQueryAppGate(scored, 3), "Length 3 must not gate.");
    }

    /// <summary>
    /// The gate never touches an empty or null app set, and treats a zero-length query (the default
    /// view, no search) as a pass-through.
    /// </summary>
    [TestMethod]
    public void Gate_NullEmptyAndZeroLength_AreNoOps()
    {
        Assert.IsNull(MainListPage.ApplyShortQueryAppGate(null, 1));

        var empty = Array.Empty<RoScored<IListItem>>();
        Assert.AreSame(empty, MainListPage.ApplyShortQueryAppGate(empty, 1));

        var matcher = CreateMatcher();
        var scored = Score(BuildFuzzyOnlyCatalogForX(), "x", new RecentCommandsManager(), matcher);
        Assert.AreSame(scored, MainListPage.ApplyShortQueryAppGate(scored, 0), "A zero-length query is a pass-through.");
    }

    /// <summary>
    /// Unit-level check of the prefix-length helper: because the scored array is sorted descending
    /// by packed score and the tier occupies the high bits, all entries at or above a tier form a
    /// contiguous prefix, and the helper returns that prefix length.
    /// </summary>
    [TestMethod]
    public void HighConfidenceAppPrefixLength_CountsLeadingHighTierEntries()
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

        Assert.AreEqual(5, MainListPage.HighConfidenceAppPrefixLength(scored, RankTier.FallbackFloor));
        Assert.AreEqual(4, MainListPage.HighConfidenceAppPrefixLength(scored, RankTier.Fuzzy));
        Assert.AreEqual(3, MainListPage.HighConfidenceAppPrefixLength(scored, RankTier.AcronymWordBoundary));
        Assert.AreEqual(2, MainListPage.HighConfidenceAppPrefixLength(scored, RankTier.Prefix));
        Assert.AreEqual(1, MainListPage.HighConfidenceAppPrefixLength(scored, RankTier.ExactTitle));
        Assert.AreEqual(0, MainListPage.HighConfidenceAppPrefixLength(scored, RankTier.AliasExact));
    }

    /// <summary>
    /// FIX #2 guardrail: the gate decision is driven entirely by the length it is handed - the query
    /// length PUBLISHED atomically with the app array - not by any later value. Feeding the published
    /// short length (2) withholds the fuzzy tail even though a longer length would pass it through,
    /// which is exactly why MainListPage gates on the published _filteredAppsQueryLength rather than
    /// the live SearchText the UI thread may already have advanced past on a 2-to-3 char transition.
    /// </summary>
    [TestMethod]
    public void Gate_DecidesOnSuppliedPublishedLength_NotLiveText()
    {
        RoScored<IListItem> Fuzzy(int within)
            => new(new CatalogItem($"fuzzy.{within}", string.Empty, $"fuzzy.{within}"), MainListRanker.Pack(RankTier.Fuzzy, within));

        var scored = new[] { Fuzzy(30), Fuzzy(20), Fuzzy(10) };

        // Published length 2 (the query that produced this array is still ultra-short): withhold.
        var gatedByPublished = MainListPage.ApplyShortQueryAppGate(scored, queryLength: 2);
        Assert.IsNotNull(gatedByPublished);
        Assert.AreEqual(0, gatedByPublished!.Count, "Gating on the published short length (2) must withhold the fuzzy tail.");

        // The SAME array gated on a longer length passes through, so gating on a stale/live longer
        // length would wrongly expose the tail. That is the regression Fix #2 prevents.
        Assert.AreSame(scored, MainListPage.ApplyShortQueryAppGate(scored, queryLength: 5), "A longer length would pass the tail through, proving the supplied length is what decides.");
    }

    /// <summary>
    /// FIX #3 guardrail: the settled-search telemetry count reflects the GATED visible apps for an
    /// ultra-short query, not the full ungated app array. For a discriminating query it is the full
    /// (capped) count. This keeps the reported result count consistent with what the user is shown.
    /// </summary>
    [TestMethod]
    public void GatedVisibleAppCount_ShortQuery_CountsOnlyGatedApps()
    {
        var matcher = CreateMatcher();
        var apps = BuildMixedCatalogForC();
        var scored = Score(apps, "c", new RecentCommandsManager(), matcher);

        var full = scored.Length;
        var confident = scored.Count(s => (int)MainListRanker.TierOf(s.Score) >= (int)RankTier.AcronymWordBoundary);
        Assert.IsTrue(confident < full, "Precondition: a fuzzy tail exists so the gated count is strictly less than the full count.");

        const int NoCap = 1000;

        // Ultra-short published length: only the confident head is counted.
        Assert.AreEqual(confident, MainListPage.GatedVisibleAppCount(scored, queryLength: 1, appResultLimit: NoCap));
        Assert.AreEqual(confident, MainListPage.GatedVisibleAppCount(scored, queryLength: 2, appResultLimit: NoCap));

        // Discriminating published length: the full set (capped) is counted.
        Assert.AreEqual(full, MainListPage.GatedVisibleAppCount(scored, queryLength: 3, appResultLimit: NoCap));
        Assert.AreEqual(full, MainListPage.GatedVisibleAppCount(scored, queryLength: 0, appResultLimit: NoCap), "A zero-length (default view) count is ungated.");
    }

    /// <summary>
    /// FIX #3: the gated visible count is still capped by the app result limit, a fuzzy-only
    /// ultra-short query reports zero visible apps (its whole tail is withheld), and null/empty
    /// inputs report zero.
    /// </summary>
    [TestMethod]
    public void GatedVisibleAppCount_RespectsCap_AndZeroForWithheldOrEmpty()
    {
        var matcher = CreateMatcher();

        // Fuzzy-only ultra-short query: nothing confident, so the gated count is zero.
        var fuzzyOnly = Score(BuildFuzzyOnlyCatalogForX(), "x", new RecentCommandsManager(), matcher);
        Assert.IsTrue(fuzzyOnly.Length > 0, "Precondition: 'x' matches fuzzily.");
        Assert.AreEqual(0, MainListPage.GatedVisibleAppCount(fuzzyOnly, queryLength: 1, appResultLimit: 1000));

        // The cap applies on the ungated path.
        var mixed = Score(BuildMixedCatalogForC(), "c", new RecentCommandsManager(), matcher);
        Assert.IsTrue(mixed.Length > 2, "Precondition: the mixed catalog has more than two matches so the cap bites.");
        Assert.AreEqual(2, MainListPage.GatedVisibleAppCount(mixed, queryLength: 3, appResultLimit: 2), "The full count is capped by the app result limit.");

        // Null and empty report zero.
        Assert.AreEqual(0, MainListPage.GatedVisibleAppCount(null, 1, 1000));
        Assert.AreEqual(0, MainListPage.GatedVisibleAppCount(Array.Empty<RoScored<IListItem>>(), 1, 1000));
    }
}
