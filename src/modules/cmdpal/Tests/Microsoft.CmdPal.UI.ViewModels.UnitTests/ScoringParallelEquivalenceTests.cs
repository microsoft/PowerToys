// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.Ext.Apps;
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
        IPrecomputedFuzzyMatcher matcher,
        string query)
    {
        var search = new AppSearch(query, matcher);
        return (in FuzzyQuery q, IListItem item) => MainListPage.ScoreTopLevelItem(q, item, history, matcher, appSearch: search);
    }

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
        var apps = BuildAppCatalog(AppCount);
        var matcher = CreateMatcher();
        var history = SeedHistory(apps, HistorySeedCount);
        var source = apps.Cast<IListItem>().ToArray();

        // Mirror the product: build the frecency index once before the parallel pass reads it.
        history.PrewarmIndex();

        foreach (var raw in Queries)
        {
            var query = matcher.PrecomputeQuery(raw);
            var scoringFn = BuildScoringFunction(history, matcher, raw);

            var sequential = InternalListHelpers.FilterListWithScores(source, query, scoringFn);
            var parallel = InternalListHelpers.FilterListWithScoresParallel(source, query, scoringFn);

            TestContext.WriteLine($"query '{raw}': {sequential.Length} matches (sequential) vs {parallel.Length} (parallel).");
            AssertOrderedResultsIdentical($"full '{raw}'", sequential, parallel);
        }
    }

    [TestMethod]
    [DataRow(16)]
    [DataRow(1024)]
    public void ParallelScoring_UsesCustomTieBreakOnBothPaths(int count)
    {
        var source = BuildAppCatalog(count).Cast<IListItem>().Reverse().ToArray();
        var query = CreateMatcher().PrecomputeQuery("cmd.exe");
        ScoringFunction<IListItem> scorer = (in FuzzyQuery _, IListItem _) => 1;
        var comparer = Comparer<RoScored<IListItem>>.Create(static (left, right) => StringComparer.Ordinal.Compare(left.Item.Title, right.Item.Title));
        var sequential = InternalListHelpers.FilterListWithScores(source, query, scorer, comparer);
        var parallel = InternalListHelpers.FilterListWithScoresParallel(source, query, scorer, comparer);

        AssertOrderedResultsIdentical("custom comparer", sequential, parallel);
        CollectionAssert.AreEqual(source.OrderBy(item => item.Title, StringComparer.Ordinal).ToArray(), parallel.Select(result => result.Item).ToArray());
    }

    /// <summary>Query growth still scores all apps because metadata admission can gain matches.</summary>
    [TestMethod]
    public void ParallelScoring_QueryGrowth_MatchesSequentialOverFullCatalog()
    {
        var apps = BuildAppCatalog(AppCount);
        var matcher = CreateMatcher();
        var history = SeedHistory(apps, HistorySeedCount);
        var source = apps.Cast<IListItem>().ToArray();
        history.PrewarmIndex();

        foreach (var raw in new[] { "c", "ca", "cal", "calc", "alias", "alias42" })
        {
            var query = matcher.PrecomputeQuery(raw);
            var scoringFn = BuildScoringFunction(history, matcher, raw);
            var sequential = InternalListHelpers.FilterListWithScores(source, query, scoringFn);
            var parallel = InternalListHelpers.FilterListWithScoresParallel(source, query, scoringFn);

            TestContext.WriteLine($"query growth '{raw}': reconsidered {source.Length}, kept {sequential.Length}.");
            AssertOrderedResultsIdentical($"growth '{raw}'", sequential, parallel);
        }
    }

    /// <summary>
    /// The retype path: a query that doesn't extend the previous one forces a full rebuild, so
    /// check a run of unrelated queries scored fresh each time.
    /// </summary>
    [TestMethod]
    public void ParallelScoring_RetypeRebuild_MatchesSequential()
    {
        var apps = BuildAppCatalog(AppCount);
        var matcher = CreateMatcher();
        var history = SeedHistory(apps, HistorySeedCount);
        var source = apps.Cast<IListItem>().ToArray();

        history.PrewarmIndex();

        // Unrelated queries (each a fresh rebuild, never an extend of the last).
        foreach (var raw in new[] { "calc", "term", "vs code", "settings", "e" })
        {
            var query = matcher.PrecomputeQuery(raw);
            var scoringFn = BuildScoringFunction(history, matcher, raw);
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
        var source = commands.Cast<IListItem>().ToArray();

        history.PrewarmIndex();

        foreach (var raw in Queries)
        {
            var query = matcher.PrecomputeQuery(raw);
            var scoringFn = BuildScoringFunction(history, matcher, raw);
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
        var apps = BuildAppCatalog(AppCount);
        var matcher = CreateMatcher();
        var history = SeedHistory(apps, HistorySeedCount);
        var source = apps.Cast<IListItem>().ToArray();

        history.PrewarmIndex();

        var query = matcher.PrecomputeQuery("c");
        var scoringFn = BuildScoringFunction(history, matcher, query.Original);
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
        var apps = BuildAppCatalog(AppCount);
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

        foreach (var raw in Queries)
        {
            var query = matcher.PrecomputeQuery(raw);
            var search = new AppSearch(raw, matcher);
            ScoringFunction<IListItem> liveReadScorer = (in FuzzyQuery q, IListItem item) =>
                MainListPage.ScoreTopLevelItem(q, item, history, matcher, perItemLookup, appSearch: search);
            ScoringFunction<IListItem> capturedContextScorer = (in FuzzyQuery q, IListItem item) =>
                MainListPage.ScoreTopLevelItem(q, item, history, matcher, constantLookup, capturedNow, search);

            var reference = InternalListHelpers.FilterListWithScores(source, query, liveReadScorer);
            var candidate = InternalListHelpers.FilterListWithScoresParallel(source, query, capturedContextScorer);

            TestContext.WriteLine($"captured-context '{raw}': {reference.Length} matches (per-item live read) vs {candidate.Length} (constant weight + fixed now).");
            AssertOrderedResultsIdentical($"captured '{raw}'", reference, candidate);
        }
    }
}
