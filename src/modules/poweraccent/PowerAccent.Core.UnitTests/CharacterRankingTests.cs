// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using static PowerAccent.Core.PowerAccent;

namespace PowerAccent.Core.UnitTests;

[TestClass]
public sealed class CharacterRankingTests
{
    [TestMethod]
    public void FrequencyUsesDisplayedCase()
    {
        static uint Frequency(string character) => character is "Ω" or "α" ? 2u : 1u;

        string[] mapped = ["α", "ω"];
        CollectionAssert.AreEqual(new[] { "Ω", "Α" }, RankCharacters(mapped, true, true, Frequency, _ => 0));
        CollectionAssert.AreEqual(new[] { "α", "ω" }, RankCharacters(mapped, false, true, Frequency, _ => 0));
    }

    [TestMethod]
    public void EqualCountsUseRecencyThenMappingOrder()
    {
        string[] mapped = ["α", "ω"];
        CollectionAssert.AreEqual(new[] { "Ω", "Α" }, RankCharacters(mapped, true, true, _ => 1, c => c == "Ω" ? 2 : 1));
        CollectionAssert.AreEqual(new[] { "Α", "Ω" }, RankCharacters(mapped, true, true, _ => 1, _ => 1));
    }

    [TestMethod]
    public void SortingDisabledPreservesUppercaseMappingOrder()
    {
        var result = RankCharacters(["α", "ϑ", "ß"], true, false, c => c == "ẞ" ? 2u : 1u, _ => 0);

        CollectionAssert.AreEqual(new[] { "Α", "ẞ" }, result);
    }
}
