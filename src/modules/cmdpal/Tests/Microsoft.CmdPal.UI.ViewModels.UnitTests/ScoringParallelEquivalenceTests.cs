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
/// GUARDRAIL for the Phase 7b throughput work. The hot-path scoring pass was moved off the
/// TopLevelCommands lock and the dominant apps pass was parallelized. The optimization is only
/// permitted to change HOW FAST and WHERE the settled list is computed - never WHAT it is. This
/// test locks that invariant in: for a representative synthetic catalog (a few thousand apps plus
/// hundreds of commands) and several queries - including the 1-char pathological case, the extend
/// (incremental narrowing) path, and the shrink/retype rebuild path - the parallel scorer
/// (<see cref="InternalListHelpers.FilterListWithScoresParallel{T}"/>) must produce a result that
/// is BYTE-IDENTICAL in order and score to the reference sequential scorer
/// (<see cref="InternalListHelpers.FilterListWithScores{T}"/>).
///
/// The comparison is by item reference and packed score at every index, so any reordering - even a
/// tie-break difference - fails the test. Everything here is deterministic (index-driven catalog,
/// no RNG, no wall clock, order-preserving partition/merge), so it is CI-safe and never flakes on
/// thread scheduling.
/// </summary>
[TestClass]
public sealed partial class ScoringParallelEquivalenceTests
{
    // Large enough that the parallel path is actually taken (well over the internal sequential
    // threshold) and multiple partitions are exercised, small enough to stay fast in CI.
    private const int AppCount = 4000;
    private const int CommandCount = 300;
    private const int HistorySeedCount = 250;

    // Queries chosen to exercise distinct scoring shapes: "c" is the pathological 1-char case that
    // matches a large fraction of the catalog; the "ca"/"cal"/"calc" chain is the extend path; the
    // acronym/word-boundary and multi-word cases stress the tier classifier.
    private static readonly string[] Queries =
        ["c", "ca", "cal", "calc", "vs", "vsc", "vs code", "term", "set", "e"];

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

    private static readonly string[] Nouns =
    [
        "Calculator", "Calendar", "Camera", "Canvas", "Command", "Control", "Cloud", "Cast",
        "Visual", "Studio", "Code", "Terminal", "Task", "Notepad", "Paint", "Photos", "Player",
        "Panel", "Prompt", "Settings", "Store", "System", "Manager", "Monitor", "Editor", "Browser",
        "Mail", "Maps", "Music", "Movies", "Network", "Office", "Onenote", "Outlook", "People",
    ];

    private static readonly string[] Qualifiers =
    [
        string.Empty, "Pro", "2022", "3D", "Preview", "X", "Lite", "Plus", "Home", "Enterprise",
        "for Windows", "Insider", "Legacy", "New", "Classic",
    ];

    private static readonly string[] SubtitleWords =
    [
        "Perform calculations and conversions", "View and manage your schedule", "Edit and refine images",
        "Modern terminal for command-line tools", "Full-featured integrated development environment",
        "Browse the web quickly and securely", "Adjust your computer settings", "Monitor apps and processes",
        "A simple and fast text editor", "Play and organize your media library",
    ];

    private static IPrecomputedFuzzyMatcher CreateMatcher() => new PrecomputedFuzzyMatcher(new PrecomputedFuzzyMatcherOptions());

    private static CatalogItem[] BuildCatalog(int count, string idPrefix)
    {
        var items = new CatalogItem[count];
        for (var i = 0; i < count; i++)
        {
            var noun = Nouns[i % Nouns.Length];
            var qualifier = Qualifiers[(i / Nouns.Length) % Qualifiers.Length];
            var title = string.IsNullOrEmpty(qualifier) ? noun : $"{noun} {qualifier}";

            if (i >= Nouns.Length * Qualifiers.Length)
            {
                title = $"{title} {i}";
            }

            var subtitle = SubtitleWords[i % SubtitleWords.Length];
            items[i] = new CatalogItem(title, subtitle, $"{idPrefix}.{i}");
        }

        return items;
    }

