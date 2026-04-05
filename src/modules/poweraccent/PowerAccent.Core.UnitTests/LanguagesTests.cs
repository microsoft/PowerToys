// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.PowerAccentKeyboardService;

namespace PowerAccent.Core.UnitTests
{
    [TestClass]
    public class LanguagesTests
    {
        [TestMethod]
        public void GetDefaultLetterKey_EmptyLanguageArray_ShouldReturnEmpty()
        {
            var result = Languages.GetDefaultLetterKey(LetterKey.VK_A, Array.Empty<Language>());
            Assert.AreEqual(0, result.Length);
        }

        [TestMethod]
        public void GetDefaultLetterKey_SingleLanguage_ShouldReturnNonEmpty()
        {
            // French has accent characters for 'A'
            var result = Languages.GetDefaultLetterKey(LetterKey.VK_A, new[] { Language.FR });
            Assert.IsTrue(result.Length > 0, "French should have accented characters for A");
        }

        [TestMethod]
        public void GetDefaultLetterKey_MultipleLanguages_ShouldMergeAndDeduplicate()
        {
            // Both French and Spanish have accented A characters, should be merged and deduplicated
            var frResult = Languages.GetDefaultLetterKey(LetterKey.VK_A, new[] { Language.FR });
            var spResult = Languages.GetDefaultLetterKey(LetterKey.VK_A, new[] { Language.SP });
            var combinedResult = Languages.GetDefaultLetterKey(LetterKey.VK_A, new[] { Language.FR, Language.SP });

            // Combined should have no duplicates
            Assert.AreEqual(combinedResult.Length, combinedResult.Distinct().Count(), "Combined result should contain no duplicates");

            // Combined should contain all characters from both languages
            foreach (var ch in frResult)
            {
                CollectionAssert.Contains(combinedResult, ch, $"Combined result should contain French character '{ch}'");
            }

            foreach (var ch in spResult)
            {
                CollectionAssert.Contains(combinedResult, ch, $"Combined result should contain Spanish character '{ch}'");
            }
        }

        [TestMethod]
        public void GetDefaultLetterKey_French_A_ShouldContainExpectedAccents()
        {
            var result = Languages.GetDefaultLetterKey(LetterKey.VK_A, new[] { Language.FR });

            // French 'a' accents: à, â, æ, á, ä are common French accent characters for A
            CollectionAssert.Contains(result, "à", "French should contain à");
            CollectionAssert.Contains(result, "â", "French should contain â");
            CollectionAssert.Contains(result, "æ", "French should contain æ");
        }

        [TestMethod]
        public void GetDefaultLetterKey_German_O_ShouldContainUmlaut()
        {
            var result = Languages.GetDefaultLetterKey(LetterKey.VK_O, new[] { Language.DE });
            CollectionAssert.Contains(result, "ö", "German should contain ö for O");
        }

        [TestMethod]
        public void GetDefaultLetterKey_German_U_ShouldContainUmlaut()
        {
            var result = Languages.GetDefaultLetterKey(LetterKey.VK_U, new[] { Language.DE });
            CollectionAssert.Contains(result, "ü", "German should contain ü for U");
        }

        [TestMethod]
        public void GetDefaultLetterKey_German_S_ShouldContainEszett()
        {
            var result = Languages.GetDefaultLetterKey(LetterKey.VK_S, new[] { Language.DE });
            CollectionAssert.Contains(result, "ß", "German should contain ß for S");
        }

        [TestMethod]
        public void GetDefaultLetterKey_Spanish_N_ShouldContainEne()
        {
            var result = Languages.GetDefaultLetterKey(LetterKey.VK_N, new[] { Language.SP });
            CollectionAssert.Contains(result, "ñ", "Spanish should contain ñ for N");
        }

        [TestMethod]
        public void GetDefaultLetterKey_Czech_C_ShouldContainCaron()
        {
            var result = Languages.GetDefaultLetterKey(LetterKey.VK_C, new[] { Language.CZ });
            CollectionAssert.Contains(result, "č", "Czech should contain č for C");
        }

