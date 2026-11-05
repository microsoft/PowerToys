// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Common.Search.FuzzSearch;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Settings.UI.UnitTests.Search
{
    [TestClass]
    public class StringMatcherTests
    {
        private static readonly int[] PrefixMatchData = [0, 1, 2, 3, 4];
        private static readonly int[] MultiwordMatchData = [0, 1, 2, 5, 6, 7];

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow(" ")]
        [DataRow("   ")]
        [DataRow("\t")]
        [DataRow("\r\n")]
        [DataRow("\v\f")]
        [DataRow("\u00A0")]
        [DataRow("\u1680")]
        [DataRow("\u2000\u2001\u2002\u2003\u2009")]
        [DataRow("\u2028\u2029")]
        [DataRow("\u202F")]
        [DataRow("\u205F")]
        [DataRow("\u3000")]
        [DataRow("\t \u00A0\u3000\r\n")]
        public void FuzzyMatch_EmptyOrWhitespaceQuery_ReturnsNoMatch(string query)
        {
            foreach (var ignoreCase in new[] { true, false })
            {
                var result = StringMatcher.FuzzyMatch(query, "PowerToys", new MatchOption { IgnoreCase = ignoreCase });

                Assert.IsFalse(result.Success);
                Assert.AreEqual(0, result.Score);
                Assert.AreEqual(0, result.RawScore);
                Assert.AreEqual(0, result.MatchData.Count);
            }
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        public void FuzzyMatch_EmptyCandidate_ReturnsNoMatch(string candidate)
        {
            var result = StringMatcher.FuzzyMatch("power", candidate);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(0, result.Score);
            Assert.AreEqual(0, result.MatchData.Count);
        }

        [TestMethod]
        [DataRow("power")]
        [DataRow("POWER")]
        [DataRow(" power ")]
        [DataRow("\tPOWER\r\n")]
        [DataRow("\u00A0power\u3000")]
        public void FuzzyMatch_NonemptyQuery_PreservesScoreAndHighlights(string query)
        {
            var result = StringMatcher.FuzzyMatch(query, "PowerToys");

            Assert.IsTrue(result.Success);
            Assert.AreEqual(157, result.Score);
            CollectionAssert.AreEqual(PrefixMatchData, result.MatchData);
        }

        [TestMethod]
        public void FuzzyMatch_ExactMatch_RanksAbovePrefix()
        {
            var exact = StringMatcher.FuzzyMatch("PowerToys", "PowerToys");
            var prefix = StringMatcher.FuzzyMatch("PowerToys", "PowerToys Settings");

            Assert.IsTrue(exact.Success);
            Assert.IsTrue(prefix.Success);
            Assert.AreEqual(190, exact.Score);
            Assert.IsTrue(exact.Score > prefix.Score);
        }

        [TestMethod]
        public void FuzzyMatch_MultiwordQuery_PreservesScoreAndHighlights()
        {
            var result = StringMatcher.FuzzyMatch("pow toy", "PowerToys");

            Assert.IsTrue(result.Success);
            Assert.AreEqual(154, result.Score);
            CollectionAssert.AreEqual(MultiwordMatchData, result.MatchData);
        }

        [TestMethod]
        public void FuzzyMatch_CaseSensitiveQuery_PreservesMatchOption()
        {
            var options = new MatchOption { IgnoreCase = false };

            Assert.IsTrue(StringMatcher.FuzzyMatch("Power", "PowerToys", options).Success);
            Assert.IsFalse(StringMatcher.FuzzyMatch("POWER", "PowerToys", options).Success);
        }

        [TestMethod]
        public void FuzzyMatch_UnrelatedQuery_ReturnsNoMatch()
        {
            var result = StringMatcher.FuzzyMatch("unrelated", "PowerToys");

            Assert.IsFalse(result.Success);
            Assert.AreEqual(0, result.Score);
        }
    }
}
