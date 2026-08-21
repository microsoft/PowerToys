// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.Ext.UnitTestBase;
using Microsoft.CmdPal.UI.ViewModels.MainPage;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;
using WyHash;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public partial class RecentCommandsTests : CommandPaletteUnitTestBase
{
    private static RecentCommandsManager CreateHistory(IList<string>? commandIds = null)
    {
        var history = new RecentCommandsManager();
        if (commandIds != null)
        {
            foreach (var item in commandIds)
            {
                history = history.WithHistoryItem(item);
            }
        }

        return history;
    }

    [TestMethod]
    public void ValidateHistoryFunctionality()
    {
        // Setup
        var history = CreateHistory();

        // Act
        history = history.WithHistoryItem("com.microsoft.cmdpal.shell");

        // Assert
        Assert.IsTrue(history.GetCommandHistoryWeight("com.microsoft.cmdpal.shell") > 0);
    }

    [TestMethod]
    public void ValidateHistoryWeighting()
    {
        // Build history with explicit, strictly-increasing timestamps so time-decay is
        // deterministic. "shell" is used twice (most uses) and most recently; the others are
        // each used once, progressively more recently.
        var t0 = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var history = new RecentCommandsManager();
        history = history.WithHistoryItem("com.microsoft.cmdpal.shell", t0);
        history = history.WithHistoryItem("com.microsoft.cmdpal.windowwalker", t0.AddDays(1));
        history = history.WithHistoryItem("Visual Studio 2022 Preview_6533433915015224980", t0.AddDays(2));
        history = history.WithHistoryItem("com.microsoft.cmdpal.reload", t0.AddDays(3));
        history = history.WithHistoryItem("com.microsoft.cmdpal.shell", t0.AddDays(4));

        var now = t0.AddDays(4);

        // Act
        var shellWeight = history.GetCommandHistoryWeight("com.microsoft.cmdpal.shell", now);
        var windowWalkerWeight = history.GetCommandHistoryWeight("com.microsoft.cmdpal.windowwalker", now);
        var vsWeight = history.GetCommandHistoryWeight("Visual Studio 2022 Preview_6533433915015224980", now);
        var reloadWeight = history.GetCommandHistoryWeight("com.microsoft.cmdpal.reload", now);
        var nonExistentWeight = history.GetCommandHistoryWeight("non.existent.command", now);

        // Assert
        Assert.IsTrue(shellWeight > windowWalkerWeight, "Shell is both the most-used and most-recent command");
        Assert.IsTrue(vsWeight > windowWalkerWeight, "Visual Studio was used more recently than Window Walker");
        Assert.IsTrue(reloadWeight > vsWeight, "Reload was used more recently than Visual Studio");
        Assert.IsTrue(shellWeight > vsWeight, "Shell is both more recent and more frequently used than Visual Studio");
        Assert.AreEqual(0, nonExistentWeight, "Nonexistent command should have zero weight");
    }

    private sealed partial record ListItemMock(
        string Title,
        string? Subtitle = "",
        string? GivenId = "",
        string? ProviderId = "") : IListItem
    {
        public string Id => string.IsNullOrEmpty(GivenId) ? GenerateId() : GivenId;

        public IDetails Details => throw new System.NotImplementedException();

        public string Section => throw new System.NotImplementedException();

        public ITag[] Tags => throw new System.NotImplementedException();

        public string TextToSuggest => throw new System.NotImplementedException();

        public ICommand Command => new NoOpCommand() { Id = Id };

        public IIconInfo Icon => throw new System.NotImplementedException();

        public IContextItem[] MoreCommands => throw new System.NotImplementedException();

#pragma warning disable CS0067
        public event TypedEventHandler<object, IPropChangedEventArgs>? PropChanged;
#pragma warning restore CS0067

        private string GenerateId()
        {
            // Use WyHash64 to generate stable ID hashes.
            // manually seeding with 0, so that the hash is stable across launches
            var result = WyHash64.ComputeHash64(ProviderId + Title + Subtitle, seed: 0);
            return $"{ProviderId}{result}";
        }
    }

    private static RecentCommandsManager CreateHistory(IList<ListItemMock> items)
    {
        var history = new RecentCommandsManager();
        foreach (var item in items)
        {
            history = history.WithHistoryItem(item.Id);
        }

        return history;
    }

    [TestMethod]
    public void ValidateMocksWork()
    {
        // Setup
        var items = new List<ListItemMock>
        {
            new("Command A", "Subtitle A", "idA", "providerA"),
            new("Command B", "Subtitle B", GivenId: "idB"),
            new("Command C", "Subtitle C", ProviderId: "providerC"),
            new("Command A", "Subtitle A", "idA", "providerA"), // Duplicate to test incrementing uses
        };

        // Act
        var history = CreateHistory(items);

        // Assert
        foreach (var item in items)
        {
            var weight = history.GetCommandHistoryWeight(item.Id);
            Assert.IsTrue(weight > 0, $"Item {item.Title} should have a weight greater than zero.");
        }

        // Check that the duplicate item has a higher weight due to increased uses
        var weightA = history.GetCommandHistoryWeight("idA");
        var weightB = history.GetCommandHistoryWeight("idB");
        var weightC = history.GetCommandHistoryWeight(items[2].Id); // providerC generated ID
        Assert.IsTrue(weightA > weightB, "Item A should have a higher weight than Item B due to more uses.");
        Assert.IsTrue(weightA > weightC, "Item A should have a higher weight than Item C due to more uses.");
        Assert.AreEqual(weightC, weightB, "Item C and Item B were used in the last 3 commands");
    }

    [TestMethod]
    public void ValidateRecencyDecay()
    {
        // Each command is used exactly once, at progressively older times. Weight must decay
        // monotonically with age, and a 3-day-old use (one half-life) should weigh about half
        // of a use that just happened.
        var now = new DateTimeOffset(2025, 2, 1, 0, 0, 0, TimeSpan.Zero);
        var history = new RecentCommandsManager()
            .WithHistoryItem("today", now)
            .WithHistoryItem("three-days", now.AddDays(-3))
            .WithHistoryItem("ten-days", now.AddDays(-10))
            .WithHistoryItem("thirty-days", now.AddDays(-30));

        var today = history.GetCommandHistoryWeight("today", now);
        var threeDays = history.GetCommandHistoryWeight("three-days", now);
        var tenDays = history.GetCommandHistoryWeight("ten-days", now);
        var thirtyDays = history.GetCommandHistoryWeight("thirty-days", now);

        Assert.IsTrue(today > threeDays, "A more recent use must weigh more than an older one");
        Assert.IsTrue(threeDays > tenDays, "Decay must be monotonic with age");
        Assert.IsTrue(tenDays > thirtyDays, "Older uses keep decaying toward zero");
        Assert.IsTrue(thirtyDays >= 0, "Weight must never go negative");

        // The half-life is 3 days, so a 3-day-old single use is about half of a fresh one.
        Assert.AreEqual(today / 2.0, threeDays, 1.0, "Three days should be a single half-life");
    }

    [TestMethod]
    public void ValidateFrequencyWeighting()
    {
        // Hold recency constant (same timestamp) and vary only the use count. More uses must
        // weigh more, but the log-scaled frequency term stays within the documented cap.
        var now = new DateTimeOffset(2025, 2, 1, 0, 0, 0, TimeSpan.Zero);
        var history = new RecentCommandsManager().WithHistoryItem("once", now);
        for (var i = 0; i < 7; i++)
        {
            history = history.WithHistoryItem("many", now);
        }

        var once = history.GetCommandHistoryWeight("once", now);
        var many = history.GetCommandHistoryWeight("many", now);

        Assert.IsTrue(many > once, "At equal recency, a more frequently used command weighs more");
        Assert.IsTrue(many <= RecentCommandsManager.MaxWeight, "Weight stays within the documented cap");
    }

    [TestMethod]
    public void ValidateLookupIndexStaysConsistentAcrossChanges()
    {
        // The commandId lookup is backed by a cached index that must be invalidated whenever
        // history changes (WithHistoryItem returns a new record, and a 'with' copy carries the
        // old cached field over). Force the index to build on one instance, then mutate, and
        // confirm each instance reports exactly its own history.
        var now = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var first = new RecentCommandsManager().WithHistoryItem("alpha", now);

        // Read once to build the cached index on 'first'.
        var alphaFirst = first.GetCommandHistoryWeight("alpha", now);
        Assert.IsTrue(alphaFirst > 0, "alpha should have weight on the first instance");
        Assert.AreEqual(0, first.GetCommandHistoryWeight("beta", now), "beta is not in the first instance");

        // Add a use of alpha and a brand-new beta, producing a new instance.
        var second = first
            .WithHistoryItem("alpha", now.AddDays(1))
            .WithHistoryItem("beta", now.AddDays(1));

        // The new instance sees the newer alpha (more recent + higher use) and the new beta.
        Assert.IsTrue(
            second.GetCommandHistoryWeight("alpha", now.AddDays(1)) > 0,
            "alpha should still resolve on the updated instance");
        Assert.IsTrue(
            second.GetCommandHistoryWeight("beta", now.AddDays(1)) > 0,
            "beta should resolve on the updated instance after its index rebuilds");

        // The original instance is unchanged - its index never gained beta.
        Assert.AreEqual(0, first.GetCommandHistoryWeight("beta", now), "the original instance must not see beta");
    }

    [TestMethod]
    public void ValidateHistoryCap()
    {
        // Insert well beyond the cap, each newer than the last. The store keeps the most-recent
        // entries and evicts the oldest, replacing the previous 50-entry limit.
        var now = new DateTimeOffset(2025, 2, 1, 0, 0, 0, TimeSpan.Zero);
        var history = new RecentCommandsManager();

        var total = RecentCommandsManager.MaxHistoryEntries + 50;
        for (var i = 0; i < total; i++)
        {
            history = history.WithHistoryItem($"cmd-{i}", now.AddMinutes(i));
        }

        var evaluatedAt = now.AddMinutes(total);

        Assert.IsTrue(
            history.History.Count <= RecentCommandsManager.MaxHistoryEntries,
            "History should be capped at MaxHistoryEntries");

        // The earliest (now-evicted) entries have fallen out of the store.
        Assert.AreEqual(0, history.GetCommandHistoryWeight("cmd-0", evaluatedAt), "The oldest entry should have been evicted");

        // The most-recent entries survive and still carry weight.
        Assert.IsTrue(
            history.GetCommandHistoryWeight($"cmd-{total - 1}", evaluatedAt) > 0,
            "The most recent entry should be retained");
    }

    [TestMethod]
    public void ValidateLegacyHistoryMigration()
    {
        // Legacy items persisted before LastUsed existed deserialize with a default timestamp.
        // They should be mildly backdated so ordering falls back to Uses (frequency) rather
        // than collapsing to all-equal or zero.
        var now = new DateTimeOffset(2025, 5, 1, 0, 0, 0, TimeSpan.Zero);
        var legacy = new RecentCommandsManager
        {
            History = ImmutableList.Create(
                new HistoryItem { CommandId = "rare", Uses = 1 },
                new HistoryItem { CommandId = "common", Uses = 8 }),
        };

        var rare = legacy.GetCommandHistoryWeight("rare", now);
        var common = legacy.GetCommandHistoryWeight("common", now);

        Assert.IsTrue(rare > 0, "Legacy items should be mildly backdated, not zeroed out");
        Assert.IsTrue(common > rare, "Legacy ordering should fall back to Uses (frequency)");

        // A brand-new, single real use should outrank a backdated single-use legacy item.
        var withFresh = legacy.WithHistoryItem("brand-new", now);
        var fresh = withFresh.GetCommandHistoryWeight("brand-new", now);
        Assert.IsTrue(fresh > rare, "A just-used command should outrank a backdated single-use legacy item");
    }

    [TestMethod]
    public void ValidateHistorySerializationRoundTrips()
    {
        // The persisted history (see SettingsModel's JsonSerializable context) must round-trip,
        // including the new LastUsed timestamp, so decay survives a save/load cycle.
        var now = new DateTimeOffset(2025, 3, 15, 12, 30, 0, TimeSpan.Zero);
        var original = new RecentCommandsManager()
            .WithHistoryItem("alpha", now)
            .WithHistoryItem("beta", now.AddMinutes(5));

        var json = JsonSerializer.Serialize(original, JsonSerializationContext.Default.RecentCommandsManager);
        var restored = JsonSerializer.Deserialize(json, JsonSerializationContext.Default.RecentCommandsManager);

        Assert.IsNotNull(restored, "Round-tripped history should not be null");
        Assert.AreEqual(2, restored!.History.Count, "All history entries should round-trip");

        var beta = restored.History.First(h => h.CommandId == "beta");
        Assert.AreEqual(now.AddMinutes(5), beta.LastUsed, "The LastUsed timestamp should round-trip");
        Assert.AreEqual(1, beta.Uses, "The use count should round-trip");

        // Weights computed from the restored state should match the original.
        Assert.AreEqual(
            original.GetCommandHistoryWeight("beta", now.AddMinutes(5)),
            restored.GetCommandHistoryWeight("beta", now.AddMinutes(5)),
            "Restored history should produce the same weight as the original");
    }

    [TestMethod]
    public void ValidateSimpleScoring()
    {
        // Setup
        var items = new List<ListItemMock>
        {
            new("Command A", "Subtitle A", GivenId: "idA"), // #0  -> bucket 0
            new("Command B", "Subtitle B", GivenId: "idB"), // #1  -> bucket 0
            new("Command C", "Subtitle C", GivenId: "idC"), // #2  -> bucket 0
        };

        var history = CreateHistory(items.Reverse<ListItemMock>().ToList());
        var fuzzyMatcher = CreateMatcher();
        var q = fuzzyMatcher.PrecomputeQuery("C");

        var scoreA = MainListPage.ScoreTopLevelItem(q, items[0], history, fuzzyMatcher);
        var scoreB = MainListPage.ScoreTopLevelItem(q, items[1], history, fuzzyMatcher);
        var scoreC = MainListPage.ScoreTopLevelItem(q, items[2], history, fuzzyMatcher);

        // Assert
        // All of these equally match the query, and they're all in the same bucket,
        // so they should all have the same score.
        Assert.AreEqual(scoreA, scoreB, "Items A and B should have the same score");
        Assert.AreEqual(scoreB, scoreC, "Items B and C should have the same score");
    }

    private static List<ListItemMock> CreateMockHistoryItems()
    {
        var items = new List<ListItemMock>
        {
            new("Visual Studio 2022"), // #0  -> bucket 0
            new("Visual Studio Code"), // #1  -> bucket 0
            new("Explore Mastodon", GivenId: "social.mastodon.explore"), // #2  -> bucket 0
            new("Run commands", Subtitle: "Executes commands (e.g. ping, cmd)", GivenId: "com.microsoft.cmdpal.run"), // #3  -> bucket 1
            new("Windows Settings"), // #4  -> bucket 1
            new("Command Prompt"), // #5  -> bucket 1
            new("Terminal Canary"), // #6  -> bucket 1
        };
        return items;
    }

    private static RecentCommandsManager CreateMockHistoryService(List<ListItemMock>? items = null)
    {
        var history = CreateHistory((items ?? CreateMockHistoryItems()).Reverse<ListItemMock>().ToList());
        return history;
    }

    private static IPrecomputedFuzzyMatcher CreateMatcher()
    {
        return new PrecomputedFuzzyMatcher(new PrecomputedFuzzyMatcherOptions());
    }

    private sealed record ScoredItem(ListItemMock Item, int Score)
    {
        public string Title => Item.Title;

        public override string ToString() => $"[{Score}]{Title}";
    }

    private static IEnumerable<ScoredItem> TieScoresToMatches(List<ListItemMock> items, List<int> scores)
    {
        if (items.Count != scores.Count)
        {
            throw new ArgumentException("Items and scores must have the same number of elements");
        }

        for (var i = 0; i < items.Count; i++)
        {
            yield return new ScoredItem(items[i], scores[i]);
        }
    }

    private static IEnumerable<ScoredItem> GetMatches(IEnumerable<ScoredItem> scoredItems)
    {
        var matches = scoredItems
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ToList();

        return matches;
    }

    private static IEnumerable<ScoredItem> GetMatches(List<ListItemMock> items, List<int> scores)
    {
        return GetMatches(TieScoresToMatches(items, scores));
    }

    [TestMethod]
    public void ValidateScoredWeightingSimple()
    {
        var items = CreateMockHistoryItems();
        var emptyHistory = CreateMockHistoryService(new());
        var history = CreateMockHistoryService(items);
        var fuzzyMatcher = CreateMatcher();

        var q = fuzzyMatcher.PrecomputeQuery("C");
        var unweightedScores = items.Select(item => MainListPage.ScoreTopLevelItem(q, item, emptyHistory, fuzzyMatcher)).ToList();
        var weightedScores = items.Select(item => MainListPage.ScoreTopLevelItem(q, item, history, fuzzyMatcher)).ToList();
        Assert.AreEqual(unweightedScores.Count, weightedScores.Count, "Both score lists should have the same number of items");
        for (var i = 0; i < unweightedScores.Count; i++)
        {
            var unweighted = unweightedScores[i];
            var weighted = weightedScores[i];
            var item = items[i];
            if (item.Title.Contains('C', System.StringComparison.CurrentCultureIgnoreCase))
            {
                Assert.IsTrue(unweighted >= 0, $"Item {item.Title} didn't match the query, so should have a weighted score of zero");
                Assert.IsTrue(weighted > unweighted, $"Item {item.Title} should have a higher weighted ({weighted}) score than unweighted ({unweighted})");
            }
            else
            {
                Assert.AreEqual(unweighted, 0, $"Item {item.Title} didn't match the query, so should have a weighted score of zero");
                Assert.AreEqual(unweighted, weighted);
            }
        }

        var unweightedMatches = GetMatches(items, unweightedScores).ToList();
        Assert.AreEqual(4, unweightedMatches.Count);
        Assert.AreEqual("Command Prompt", unweightedMatches[0].Title, "Command Prompt should be the top match");
        Assert.AreEqual("Visual Studio Code", unweightedMatches[1].Title, "Visual Studio Code should be the second match");
        Assert.AreEqual("Terminal Canary", unweightedMatches[2].Title);
        Assert.AreEqual("Run commands", unweightedMatches[3].Title);

        // Even after weighting for 1 use, Command Prompt should still be the top match.
        var weightedMatches = GetMatches(items, weightedScores).ToList();
        Assert.AreEqual(4, weightedMatches.Count);
        Assert.AreEqual("Command Prompt", weightedMatches[0].Title);
        Assert.AreEqual("Visual Studio Code", weightedMatches[1].Title);
        Assert.AreEqual("Terminal Canary", weightedMatches[2].Title);
        Assert.AreEqual("Run commands", weightedMatches[3].Title);
    }

    [TestMethod]
    public void ValidateTitlesAreMoreImportantThanHistory()
    {
        var items = CreateMockHistoryItems();
        var emptyHistory = CreateMockHistoryService(new());
        var history = CreateMockHistoryService(items);
        var fuzzyMatcher = CreateMatcher();
        var q = fuzzyMatcher.PrecomputeQuery("te");

        var weightedScores = items.Select(item => MainListPage.ScoreTopLevelItem(q, item, history, fuzzyMatcher)).ToList();
        var weightedMatches = GetMatches(items, weightedScores).ToList();

        Assert.AreEqual(3, weightedMatches.Count, "Find Terminal, VsCode and Run commands");

        // Terminal is in bucket 1, VS Code is in bucket 0, but Terminal matches
        // the title better
        Assert.AreEqual("Terminal Canary", weightedMatches[0].Title, "Terminal should be the top match, title match");
        Assert.AreEqual("Visual Studio Code", weightedMatches[1].Title, "VsCode does fuzzy match, but is less relevant than Terminal");
        Assert.AreEqual("Run commands", weightedMatches[2].Title, "run only matches on the subtitle");
    }

    [TestMethod]
    public void ValidateTitlesAreMoreImportantThanUsage()
    {
        var items = CreateMockHistoryItems();
        var emptyHistory = CreateMockHistoryService(new());
        var history = CreateMockHistoryService(items);
        var fuzzyMatcher = CreateMatcher();
        var q = fuzzyMatcher.PrecomputeQuery("te");

        // Add extra uses of VS Code to try and push it above Terminal
        for (var i = 0; i < 10; i++)
        {
            history = history.WithHistoryItem(items[1].Id);
        }

        var weightedScores = items.Select(item => MainListPage.ScoreTopLevelItem(q, item, history, fuzzyMatcher)).ToList();
        var weightedMatches = GetMatches(items, weightedScores).ToList();

        Assert.AreEqual(3, weightedMatches.Count, "Find Terminal, VsCode and Run commands");

        // Terminal is in bucket 1, VS Code is in bucket 0, but Terminal matches
        // the title better
        Assert.AreEqual("Terminal Canary", weightedMatches[0].Title, "Terminal should be the top match, title match");
        Assert.AreEqual("Visual Studio Code", weightedMatches[1].Title, "VsCode does fuzzy match, but is less relevant than Terminal");
        Assert.AreEqual("Run commands", weightedMatches[2].Title, "run only matches on the subtitle");
    }

    [TestMethod]
    public void ValidateUsageDoesNotCrossTierBoundary()
    {
        // "C" is a prefix of "Command Prompt" (Prefix tier) but only a word-boundary
        // match for "Visual Studio Code" (AcronymWordBoundary tier). Frecency only
        // reorders items WITHIN a tier, so no amount of usage may lift a word-boundary
        // match above a prefix match. This is the core "logical ordering" contract.
        var items = CreateMockHistoryItems();
        var history = CreateMockHistoryService(items);
        var fuzzyMatcher = CreateMatcher();
        var q = fuzzyMatcher.PrecomputeQuery("C");

        var vsCodeId = items[1].Id;
        for (var i = 0; i < 25; i++)
        {
            history = history.WithHistoryItem(vsCodeId);

            var weightedScores = items.Select(item => MainListPage.ScoreTopLevelItem(q, item, history, fuzzyMatcher)).ToList();
            var weightedMatches = GetMatches(items, weightedScores).ToList();
            Assert.AreEqual(4, weightedMatches.Count);

            Assert.AreEqual("Command Prompt", weightedMatches[0].Title, "A prefix match must stay above a word-boundary match regardless of usage");
            Assert.AreEqual("Visual Studio Code", weightedMatches[1].Title, "VS Code should be the top of the word-boundary tier once used");
        }
    }

    [TestMethod]
    public void ValidateUsageReordersWithinTier()
    {
        // Both "Visual Studio 2022" and "Visual Studio Code" share the same tier for the
        // query "studio" (a word-boundary match on the second word, neither is a prefix).
        // Heavy usage of one should be able to reorder it above its peer within that tier.
        var items = new List<ListItemMock>
        {
            new("Visual Studio 2022", GivenId: "vs2022"),
            new("Visual Studio Code", GivenId: "vscode"),
        };

        var history = CreateHistory(items.Reverse<ListItemMock>().ToList());
        var fuzzyMatcher = CreateMatcher();
        var q = fuzzyMatcher.PrecomputeQuery("studio");

        // Both are equal word-boundary matches; give Code many uses so it climbs.
        for (var i = 0; i < 10; i++)
        {
            history = history.WithHistoryItem("vscode");
        }

        var scores = items.Select(item => MainListPage.ScoreTopLevelItem(q, item, history, fuzzyMatcher)).ToList();

        // Same tier for both, so the frequently-used one should not be below the other.
        Assert.IsTrue(
            MainListRanker.TierOf(scores[0]) == MainListRanker.TierOf(scores[1]),
            "Both items should be classified into the same tier");
        Assert.IsTrue(scores[1] >= scores[0], "The frequently-used item should reorder up within its tier");
    }

    [TestMethod]
    public void AliasSubstringOnlyMatchIsNotDropped()
    {
        // Regression: an item whose alias merely starts with the query, but whose title,
        // subtitle, and extension do not match at all, must still surface. A partial alias
        // is an explicit, user-assigned shortcut that may be intentionally unrelated to the
        // title (e.g. alias "term" on "Windows PowerShell", query "ter"). Before the fix,
        // ClassifyTier returned RankTier.None for this case, Pack produced 0, and the item
        // was filtered out by the "score > 0" gate in FilterListWithScores.
        var tier = MainListRanker.ClassifyTier(
            query: "ter",
            title: "Windows PowerShell",
            isFallback: false,
            isAliasExact: false,
            isAliasSubstringMatch: true,
            matchedLexically: false);

        Assert.AreNotEqual(RankTier.None, tier, "A partial-alias-only match must not be classified as None");
        Assert.AreEqual(RankTier.Fuzzy, tier, "A partial-alias-only match should floor to the Fuzzy tier");
        Assert.IsTrue(
            MainListRanker.Pack(tier, 0.0) > 0,
            "The packed score must be positive so the item survives the score > 0 filter");
    }

    [TestMethod]
    public void AliasSubstringFloorIsOnlyAFloor()
    {
        // The alias-substring floor never demotes a stronger title relationship: an exact
        // title match with a partial alias stays ExactTitle rather than dropping to Fuzzy.
        var exactWithAlias = MainListRanker.ClassifyTier(
            query: "settings",
            title: "Settings",
            isFallback: false,
            isAliasExact: false,
            isAliasSubstringMatch: true,
            matchedLexically: true);
        Assert.AreEqual(RankTier.ExactTitle, exactWithAlias, "A title exact match still wins over the alias floor");

        // With neither a lexical match nor any alias match, the item is still filtered out.
        var nothing = MainListRanker.ClassifyTier(
            query: "zzz",
            title: "Windows PowerShell",
            isFallback: false,
            isAliasExact: false,
            isAliasSubstringMatch: false,
            matchedLexically: false);
        Assert.AreEqual(RankTier.None, nothing, "With neither a lexical nor an alias match the item is None");
    }
}
