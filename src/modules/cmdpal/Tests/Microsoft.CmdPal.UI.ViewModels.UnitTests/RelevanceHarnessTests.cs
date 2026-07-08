// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.Ext.UnitTestBase;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.MainPage;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;
using WyHash;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

/// <summary>
/// Golden-set relevance harness for the main/root page ranker. Every case is expressed as a
/// realistic query paired with an ordering constraint (rank-1, or "X must rank above Y") and
/// asserted against the REAL <see cref="MainListPage.ScoreTopLevelItem"/> scoring path, sorted
/// exactly the way the product sorts (positive scores, descending). The intent is to lock in
/// "results seem logical and relevant" as an objective, extendable yardstick: to add a new
/// scenario, drop another entry in the fixture and another constraint in a test.
///
/// This file also carries focused per-tier unit tests for <see cref="MainListRanker"/> that
/// cover the pieces the golden set cannot easily drive through app/command mocks - alias-exact
/// and fallback-floor classification, and the packing invariants that guarantee a higher tier
/// always outranks a lower one regardless of within-tier score.
/// </summary>
[TestClass]
public partial class RelevanceHarnessTests : CommandPaletteUnitTestBase
{
    // A lightweight IListItem stand-in for a top-level command or installed app. Mirrors the
    // mock used by RecentCommandsTests so the harness exercises the same real scoring path,
    // not a reimplementation. ProviderId lets a test key a per-provider weight lookup.
    private sealed partial record ListItemMock(
        string Title,
        string? Subtitle = "",
        string? GivenId = "",
        string? ProviderId = "") : IListItem
    {
        public string Id => string.IsNullOrEmpty(GivenId) ? GenerateId() : GivenId;

        public IDetails Details => throw new NotImplementedException();

        public string Section => throw new NotImplementedException();

        public ITag[] Tags => throw new NotImplementedException();

        public string TextToSuggest => throw new NotImplementedException();

        public ICommand Command => new NoOpCommand() { Id = Id };

        public IIconInfo Icon => throw new NotImplementedException();

        public IContextItem[] MoreCommands => throw new NotImplementedException();

#pragma warning disable CS0067
        public event TypedEventHandler<object, IPropChangedEventArgs>? PropChanged;
#pragma warning restore CS0067

        private string GenerateId()
        {
            var result = WyHash64.ComputeHash64(ProviderId + Title + Subtitle, seed: 0);
            return $"{ProviderId}{result}";
        }
    }

    private static IPrecomputedFuzzyMatcher CreateMatcher() =>
        new PrecomputedFuzzyMatcher(new PrecomputedFuzzyMatcherOptions());

    private static RecentCommandsManager EmptyHistory() => new();

    // A representative slice of the main page: installed apps + top-level commands with
    // realistic titles, subtitles and shared prefixes/acronyms. Deliberately includes the
    // "confusable" clusters users complain about (Calc*, Visual Studio *, Command Prompt vs
    // Control Panel) so the golden constraints below have real competition to beat.
    private static List<ListItemMock> Fixture() => new()
    {
        new("Command Prompt", "Run the classic command interpreter"),
        new("Control Panel", "Adjust your computer's settings"),
        new("Calculator", "Perform calculations"),
        new("Calendar", "View your schedule"),
        new("Visual Studio Code", "Code editing. Redefined."),
        new("Visual Studio 2022", "Full-featured IDE"),
        new("Windows Settings", "Change PC settings"),
        new("Windows Terminal", "Modern terminal for command-line tools"),
        new("Task Manager", "Monitor apps and processes"),
        new("Notepad", "A simple text editor"),
        new("Microsoft Edge", "Browse the web"),
        new("Paint", "Draw and edit images"),
        new("Paint 3D", "Create in three dimensions"),
    };

