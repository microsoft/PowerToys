// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.UI.ViewModels.Commands;
using Microsoft.CmdPal.UI.ViewModels.MainPage;
using Microsoft.CommandPalette.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Microsoft.CmdPal.UI.ViewModels.UnitTests.ScoringTestCatalog;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

/// <summary>
/// Guardrail for the throughput work: moving scoring off the TopLevelCommands lock and
/// parallelizing the apps pass may change how fast and where the settled list is computed, never
/// what it is. Across a synthetic catalog and several queries (the 1-char pathological case, the
/// extend chain, and the retype rebuild) the parallel scorer has to match the sequential one item
/// for item and score for score.
/// </summary>
[TestClass]
public sealed partial class ScoringParallelEquivalenceTests
{
    // Big enough that the parallel path is actually taken across multiple partitions, small enough
    // to stay fast on CI.
    private const int AppCount = 4000;
    private const int CommandCount = 300;
    private const int HistorySeedCount = 250;

    // "c" is the pathological 1-char case, the "ca"/"cal"/"calc" chain is the extend path, and the
    // acronym and multi-word cases stress the tier classifier.
    private static readonly string[] Queries =
        ["c", "ca", "cal", "calc", "vs", "vsc", "vs code", "term", "set", "e"];

    public TestContext TestContext { get; set; } = null!;

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

            // Same item reference at the same index, which proves the order matches including
            // tie-breaks, not just that the same scores turn up.
            Assert.AreSame(
                reference[i].Item,
                candidate[i].Item,
                $"[{context}] item at index {i} must be the exact same instance as the sequential reference.");
        }
    }

    /// <summary>
    /// The rebuild path: a fresh query scored against the whole catalog, where the parallel apps
    /// pass has to match the sequential reference exactly.
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
    /// The extend path: score the whole catalog for a 1-char query, keep the matched subset in the
    /// order it came back, then re-score that subset for the extending query.
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

        // Each step narrows the previous result.
        var chain = new[] { "c", "ca", "cal", "calc" };

        var retained = source;
        for (var step = 1; step < chain.Length; step++)
        {
            // This is exactly what the product feeds the next keystroke: the previous result's
            // items, in the previous result's order.
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
    /// The retype path: a query that doesn't extend the previous one forces a full rebuild, so
    /// check a run of unrelated queries scored fresh each time.
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
    /// Commands (hundreds) stay serial, so the parallel entry point has to fall back below its
    /// threshold and still match the sequential result.
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
    /// Running the parallel scorer over the same catalog and query repeatedly gives the same
    /// ordered result every time, whatever the thread scheduling does.
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

    /// <summary>
    /// The hot path snapshots the frecency manager, matcher, settings and one evaluation time per
    /// query, and feeds the apps pass a single constant provider weight. This proves that captured
    /// context lands on the same ordered result as the old per-item live reads.
    /// </summary>
    [TestMethod]
    public void CapturedContext_ConstantWeightAndFixedNow_MatchesPerItemLiveRead()
    {
        var apps = BuildCatalog(AppCount, "app");
        var matcher = CreateMatcher();
        var history = SeedHistory(apps, HistorySeedCount);
        var source = apps.Cast<IListItem>().ToArray();

        history.PrewarmIndex();

        // A non-default weight, so the value has to actually flow through to the packed score.
        const ProviderSearchWeight weight = ProviderSearchWeight.Higher;
        Func<IListItem, ProviderSearchWeight> perItemLookup = _ => weight;
        Func<IListItem, ProviderSearchWeight> constantLookup = _ => weight;

        // Captured once before the loop, exactly as the product captures scoringNow. The reference
        // path below omits it, so it reads the current time per call.
        var capturedNow = DateTimeOffset.UtcNow;

        ScoringFunction<IListItem> liveReadScorer = (in FuzzyQuery query, IListItem item) =>
            MainListPage.ScoreTopLevelItem(query, item, history, matcher, perItemLookup);
        ScoringFunction<IListItem> capturedContextScorer = (in FuzzyQuery query, IListItem item) =>
            MainListPage.ScoreTopLevelItem(query, item, history, matcher, constantLookup, capturedNow);

        foreach (var raw in Queries)
        {
            var query = matcher.PrecomputeQuery(raw);

            var reference = InternalListHelpers.FilterListWithScores(source, query, liveReadScorer);
            var candidate = InternalListHelpers.FilterListWithScoresParallel(source, query, capturedContextScorer);

            TestContext.WriteLine($"captured-context '{raw}': {reference.Length} matches (per-item live read) vs {candidate.Length} (constant weight + fixed now).");
            AssertOrderedResultsIdentical($"captured '{raw}'", reference, candidate);
        }
    }
}
