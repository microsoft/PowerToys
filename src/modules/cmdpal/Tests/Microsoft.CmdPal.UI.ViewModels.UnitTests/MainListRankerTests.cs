// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.MainPage;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

/// <summary>
/// Focused, per-tier unit tests for the <see cref="MainListRanker"/> primitives - tier
/// classification, the tier/within-tier packing invariants, and the within-tier score inputs.
/// These cover the pieces the end-to-end <see cref="RelevanceHarnessTests"/> cannot easily drive
/// through app/command mocks (alias-exact and fallback-floor classification, and the packing
/// guarantee that a higher tier always outranks a lower one regardless of within-tier score).
/// </summary>
[TestClass]
public class MainListRankerTests
{
    [DataTestMethod]
    [DataRow("gh", "GitHub", true, true, false, false, RankTier.AliasExact, DisplayName = "Alias-exact is the strongest, most explicit signal and beats even a fallback flag")]
    [DataRow("zzz", "Some Fallback", true, false, false, false, RankTier.FallbackFloor, DisplayName = "A fallback that did not match at all floors to FallbackFloor rather than dropping")]
    [DataRow("reload", "Reload", true, false, false, true, RankTier.ExactTitle, DisplayName = "A fallback whose dynamic title exactly matches the query earns ExactTitle")]
    [DataRow("rel", "Reload", true, false, false, true, RankTier.Prefix, DisplayName = "A fallback whose title starts with the query earns Prefix")]
    [DataRow("vs", "Visual Studio Code", true, false, false, true, RankTier.AcronymWordBoundary, DisplayName = "A fallback acronym match earns AcronymWordBoundary")]
    [DataRow("cmd", "Command Prompt", true, false, false, true, RankTier.Fuzzy, DisplayName = "A fallback fuzzy match earns Fuzzy")]
    [DataRow("calculator", "Calculator", false, false, false, true, RankTier.ExactTitle, DisplayName = "Exact title match")]
    [DataRow("cal", "Calculator", false, false, false, true, RankTier.Prefix, DisplayName = "Title prefix match")]
    [DataRow("code", "Visual Studio Code", false, false, false, true, RankTier.AcronymWordBoundary, DisplayName = "Word-boundary match")]
    [DataRow("vs", "Visual Studio Code", false, false, false, true, RankTier.AcronymWordBoundary, DisplayName = "Acronym match")]
    [DataRow("cmd", "Command Prompt", false, false, false, true, RankTier.Fuzzy, DisplayName = "A lexical match that is not exact/prefix/word-boundary/acronym is fuzzy")]
    [DataRow("zzz", "Command Prompt", false, false, false, false, RankTier.None, DisplayName = "Nothing matched")]
    [DataRow("zz", "Some Command", false, false, true, false, RankTier.Fuzzy, DisplayName = "An alias-substring match keeps the item at the fuzzy floor even with no lexical match")]
    public void ClassifyTier_ClassifiesEachSignalIntoItsTier(
        string query,
        string title,
        bool isFallback,
        bool isAliasExact,
        bool isAliasSubstringMatch,
        bool matchedLexically,
        RankTier expected)
    {
        Assert.AreEqual(
            expected,
            MainListRanker.ClassifyTier(query, title, isFallback, isAliasExact, isAliasSubstringMatch, matchedLexically));
    }

    [TestMethod]
    public void ClassifyThenPack_ExactFallbackOutranksFuzzyNonFallback()
    {
        // The reported bug: searching "reload" surfaced fuzzy junk (e.g. "Fast Virtual Desktops")
        // above the exact "Reload" fallback because fallbacks were pinned to FallbackFloor. Now a
        // fallback whose dynamic title exactly matches is tiered like any exact match, so it wins
        // even against a fuzzy non-fallback carrying the maximum possible within-tier score.
        var fallbackTier = MainListRanker.ClassifyTier(
            "reload", "Reload", isFallback: true, isAliasExact: false, isAliasSubstringMatch: false, matchedLexically: true);
        var fuzzyTier = MainListRanker.ClassifyTier(
            "reload", "Fast Virtual Desktops", isFallback: false, isAliasExact: false, isAliasSubstringMatch: false, matchedLexically: true);

        Assert.AreEqual(RankTier.ExactTitle, fallbackTier, "An exact-title fallback should be tiered ExactTitle");
        Assert.AreEqual(RankTier.Fuzzy, fuzzyTier, "A loose subsequence match should be tiered Fuzzy");

        var fallbackScore = MainListRanker.Pack(fallbackTier, withinTierScore: 0);
        var fuzzyScore = MainListRanker.Pack(fuzzyTier, MainListRanker.TierStride - 1);

        Assert.IsTrue(
            fallbackScore > fuzzyScore,
            "An exact-title fallback must outrank a fuzzy non-fallback even at the fuzzy item's best within-tier score");
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