    // Scores every fixture item for a query through the real product scorer and returns the
    // matched titles in the exact order the product would render them: positive scores only,
    // sorted descending. Mirrors InternalListHelpers.FilterListWithScores and the existing
    // RecentCommandsTests.GetMatches helper.
    private static List<string> Rank(
        string query,
        IEnumerable<ListItemMock> items,
        IRecentCommandsManager? history = null,
        Func<IListItem, ProviderSearchWeight>? providerWeightLookup = null)
    {
        var matcher = CreateMatcher();
        var q = matcher.PrecomputeQuery(query);
        history ??= EmptyHistory();

        return items
            .Select(item => (item.Title, Score: MainListPage.ScoreTopLevelItem(q, item, history, matcher, providerWeightLookup)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Select(x => x.Title)
            .ToList();
    }

    private static void AssertRank1(
        string query,
        string expectedTitle,
        IRecentCommandsManager? history = null,
        Func<IListItem, ProviderSearchWeight>? providerWeightLookup = null)
    {
        var ranked = Rank(query, Fixture(), history, providerWeightLookup);
        Assert.IsTrue(ranked.Count > 0, $"Query '{query}' should return at least one match");
        Assert.AreEqual(
            expectedTitle,
            ranked[0],
            $"Query '{query}' should surface '{expectedTitle}' at rank 1. Actual order: [{string.Join(", ", ranked)}]");
    }

    private static void AssertRanksAbove(
        string query,
        string higher,
        string lower,
        IRecentCommandsManager? history = null,
        Func<IListItem, ProviderSearchWeight>? providerWeightLookup = null)
    {
        var ranked = Rank(query, Fixture(), history, providerWeightLookup);
        var higherIndex = ranked.IndexOf(higher);
        var lowerIndex = ranked.IndexOf(lower);

        Assert.IsTrue(higherIndex >= 0, $"Query '{query}' should match '{higher}'. Actual order: [{string.Join(", ", ranked)}]");
        Assert.IsTrue(lowerIndex >= 0, $"Query '{query}' should match '{lower}'. Actual order: [{string.Join(", ", ranked)}]");
        Assert.IsTrue(
            higherIndex < lowerIndex,
            $"Query '{query}' should rank '{higher}' above '{lower}'. Actual order: [{string.Join(", ", ranked)}]");
    }

    // Golden set: the tier ladder, end to end.
    [TestMethod]
    public void Golden_ExactTitleBeatsPrefix()
    {
        // "Paint" is an exact title; "Paint 3D" only has it as a prefix. Exact must win.
        AssertRank1("paint", "Paint");
        AssertRanksAbove("paint", "Paint", "Paint 3D");
    }

    [TestMethod]
    public void Golden_PrefixBeatsWordBoundary()
    {
        // "co" is a title prefix of Command Prompt and Control Panel, but only a word-boundary
        // match for "Code" inside Visual Studio Code. Prefix outranks word-boundary.
        AssertRanksAbove("co", "Command Prompt", "Visual Studio Code");
        AssertRanksAbove("co", "Control Panel", "Visual Studio Code");
    }

    [TestMethod]
    public void Golden_WordBoundaryBeatsFuzzy()
    {
        // "man" starts the word "Manager" in Task Manager (word-boundary), but is only a loose
        // subsequence (m..a..n) of "Command Prompt" (fuzzy). Word-boundary must win.
        AssertRank1("man", "Task Manager");
        AssertRanksAbove("man", "Task Manager", "Command Prompt");
    }

    [TestMethod]
    public void Golden_AcronymSurfacesTheRightApp()
    {
        // "vsc" is the acronym of Visual Studio Code (V-S-C); Visual Studio 2022 (V-S-2) is not
        // a match. The acronym should surface the obviously-right app at rank 1.
        AssertRank1("vsc", "Visual Studio Code");
    }

    [TestMethod]
    public void Golden_ComplaintCase_SingleLetterSurfacesFrecentApp()
    {
        // "c" prefixes several apps (Calculator, Calendar, Command Prompt, Control Panel). With
        // no signal they tie; a user who keeps opening Calculator should see it at rank 1. This
        // is the canonical "the thing I want is buried" complaint, fixed by within-tier frecency.
        var history = EmptyHistory();
        var calculatorId = Fixture().First(i => i.Title == "Calculator").Id;
        for (var i = 0; i < 5; i++)
        {
            history = history.WithHistoryItem(calculatorId);
        }

        AssertRank1("c", "Calculator", history);
    }

    [TestMethod]
    public void Golden_ComplaintCase_CodeSurfacesVsCode()
    {
        // Typing "code" should put Visual Studio Code first (word-boundary on "Code").
        AssertRank1("code", "Visual Studio Code");
    }

    [TestMethod]
    public void Golden_ComplaintCase_SetSurfacesSettings()
    {
        // Typing "set" should put Windows Settings first (word-boundary on "Settings").
        AssertRank1("set", "Windows Settings");
    }

    [TestMethod]
    public void Golden_FrecencyReordersWithinTierOnly()
    {
        // Heavy use of Visual Studio Code (a word-boundary match for "co") must NOT lift it over
        // Command Prompt / Control Panel, which are prefix matches a whole tier above it.
        // Frecency reorders within a tier; it can never cross a tier boundary.
        var history = EmptyHistory();
        var vsCodeId = Fixture().First(i => i.Title == "Visual Studio Code").Id;
        for (var i = 0; i < 50; i++)
        {
            history = history.WithHistoryItem(vsCodeId);
        }

        AssertRanksAbove("co", "Command Prompt", "Visual Studio Code", history);
        AssertRanksAbove("co", "Control Panel", "Visual Studio Code", history);
    }

    [TestMethod]
    public void Golden_FrecencyBreaksTieWithinTier()
    {
        // "vs" is an acronym match for both Visual Studio Code and Visual Studio 2022 (same
        // tier). With no history they tie; the recently/repeatedly used one should climb to the
        // top of the tier.
        var fixture = Fixture();
        var vs2022Id = fixture.First(i => i.Title == "Visual Studio 2022").Id;

        var history = EmptyHistory();
        for (var i = 0; i < 5; i++)
        {
            history = history.WithHistoryItem(vs2022Id);
        }

        AssertRanksAbove("vs", "Visual Studio 2022", "Visual Studio Code", history);
    }

    [TestMethod]
    public void Golden_ProviderHigherBreaksAnExactTie()
    {
        // Two providers surface an identically-titled "Settings" command. Everything else being
        // equal (same tier, same lexical quality, no frecency), a provider marked Higher should
        // sort above the Normal one. Provider weight is a within-tier nudge for near-ties only.
        var alpha = new ListItemMock("Settings", "From provider Alpha", ProviderId: "alpha");
        var bravo = new ListItemMock("Settings", "From provider Bravo", ProviderId: "bravo");
        var items = new List<ListItemMock> { alpha, bravo };

        var matcher = CreateMatcher();
        var q = matcher.PrecomputeQuery("Settings");
        var history = EmptyHistory();

        // Baseline: with no provider weighting the two tie exactly.
        var baseAlpha = MainListPage.ScoreTopLevelItem(q, alpha, history, matcher);
        var baseBravo = MainListPage.ScoreTopLevelItem(q, bravo, history, matcher);
        Assert.AreEqual(baseAlpha, baseBravo, "The two identically-titled items should tie before provider weighting");

        Func<IListItem, ProviderSearchWeight> lookup = item =>
            item is ListItemMock m && m.ProviderId == "bravo"
                ? ProviderSearchWeight.Higher
                : ProviderSearchWeight.Normal;

        var ranked = items
            .Select(item => (item.ProviderId, Score: MainListPage.ScoreTopLevelItem(q, item, history, matcher, lookup)))
            .OrderByDescending(x => x.Score)
            .Select(x => x.ProviderId)
            .ToList();

        Assert.AreEqual("bravo", ranked[0], "The Higher-weighted provider should win an otherwise exact tie");
    }

    [TestMethod]
    public void Golden_ProviderWeightCannotCrossTierBoundary()
    {
        // Even marked Higher, a word-boundary match (Visual Studio Code for "co") must stay
        // below a prefix match (Command Prompt). Provider weight is clamped within a tier.
        Func<IListItem, ProviderSearchWeight> boostVsCode = item =>
            item is ListItemMock m && m.Title == "Visual Studio Code"
                ? ProviderSearchWeight.Higher
                : ProviderSearchWeight.Normal;

        AssertRanksAbove("co", "Command Prompt", "Visual Studio Code", providerWeightLookup: boostVsCode);
    }

    // Per-tier unit tests for MainListRanker.
    [TestMethod]
    public void ClassifyTier_AliasExact_WinsEvenOverFallback()
    {
        // Alias-exact is the strongest, most explicit signal - it beats even a fallback flag.
        Assert.AreEqual(
            RankTier.AliasExact,
            MainListRanker.ClassifyTier("gh", "GitHub", isFallback: true, isAliasExact: true, matchedLexically: false));
    }

    [TestMethod]
    public void ClassifyTier_Fallback_SitsAtTheFloor()
    {
        // A fallback that is not an alias-exact match lands on the floor tier regardless of any
        // lexical match, so dynamic fallbacks appear after direct command/app matches.
        Assert.AreEqual(
            RankTier.FallbackFloor,
            MainListRanker.ClassifyTier("anything", "Some Fallback", isFallback: true, isAliasExact: false, matchedLexically: true));
    }

    [TestMethod]
    public void ClassifyTier_ExactTitle()
    {
        Assert.AreEqual(
            RankTier.ExactTitle,
            MainListRanker.ClassifyTier("calculator", "Calculator", isFallback: false, isAliasExact: false, matchedLexically: true));
    }

    [TestMethod]
    public void ClassifyTier_Prefix()
    {
        Assert.AreEqual(
            RankTier.Prefix,
            MainListRanker.ClassifyTier("cal", "Calculator", isFallback: false, isAliasExact: false, matchedLexically: true));
    }

    [TestMethod]
    public void ClassifyTier_WordBoundary()
    {
        Assert.AreEqual(
            RankTier.AcronymWordBoundary,
            MainListRanker.ClassifyTier("code", "Visual Studio Code", isFallback: false, isAliasExact: false, matchedLexically: true));
    }

    [TestMethod]
    public void ClassifyTier_Acronym()
    {
        Assert.AreEqual(
            RankTier.AcronymWordBoundary,
            MainListRanker.ClassifyTier("vs", "Visual Studio Code", isFallback: false, isAliasExact: false, matchedLexically: true));
    }

    [TestMethod]
    public void ClassifyTier_Fuzzy()
    {
        // Lexically matched, but not exact/prefix/word-boundary/acronym.
        Assert.AreEqual(
            RankTier.Fuzzy,
            MainListRanker.ClassifyTier("cmd", "Command Prompt", isFallback: false, isAliasExact: false, matchedLexically: true));
    }

    [TestMethod]
    public void ClassifyTier_None_WhenNothingMatched()
    {
        Assert.AreEqual(
            RankTier.None,
            MainListRanker.ClassifyTier("zzz", "Command Prompt", isFallback: false, isAliasExact: false, matchedLexically: false));
    }

    [TestMethod]
    public void Pack_HigherTierAlwaysOutranksLowerTier()
    {
        // The core invariant: a higher tier with the WORST possible within-tier score still
        // outranks a lower tier with the BEST possible within-tier score. This is what makes
        // "an exact match always beats a fuzzy one" true no matter how much frecency piles up.
        RankTier[] ascending =
        {
            RankTier.FallbackFloor,
            RankTier.Fuzzy,
            RankTier.AcronymWordBoundary,
            RankTier.Prefix,
            RankTier.ExactTitle,
            RankTier.AliasExact,
        };

        for (var i = 0; i < ascending.Length - 1; i++)
        {
            var lower = MainListRanker.Pack(ascending[i], MainListRanker.TierStride - 1);
            var higher = MainListRanker.Pack(ascending[i + 1], 0);
            Assert.IsTrue(
                higher > lower,
                $"{ascending[i + 1]} (min within-tier) must outrank {ascending[i]} (max within-tier)");
        }
    }

    [TestMethod]
    public void Pack_NoneIsZeroAndFiltered()
    {
        Assert.AreEqual(0, MainListRanker.Pack(RankTier.None, 999_999));
    }

    [TestMethod]
    public void Pack_WithinTierScoreIsClampedToItsBand()
    {
        // An absurd within-tier score must never spill into the next tier's band.
        var packed = MainListRanker.Pack(RankTier.Fuzzy, double.MaxValue);
        Assert.AreEqual(RankTier.Fuzzy, MainListRanker.TierOf(packed));

        var nextTierFloor = MainListRanker.Pack(RankTier.AcronymWordBoundary, 0);
        Assert.IsTrue(packed < nextTierFloor, "A clamped within-tier score must stay below the next tier");
    }

    [TestMethod]
    public void Pack_WithinTierScoreOrdersItemsInTheSameTier()
    {
        var low = MainListRanker.Pack(RankTier.Prefix, 10);
        var high = MainListRanker.Pack(RankTier.Prefix, 20);
        Assert.IsTrue(high > low, "Within the same tier, a higher within-tier score sorts higher");
        Assert.AreEqual(MainListRanker.TierOf(low), MainListRanker.TierOf(high), "Both remain in the same tier");
    }

    [TestMethod]
    public void TierOf_RoundTripsEveryTier()
    {
        foreach (RankTier tier in Enum.GetValues(typeof(RankTier)))
        {
            if (tier == RankTier.None)
            {
                continue;
            }

            var packed = MainListRanker.Pack(tier, 42);
            Assert.AreEqual(tier, MainListRanker.TierOf(packed), $"Packing then unpacking {tier} should round-trip");
        }
    }

    [TestMethod]
    public void WithinTierScore_LexicalQualityLeads()
    {
        // More lexical quality raises the within-tier score, all else equal.
        var lowLexical = MainListRanker.WithinTierScore(lexicalQuality: 5, frecencyWeight: 0, aliasSubstringBonus: 0, providerBonus: 0);
        var highLexical = MainListRanker.WithinTierScore(lexicalQuality: 6, frecencyWeight: 0, aliasSubstringBonus: 0, providerBonus: 0);
        Assert.IsTrue(highLexical > lowLexical, "Higher lexical quality should raise the within-tier score");
    }

    [TestMethod]
    public void WithinTierScore_FrecencyBreaksTies()
    {
        var noFrecency = MainListRanker.WithinTierScore(lexicalQuality: 5, frecencyWeight: 0, aliasSubstringBonus: 0, providerBonus: 0);
        var withFrecency = MainListRanker.WithinTierScore(lexicalQuality: 5, frecencyWeight: 3, aliasSubstringBonus: 0, providerBonus: 0);
        Assert.IsTrue(withFrecency > noFrecency, "Frecency should raise the within-tier score for otherwise-equal items");
    }

    [TestMethod]
    public void ProviderBonus_LowerIsBelowNormalIsBelowHigher()
    {
        Assert.IsTrue(
            MainListRanker.ProviderBonus(ProviderSearchWeight.Lower) < MainListRanker.ProviderBonus(ProviderSearchWeight.Normal),
            "Lower should subtract relative to Normal");
        Assert.IsTrue(
            MainListRanker.ProviderBonus(ProviderSearchWeight.Normal) < MainListRanker.ProviderBonus(ProviderSearchWeight.Higher),
            "Higher should add relative to Normal");
        Assert.AreEqual(0.0, MainListRanker.ProviderBonus(ProviderSearchWeight.Normal), "Normal is the neutral default");
    }
}
