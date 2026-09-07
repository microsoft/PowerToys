// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
public partial class ProviderWeightingTests : CommandPaletteUnitTestBase
{
    private static IPrecomputedFuzzyMatcher CreateMatcher()
        => new PrecomputedFuzzyMatcher(new PrecomputedFuzzyMatcherOptions());

    private static RecentCommandsManager EmptyHistory() => new();

    // Maps each item to the weight configured for its provider id.
    private static Func<IListItem, ProviderSearchWeight> LookupBy(IReadOnlyDictionary<string, ProviderSearchWeight> byProvider)
        => item =>
        {
            var providerId = item is WeightItemMock mock ? mock.ProviderId ?? string.Empty : string.Empty;
            return byProvider.TryGetValue(providerId, out var weight) ? weight : ProviderSearchWeight.Normal;
        };

    [TestMethod]
    public void ProviderBonus_MapsSignedMagnitude()
    {
        Assert.AreEqual(-MainListRanker.ProviderWeightBonus, MainListRanker.ProviderBonus(ProviderSearchWeight.Lower));
        Assert.AreEqual(0.0, MainListRanker.ProviderBonus(ProviderSearchWeight.Normal));
        Assert.AreEqual(MainListRanker.ProviderWeightBonus, MainListRanker.ProviderBonus(ProviderSearchWeight.Higher));
    }

    [TestMethod]
    public void ProviderWeight_ReordersWithinTier()
    {
        // Two items that land in the SAME tier (exact-title) with identical lexical quality
        // and no history. The only differentiator is the per-provider weight.
        var fuzzyMatcher = CreateMatcher();
        var q = fuzzyMatcher.PrecomputeQuery("git");

        var itemA = new WeightItemMock("git", ProviderId: "providerA");
        var itemB = new WeightItemMock("git", ProviderId: "providerB");

        // Baseline: both Normal -> equal scores.
        var neutral = LookupBy(new Dictionary<string, ProviderSearchWeight>());
        var baseA = MainListPage.ScoreTopLevelItem(q, itemA, EmptyHistory(), fuzzyMatcher, neutral);
        var baseB = MainListPage.ScoreTopLevelItem(q, itemB, EmptyHistory(), fuzzyMatcher, neutral);
        Assert.AreEqual(baseA, baseB, "With both providers Normal, tied items should score equally");

        // A Higher, B Lower -> A must now sort above B.
        var lookup = LookupBy(new Dictionary<string, ProviderSearchWeight>
        {
            ["providerA"] = ProviderSearchWeight.Higher,
            ["providerB"] = ProviderSearchWeight.Lower,
        });
        var higherA = MainListPage.ScoreTopLevelItem(q, itemA, EmptyHistory(), fuzzyMatcher, lookup);
        var lowerB = MainListPage.ScoreTopLevelItem(q, itemB, EmptyHistory(), fuzzyMatcher, lookup);

        Assert.IsTrue(higherA > baseA, "Higher weight should raise the score");
        Assert.IsTrue(lowerB < baseB, "Lower weight should reduce the score");
        Assert.IsTrue(higherA > lowerB, "Higher-weighted provider should outrank the lower-weighted one within the tier");

        // Same tier the whole time - the nudge only reordered within it.
        Assert.AreEqual(MainListRanker.TierOf(baseA), MainListRanker.TierOf(higherA));
        Assert.AreEqual(MainListRanker.TierOf(baseB), MainListRanker.TierOf(lowerB));
    }

