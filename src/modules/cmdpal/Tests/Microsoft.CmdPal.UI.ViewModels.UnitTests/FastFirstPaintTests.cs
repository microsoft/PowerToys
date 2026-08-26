// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
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
/// Guardrail tests for the "fast first paint" behavior of the main/root page.
///
/// The typing path renders deterministic in-proc results (top-level commands + installed
/// apps) immediately and folds in slow, out-of-proc fallback contributions asynchronously
/// as they resolve. These tests pin the two invariants that make that safe:
///   1. Deterministic results are produced without any fallback contribution present, so
///      first paint never waits on a slow source.
///   2. Late-arriving fallbacks are merged with a fresh score from the same ranker, always
///      at the FallbackFloor tier, so they can never leapfrog deterministic matches, and a
///      superseding query's snapshot replaces any stale one.
/// </summary>
[TestClass]
public sealed partial class FastFirstPaintTests
{
    private static readonly Separator _resultsSeparator = new("Results");
    private static readonly Separator _fallbacksSeparator = new("Fallbacks");

    // A list item whose Title can be mutated to simulate an extension resolving a dynamic
    // fallback title asynchronously, after first paint.
    private sealed partial class MutableListItem : IListItem
    {
        public string Title { get; set; } = string.Empty;

        public string Subtitle { get; set; } = string.Empty;

        public ICommand Command => new NoOpCommand();

        public IDetails? Details => null;

        public IIconInfo? Icon => null;

        public string Section => string.Empty;

        public ITag[] Tags => [];

        public string TextToSuggest => string.Empty;

        public IContextItem[] MoreCommands => [];

#pragma warning disable CS0067 // The event is never used
        public event TypedEventHandler<object, IPropChangedEventArgs>? PropChanged;
#pragma warning restore CS0067 // The event is never used

        public override string ToString() => Title;
    }

    // A stand-in ranker that mirrors the real contract for fallbacks: an item with no
    // (resolved) title scores 0 and is dropped; otherwise it lands at the FallbackFloor tier
    // with a stronger within-floor score when its current title overlaps the query text.
    private static ScoringFunction<IListItem> FloorScorerFor(string queryText)
    {
        return (in FuzzyQuery _, IListItem item) =>
        {
            var title = item.Title;
            if (string.IsNullOrWhiteSpace(title))
            {
                return 0;
            }

            var within = title.Contains(queryText, StringComparison.OrdinalIgnoreCase) ? 100 : 1;
            return MainListRanker.Pack(RankTier.FallbackFloor, within);
        };
    }

    private static RoScored<IListItem> ScoredFuzzy(string title, int within)
        => new(score: MainListRanker.Pack(RankTier.Fuzzy, within), item: new MutableListItem { Title = title });

    private static RoScored<IListItem> ScoredFloorMax(string title)
        => new(score: MainListRanker.Pack(RankTier.FallbackFloor, MainListRanker.TierStride - 1), item: new MutableListItem { Title = title });

    [TestMethod]
    public void FirstPaint_ProducesDeterministicResults_WithNoFallbackContribution()
    {
        var command = ScoredFuzzy("Notepad", within: 50);
        var app = ScoredFuzzy("Notepad++", within: 40);
        var filtered = new List<RoScored<IListItem>> { command };
        var apps = new List<RoScored<IListItem>> { app };

        // No scored fallbacks and no fallback items: this models first paint before any slow
        // out-of-proc source has responded.
        var result = MainListPageResultFactory.Create(
            filtered,
            scoredFallbackItems: null,
            apps,
            fallbackItems: null,
            _resultsSeparator,
            _fallbacksSeparator,
            appResultLimit: 10);

        CollectionAssert.AreEqual(
            new IListItem[] { _resultsSeparator, command.Item, app.Item },
            result,
            "First paint must render the deterministic command/app results without any fallback contribution.");
    }

    [TestMethod]
    public void UnresolvedFallback_IsAbsentAtFirstPaint()
    {
        var fallback = new MutableListItem { Title = string.Empty };
        IReadOnlyList<IListItem> sources = new IListItem[] { fallback };

        var scored = MainListPage.ScoreDeferredFallbacks(sources, default, FloorScorerFor("remote"));

        Assert.IsNull(scored, "A fallback whose dynamic title has not resolved yet must not appear.");
    }