    private static RecentCommandsManager SeedHistory(CatalogItem[] apps, int seedCount)
    {
        var history = new RecentCommandsManager();
        var n = Math.Min(seedCount, apps.Length);
        for (var i = 0; i < n; i++)
        {
            var idx = (i * 7) % apps.Length;
            history = history.WithHistoryItem(apps[idx].Command.Id);
        }

        return history;
    }

    private static ScoringFunction<IListItem> BuildScoringFunction(
        IRecentCommandsManager history,
        IPrecomputedFuzzyMatcher matcher)
        => (in FuzzyQuery query, IListItem item) =>
            MainListPage.ScoreTopLevelItem(query, item, history, matcher, null);

    private static void AssertOrderedResultsIdentical(
        string context,
        RoScored<IListItem>[] reference,
        RoScored<IListItem>[] candidate)
    {
        Assert.AreEqual(reference.Length, candidate.Length, $"[{context}] result count must match the sequential reference.");

        for (var i = 0; i < reference.Length; i++)
        {
            // Same packed score at the same index.
            Assert.AreEqual(
                reference[i].Score,
                candidate[i].Score,
                $"[{context}] score at index {i} must match the sequential reference.");

            // Same item reference at the same index - proves the ORDER is byte-identical, including
            // tie-breaks, not merely that the same set of scores appears.
            Assert.AreSame(
                reference[i].Item,
                candidate[i].Item,
                $"[{context}] item at index {i} must be the exact same instance as the sequential reference.");
        }
    }

    /// <summary>
    /// Full-catalog scoring: for every query the parallel apps pass must produce a result
    /// byte-identical in order and score to the sequential reference. This is the rebuild/retype
    /// path (a fresh query scored against the whole catalog).
    /// </summary>
    [TestMethod]
    public void ParallelScoring_FullCatalog_MatchesSequentialForEveryQuery()
    {
        var apps = BuildCatalog(AppCount, "app");
        var matcher = CreateMatcher();
        var history = SeedHistory(apps, HistorySeedCount);
        var scoringFn = BuildScoringFunction(history, matcher);
        var source = apps.Cast<IListItem>().ToArray();

        // Mirror the product: build the frecency index once before the parallel pass reads it.
        history.PrewarmIndex();

        foreach (var raw in Queries)
        {
            var query = matcher.PrecomputeQuery(raw);

            var sequential = InternalListHelpers.FilterListWithScores(source, query, scoringFn);
            var parallel = InternalListHelpers.FilterListWithScoresParallel(source, query, scoringFn);

            TestContext.WriteLine($"query '{raw}': {sequential.Length} matches (sequential) vs {parallel.Length} (parallel).");
            AssertOrderedResultsIdentical($"full '{raw}'", sequential, parallel);
        }
    }

    /// <summary>
    /// Extend (incremental narrowing) path: score the whole catalog for the 1-char query, retain
    /// the matched subset in its produced order, then re-score that subset for the extending query.
    /// The parallel scorer over the retained subset must still match the sequential reference.
    /// </summary>
    [TestMethod]
    public void ParallelScoring_ExtendPath_MatchesSequentialOverRetainedSubset()
    {
        var apps = BuildCatalog(AppCount, "app");
        var matcher = CreateMatcher();
        var history = SeedHistory(apps, HistorySeedCount);
        var scoringFn = BuildScoringFunction(history, matcher);
        var source = apps.Cast<IListItem>().ToArray();

        history.PrewarmIndex();

        // ("c" -> "ca" -> "cal" -> "calc") extend chain, each step narrowing the previous result.
        var chain = new[] { "c", "ca", "cal", "calc" };

        var retained = source;
        for (var step = 1; step < chain.Length; step++)
        {
            // The retained subset is exactly what the product feeds the next keystroke: the items
            // of the previous result, in the previous result's order.
            var prevQuery = matcher.PrecomputeQuery(chain[step - 1]);
            var prev = InternalListHelpers.FilterListWithScores(retained, prevQuery, scoringFn);
            retained = prev.Select(s => s.Item).ToArray();

            var query = matcher.PrecomputeQuery(chain[step]);
            var sequential = InternalListHelpers.FilterListWithScores(retained, query, scoringFn);
            var parallel = InternalListHelpers.FilterListWithScoresParallel(retained, query, scoringFn);

            TestContext.WriteLine($"extend '{chain[step - 1]}' -> '{chain[step]}': retained {retained.Length}, kept {sequential.Length}.");
            AssertOrderedResultsIdentical($"extend '{chain[step - 1]}'->'{chain[step]}'", sequential, parallel);

            Assert.IsTrue(sequential.Length <= retained.Length, "Extending a query cannot add matches beyond the retained set.");
        }
    }