    [TestMethod]
    public void ProviderWeight_NeverCrossesTierBoundary()
    {
        var fuzzyMatcher = CreateMatcher();
        var q = fuzzyMatcher.PrecomputeQuery("code");

        // "code" exactly matches -> ExactTitle tier. Give it the WORST weight.
        var exact = new WeightItemMock("code", ProviderId: "weak");

        // "Visual Studio Code" only matches "code" at a word boundary -> lower tier. Give it
        // the BEST weight, plus a pile of history, to try to jump the tier boundary.
        var lowerTier = new WeightItemMock("Visual Studio Code", ProviderId: "strong");

        var lookup = LookupBy(new Dictionary<string, ProviderSearchWeight>
        {
            ["weak"] = ProviderSearchWeight.Lower,
            ["strong"] = ProviderSearchWeight.Higher,
        });

        var history = EmptyHistory();
        for (var i = 0; i < 50; i++)
        {
            history = history.WithHistoryItem(lowerTier.Id);
        }

        var exactScore = MainListPage.ScoreTopLevelItem(q, exact, EmptyHistory(), fuzzyMatcher, lookup);
        var lowerScore = MainListPage.ScoreTopLevelItem(q, lowerTier, history, fuzzyMatcher, lookup);

        Assert.IsTrue(
            MainListRanker.TierOf(exactScore) > MainListRanker.TierOf(lowerScore),
            "The exact match must live in a strictly higher tier");
        Assert.IsTrue(
            exactScore > lowerScore,
            "A within-tier nudge (even Higher + heavy history) must never promote an item across a tier boundary");
    }

    [TestMethod]
    public void ProviderWeight_DefaultNormalMatchesNoLookup()
    {
        var fuzzyMatcher = CreateMatcher();
        var q = fuzzyMatcher.PrecomputeQuery("term");
        var item = new WeightItemMock("Terminal", ProviderId: "providerA");

        // No lookup at all should behave exactly like an all-Normal lookup, which should
        // behave exactly like the previous (provider-unaware) scoring.
        var noLookup = MainListPage.ScoreTopLevelItem(q, item, EmptyHistory(), fuzzyMatcher);
        var normalLookup = MainListPage.ScoreTopLevelItem(
            q,
            item,
            EmptyHistory(),
            fuzzyMatcher,
            LookupBy(new Dictionary<string, ProviderSearchWeight> { ["providerA"] = ProviderSearchWeight.Normal }));

        Assert.AreEqual(noLookup, normalLookup, "Normal weight must be a no-op relative to the default path");
    }

    [TestMethod]
    public void ProviderWeight_AppliesToAnyProviderItem()
    {
        // App items are plain IListItems (not TopLevelViewModel). This asserts the scorer
        // honors the provider weight for ANY item, which is how installed apps (the "AllApps"
        // provider) get nudged.
        var fuzzyMatcher = CreateMatcher();
        var q = fuzzyMatcher.PrecomputeQuery("note");
        var appItem = new WeightItemMock("Notepad", ProviderId: "AllApps");

        var normal = MainListPage.ScoreTopLevelItem(q, appItem, EmptyHistory(), fuzzyMatcher);
        var higher = MainListPage.ScoreTopLevelItem(
            q,
            appItem,
            EmptyHistory(),
            fuzzyMatcher,
            LookupBy(new Dictionary<string, ProviderSearchWeight> { ["AllApps"] = ProviderSearchWeight.Higher }));

        Assert.IsTrue(higher > normal, "An app-style item should also respond to its provider's Higher weight");
    }

    [TestMethod]
    public void SearchWeight_SerializationRoundTrips()
    {
        var dict = ImmutableDictionary<string, ProviderSettings>.Empty
            .SetItem("p", new ProviderSettings { SearchWeight = ProviderSearchWeight.Higher });

        var json = JsonSerializer.Serialize(dict, JsonSerializationContext.Default.ImmutableProviderSettingsDictionary);
        var restored = JsonSerializer.Deserialize(json, JsonSerializationContext.Default.ImmutableProviderSettingsDictionary);

        Assert.IsNotNull(restored);
        Assert.AreEqual(ProviderSearchWeight.Higher, restored!["p"].SearchWeight);
    }

    [TestMethod]
    public void SearchWeight_LegacyJsonDeserializesToNormal()
    {
        // Legacy persisted settings predate SearchWeight; the missing property must default
        // to Normal rather than throwing or landing on Lower.
        const string legacyJson = "{\"p\":{\"IsEnabled\":true}}";

        var restored = JsonSerializer.Deserialize(legacyJson, JsonSerializationContext.Default.ImmutableProviderSettingsDictionary);

        Assert.IsNotNull(restored);
        Assert.AreEqual(ProviderSearchWeight.Normal, restored!["p"].SearchWeight);
    }

    private sealed partial record WeightItemMock(
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
}