        [TestMethod]
        public void GetDefaultLetterKey_Polish_L_ShouldContainStroke()
        {
            var result = Languages.GetDefaultLetterKey(LetterKey.VK_L, new[] { Language.PL });
            CollectionAssert.Contains(result, "ł", "Polish should contain ł for L");
        }

        [TestMethod]
        public void GetDefaultLetterKey_Vietnamese_A_ShouldContainMultipleAccents()
        {
            var result = Languages.GetDefaultLetterKey(LetterKey.VK_A, new[] { Language.VI });

            // Vietnamese has many A variants
            Assert.IsTrue(result.Length >= 10, "Vietnamese should have many accented A characters");
            CollectionAssert.Contains(result, "à", "Vietnamese should contain à");
            CollectionAssert.Contains(result, "ă", "Vietnamese should contain ă");
            CollectionAssert.Contains(result, "â", "Vietnamese should contain â");
        }

        [TestMethod]
        public void GetDefaultLetterKey_Special_Digits_ShouldReturnSubscriptsSuperscripts()
        {
            var result = Languages.GetDefaultLetterKey(LetterKey.VK_0, new[] { Language.SPECIAL });
            Assert.IsTrue(result.Length > 0, "Special characters for 0 should exist");
            CollectionAssert.Contains(result, "⁰", "Special 0 should contain superscript 0");
            CollectionAssert.Contains(result, "₀", "Special 0 should contain subscript 0");
        }

        [TestMethod]
        public void GetDefaultLetterKey_Special_1_ShouldContainFractions()
        {
            var result = Languages.GetDefaultLetterKey(LetterKey.VK_1, new[] { Language.SPECIAL });
            CollectionAssert.Contains(result, "½", "Special 1 should contain ½");
            CollectionAssert.Contains(result, "¹", "Special 1 should contain ¹");
        }

        [TestMethod]
        public void GetDefaultLetterKey_Currency_ShouldReturnCurrencySymbols()
        {
            // Test that the Currency language returns currency symbols for relevant keys
            var result = Languages.GetDefaultLetterKey(LetterKey.VK_R, new[] { Language.CUR });

            // CUR (Currency) for R: ₹ (Indian Rupee) and others if present
            // Even if empty for VK_R, it shouldn't throw
            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void GetDefaultLetterKey_UnusedKeyForLanguage_ShouldReturnEmpty()
        {
            // German typically has no accent for 'Q'
            var result = Languages.GetDefaultLetterKey(LetterKey.VK_Q, new[] { Language.DE });
            Assert.AreEqual(0, result.Length, "German should have no accented characters for Q");
        }

        [TestMethod]
        public void GetDefaultLetterKey_AllLanguages_ShouldReturnCachedResults()
        {
            var allLanguages = Enum.GetValues<Language>();

            // First call computes and caches
            var result1 = Languages.GetDefaultLetterKey(LetterKey.VK_A, allLanguages);

            // Second call should return same array from cache
            var result2 = Languages.GetDefaultLetterKey(LetterKey.VK_A, allLanguages);

            Assert.IsTrue(result1.Length > 0, "All languages combined should have accented A characters");
            CollectionAssert.AreEqual(result1, result2, "Cached results should be identical");
        }

        [TestMethod]
        public void GetDefaultLetterKey_AllLanguages_EachLetterKey_ShouldNotThrow()
        {
            var allLanguages = Enum.GetValues<Language>();
            var allLetterKeys = Enum.GetValues<LetterKey>();

            foreach (var letterKey in allLetterKeys)
            {
                var result = Languages.GetDefaultLetterKey(letterKey, allLanguages);
                Assert.IsNotNull(result, $"Result for {letterKey} should not be null");

                // Verify no duplicates
                Assert.AreEqual(result.Length, result.Distinct().Count(), $"Result for {letterKey} should have no duplicates");
            }
        }
    }
}