    [TestMethod]
    public void SlowFallback_FoldsIn_WhenItsTitleResolves()
    {
        var fallback = new MutableListItem { Title = string.Empty };
        IReadOnlyList<IListItem> sources = new IListItem[] { fallback };
        var scorer = FloorScorerFor("remote");

        // First paint: unresolved title -> not present.
        Assert.IsNull(MainListPage.ScoreDeferredFallbacks(sources, default, scorer));

        // The extension resolves the dynamic title asynchronously (off the typing path).
        fallback.Title = "Remote Desktop: server01";

        // A later refresh re-scores the same snapshot and folds the fallback in.
        var after = MainListPage.ScoreDeferredFallbacks(sources, default, scorer);

        Assert.IsNotNull(after);
        Assert.AreEqual(1, after!.Count);
        Assert.AreSame(fallback, after[0].Item);
        Assert.AreEqual(RankTier.FallbackFloor, MainListRanker.TierOf(after[0].Score));
    }

    [TestMethod]
    public void ReScore_ReflectsCurrentTitle_NotAFrozenValue()
    {
        var fallback = new MutableListItem { Title = "no match here" };
        IReadOnlyList<IListItem> sources = new IListItem[] { fallback };
        var scorer = FloorScorerFor("remote");

        var weak = MainListPage.ScoreDeferredFallbacks(sources, default, scorer);
        Assert.IsNotNull(weak);
        var weakScore = weak![0].Score;

        // The title resolves to a strong match. If scoring were frozen at keystroke time, the
        // weak score would persist; deferred re-scoring must reflect the fresh title.
        fallback.Title = "Remote host";
        var strong = MainListPage.ScoreDeferredFallbacks(sources, default, scorer);

        Assert.IsNotNull(strong);
        Assert.IsTrue(strong![0].Score > weakScore, "Re-scoring must reflect the freshly resolved title, not a frozen value.");
    }

    [TestMethod]
    public void ReScoreUsesLatestSnapshot_StaleStrongMatchNotApplied()
    {
        // This exercises the render-path re-score directly: it always scores whatever snapshot is
        // current, so a superseding keystroke's query wins and the prior query's strong score is
        // gone. In production the field pair (_globalFallbackSources + _globalFallbackQuery) is
        // written under lock (commands) in UpdateSearchTextCore and read under the same lock in
        // GetItems, so the swap is atomic; this test stands in for that behavior at the helper level.
        var fallback = new MutableListItem { Title = "Remote Desktop" };
        IReadOnlyList<IListItem> sources = new IListItem[] { fallback };

        // Query A ("remote") is a strong match for the current title.
        var a = MainListPage.ScoreDeferredFallbacks(sources, default, FloorScorerFor("remote"));
        Assert.IsNotNull(a);
        var strongScore = a![0].Score;

        // A newer keystroke installs query B ("zzz"), which does not overlap the title. The
        // render path always scores the latest snapshot, so query A's strong score is gone.
        var b = MainListPage.ScoreDeferredFallbacks(sources, default, FloorScorerFor("zzz"));

        Assert.IsNotNull(b);
        Assert.IsTrue(b![0].Score < strongScore, "A superseding query must not inherit the prior query's stale score.");
    }

    [TestMethod]
    public void LateFallback_MergesBelowDeterministicMatches_NoLeapfrog()
    {
        // A minimal deterministic Fuzzy match versus a fallback with the maximum possible
        // within-floor score. The tier ladder must still keep the fallback below the command.
        var command = ScoredFuzzy("Notepad", within: 1);
        var fallback = ScoredFloorMax("Search the web for notepad");

        var filtered = new List<RoScored<IListItem>> { command };
        var scoredFallbacks = new List<RoScored<IListItem>> { fallback };

        var result = MainListPageResultFactory.Create(
            filtered,
            scoredFallbacks,
            filteredApps: null,
            fallbackItems: null,
            _resultsSeparator,
            _fallbacksSeparator,
            appResultLimit: 10);

        Assert.AreEqual(_resultsSeparator, result[0]);
        Assert.AreSame(command.Item, result[1], "Deterministic matches must sort above floor-tier fallbacks.");
        Assert.AreSame(fallback.Item, result[2], "A late floor-tier fallback merges after deterministic results, never leapfrogging them.");
    }
}