    /// <summary>
    /// Shrink/retype rebuild path: a query that does NOT extend the previous one forces a full
    /// rebuild against the whole catalog. Verify the parallel scorer matches the sequential
    /// reference for a sequence of unrelated queries scored fresh each time.
    /// </summary>
    [TestMethod]
    public void ParallelScoring_RetypeRebuild_MatchesSequential()
    {
        var apps = BuildCatalog(AppCount, "app");
        var matcher = CreateMatcher();
        var history = SeedHistory(apps, HistorySeedCount);
        var scoringFn = BuildScoringFunction(history, matcher);
        var source = apps.Cast<IListItem>().ToArray();

        history.PrewarmIndex();

        // Unrelated queries (each a fresh rebuild, never an extend of the last).
        foreach (var raw in new[] { "calc", "term", "vs code", "settings", "e" })
        {
            var query = matcher.PrecomputeQuery(raw);
            var sequential = InternalListHelpers.FilterListWithScores(source, query, scoringFn);
            var parallel = InternalListHelpers.FilterListWithScoresParallel(source, query, scoringFn);

            AssertOrderedResultsIdentical($"retype '{raw}'", sequential, parallel);
        }
    }

    /// <summary>
    /// Commands (hundreds) stay on the serial path but the helper is still exercised over that
    /// smaller catalog for completeness: the parallel entry point must fall back to - and match -
    /// the sequential result even below its parallel threshold.
    /// </summary>
    [TestMethod]
    public void ParallelScoring_Commands_MatchesSequential()
    {
        var commands = BuildCatalog(CommandCount, "cmd");
        var matcher = CreateMatcher();
        var history = SeedHistory(commands, HistorySeedCount);
        var scoringFn = BuildScoringFunction(history, matcher);
        var source = commands.Cast<IListItem>().ToArray();

        history.PrewarmIndex();

        foreach (var raw in Queries)
        {
            var query = matcher.PrecomputeQuery(raw);
            var sequential = InternalListHelpers.FilterListWithScores(source, query, scoringFn);
            var parallel = InternalListHelpers.FilterListWithScoresParallel(source, query, scoringFn);

            AssertOrderedResultsIdentical($"commands '{raw}'", sequential, parallel);
        }
    }

    /// <summary>
    /// Determinism: repeatedly running the parallel scorer over the same catalog and query yields
    /// the exact same ordered result each time, regardless of thread scheduling.
    /// </summary>
    [TestMethod]
    public void ParallelScoring_IsDeterministicAcrossRuns()
    {
        var apps = BuildCatalog(AppCount, "app");
        var matcher = CreateMatcher();
        var history = SeedHistory(apps, HistorySeedCount);
        var scoringFn = BuildScoringFunction(history, matcher);
        var source = apps.Cast<IListItem>().ToArray();

        history.PrewarmIndex();

        var query = matcher.PrecomputeQuery("c");
        var first = InternalListHelpers.FilterListWithScoresParallel(source, query, scoringFn);

        for (var run = 0; run < 8; run++)
        {
            var again = InternalListHelpers.FilterListWithScoresParallel(source, query, scoringFn);
            AssertOrderedResultsIdentical($"determinism run {run}", first, again);
        }
    }
}
