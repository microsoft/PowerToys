// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.CmdPal.Ext.TextExpander.Snippets;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.TextExpander.UnitTests;

[TestClass]
public sealed class SnippetSearchTests
{
    private static readonly Snippet[] Snippets =
    [
        new("!mysig", "Best regards,\nYour name"),
        new("addr", "1 Main Street, my town"),
        new("m_y", "Fuzzy"),
        new("myemail", "you@example.com"),
        new("!my", "Short"),
        new("my", "Exact"),
        new("zzz", "Nothing to see"),
    ];

    [TestMethod]
    public void Filter_EmptySearch_ListsEverySnippetInFileOrder()
        => CollectionAssert.AreEqual(Snippets, SnippetSearch.Filter(Snippets, "  ").ToArray());

    [TestMethod]
    public void Filter_Search_RanksExactThenStartThenContainsThenFuzzyTriggersThenText()
    {
        string[] expected = ["my", "myemail", "!my", "!mysig", "m_y", "addr"];

        CollectionAssert.AreEqual(expected, SnippetSearch.Filter(Snippets, "my").Select(snippet => snippet.Trigger).ToArray());
    }

    [TestMethod]
    public void Filter_SearchInDifferentCapitals_FindsTheTriggerButAnExactMatchComesFirst()
    {
        Snippet[] snippets = [new("Sig", "Formal"), new("sig", "Informal")];
        string[] expected = ["Informal", "Formal"];

        CollectionAssert.AreEqual(expected, SnippetSearch.Filter(snippets, "sig").Select(snippet => snippet.Replacement).ToArray());
    }

    [TestMethod]
    public void Filter_WordsInTheText_FindTheSnippet()
        => Assert.AreEqual("!mysig", SnippetSearch.Filter(Snippets, "regards").Single().Trigger);

    [TestMethod]
    public void BestTriggerMatch_Search_PicksTheClosestTrigger()
    {
        Assert.AreEqual("my", SnippetSearch.BestTriggerMatch(Snippets, "my")?.Trigger);
        Assert.AreEqual("!mysig", SnippetSearch.BestTriggerMatch(Snippets, "mysig")?.Trigger);
        Assert.AreEqual("myemail", SnippetSearch.BestTriggerMatch(Snippets, "MYE")?.Trigger);
    }

    [TestMethod]
    public void BestTriggerMatch_OnlyTheTextMatches_OffersNothing()
    {
        Assert.IsNull(SnippetSearch.BestTriggerMatch(Snippets, "regards"));
        Assert.IsNull(SnippetSearch.BestTriggerMatch(Snippets, string.Empty));
    }
}
