// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Betsegaw Tadele's https://github.com/betsegaw/windowwalker/

using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WindowWalker.Components
{
    [TestClass]
    public class FuzzyMatchingUnitTest
    {
        [TestMethod]
        public void SimpleMatching()
        {
            List<int> result = FuzzyMatching.FindBestFuzzyMatch("watsapp hellow", "hello");
            List<int> expected = new List<int>() { 8, 9, 10, 11, 12 };

            Assert.IsTrue(IsEqual(expected, result));
        }

        [TestMethod]
        public void NoResult()
        {
            List<int> result = FuzzyMatching.FindBestFuzzyMatch("what is going on?", "whatsx goin on?");
            List<int> expected = new List<int>();

            Assert.IsTrue(IsEqual(expected, result));
        }

        [TestMethod]
        public void ZeroLengthSearchString()
        {
            List<int> result = FuzzyMatching.FindBestFuzzyMatch("whatsapp hellow", string.Empty);
            List<int> expected = new List<int>();

            Assert.IsTrue(IsEqual(expected, result));
        }

        [TestMethod]
        public void ZeroLengthText()
        {
            List<int> result = FuzzyMatching.FindBestFuzzyMatch(string.Empty, "hello");
            List<int> expected = new List<int>();

            Assert.IsTrue(IsEqual(expected, result));
        }

        [TestMethod]
        public void ZeroLengthInputs()
        {
            List<int> result = FuzzyMatching.FindBestFuzzyMatch(string.Empty, string.Empty);
            List<int> expected = new List<int>();

            Assert.IsTrue(IsEqual(expected, result));
        }

        [TestMethod]
        public void BestMatch()
        {
            List<int> result = FuzzyMatching.FindBestFuzzyMatch("aaacaab", "ab");
            List<int> expected = new List<int>() { 5, 6 };

            Assert.IsTrue(IsEqual(expected, result));
        }

        [TestMethod]
        public void RealWorldProgramManager()
        {
            List<int> result = FuzzyMatching.FindBestFuzzyMatch("Program Manager", "pr");
            List<int> expected = new List<int>() { 0, 1 };

            Assert.IsTrue(IsEqual(expected, result));
        }

        [TestMethod]
        public void BestScoreTest()
        {
            int score = FuzzyMatching.CalculateScoreForMatches(new List<int>() { 1, 2, 3, 4 });
            Assert.IsTrue(score == -3);
        }

        private static bool IsEqual(List<int> list1, List<int> list2)
        {
            if (list1.Count != list2.Count)
            {
                return false;
            }

            for (int i = 0; i < list1.Count; i++)
            {
                if (list1[i] != list2[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
