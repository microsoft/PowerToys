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
        /// <summary>
        /// Product code: Languages.GetDefaultLetterKey
        /// What: Verifies that passing an empty language array returns an empty result
        /// Why: Guards against NullReferenceException or incorrect fallback when no languages are configured
        /// </summary>
        [TestMethod]
        public void GetDefaultLetterKey_EmptyLanguageArray_ShouldReturnEmpty()
        {
            var result = Languages.GetDefaultLetterKey(LetterKey.VK_A, Array.Empty<Language>());
            Assert.AreEqual(0, result.Length);
        }

        /// <summary>
        /// Product code: Languages.GetDefaultLetterKey with Language.FR
        /// What: Verifies that French returns at least one accented character for the letter A
        /// Why: Regression guard — ensures the French language mapping is wired up and non-empty
        /// </summary>
        [TestMethod]
        public void GetDefaultLetterKey_SingleLanguage_ShouldReturnNonEmpty()
        {
            var result = Languages.GetDefaultLetterKey(LetterKey.VK_A, new[] { Language.FR });
            Assert.IsTrue(result.Length > 0, "French should have accented characters for A");
        }

        /// <summary>
        /// Product code: Languages.GetDefaultLetterKey with multiple languages
        /// What: Verifies that combining French and Spanish merges characters and removes duplicates
        /// Why: Guards against duplicate entries when languages share accent characters (e.g., à)
        /// </summary>
        [TestMethod]
        public void GetDefaultLetterKey_MultipleLanguages_ShouldMergeAndDeduplicate()
        {
            var frResult = Languages.GetDefaultLetterKey(LetterKey.VK_A, new[] { Language.FR });
            var spResult = Languages.GetDefaultLetterKey(LetterKey.VK_A, new[] { Language.SP });
            var combinedResult = Languages.GetDefaultLetterKey(LetterKey.VK_A, new[] { Language.FR, Language.SP });

            Assert.AreEqual(combinedResult.Length, combinedResult.Distinct().Count(), "Combined result should contain no duplicates");

            foreach (var ch in frResult)
            {
                CollectionAssert.Contains(combinedResult, ch, $"Combined result should contain French character '{ch}'");
            }

            foreach (var ch in spResult)
            {
                CollectionAssert.Contains(combinedResult, ch, $"Combined result should contain Spanish character '{ch}'");
            }
        }

        /// <summary>
        /// Product code: Languages.GetDefaultLetterKey with Language.FR for VK_A
        /// What: Verifies that French returns specific expected accent characters (à, â, æ)
        /// Why: Pinpoints exact character expectations — catches silent data regressions in language tables
        /// </summary>
        [TestMethod]
        public void GetDefaultLetterKey_French_A_ShouldContainExpectedAccents()
        {
            var result = Languages.GetDefaultLetterKey(LetterKey.VK_A, new[] { Language.FR });

            CollectionAssert.Contains(result, "à", "French should contain à");
            CollectionAssert.Contains(result, "â", "French should contain â");
            CollectionAssert.Contains(result, "æ", "French should contain æ");
        }

        /// <summary>
        /// Product code: Languages.GetDefaultLetterKey with Language.DE for VK_O
        /// What: Verifies German returns the ö umlaut for the O key
        /// Why: Core German character — regression would break German users' workflow
        /// </summary>
        [TestMethod]
        public void GetDefaultLetterKey_German_O_ShouldContainUmlaut()
        {
            var result = Languages.GetDefaultLetterKey(LetterKey.VK_O, new[] { Language.DE });
            CollectionAssert.Contains(result, "ö", "German should contain ö for O");
        }

        /// <summary>
        /// Product code: Languages.GetDefaultLetterKey with Language.DE for VK_U
        /// What: Verifies German returns the ü umlaut for the U key
        /// Why: Core German character — regression would break German users' workflow
        /// </summary>
        [TestMethod]
        public void GetDefaultLetterKey_German_U_ShouldContainUmlaut()
        {
            var result = Languages.GetDefaultLetterKey(LetterKey.VK_U, new[] { Language.DE });
            CollectionAssert.Contains(result, "ü", "German should contain ü for U");
        }

        /// <summary>
        /// Product code: Languages.GetDefaultLetterKey with Language.DE for VK_S
        /// What: Verifies German returns the ß (Eszett) for the S key
        /// Why: ß is unique to German — its absence would be a critical language regression
        /// </summary>
        [TestMethod]
        public void GetDefaultLetterKey_German_S_ShouldContainEszett()
        {
            var result = Languages.GetDefaultLetterKey(LetterKey.VK_S, new[] { Language.DE });
            CollectionAssert.Contains(result, "ß", "German should contain ß for S");
        }

        /// <summary>
        /// Product code: Languages.GetDefaultLetterKey with Language.SP for VK_N
        /// What: Verifies Spanish returns the ñ character for the N key
        /// Why: ñ is the most recognizable Spanish-specific character
        /// </summary>
        [TestMethod]
        public void GetDefaultLetterKey_Spanish_N_ShouldContainEne()
        {
            var result = Languages.GetDefaultLetterKey(LetterKey.VK_N, new[] { Language.SP });
            CollectionAssert.Contains(result, "ñ", "Spanish should contain ñ for N");
        }

        /// <summary>
        /// Product code: Languages.GetDefaultLetterKey with Language.CZ for VK_C
        /// What: Verifies Czech returns the č (c-caron) for the C key
        /// Why: č is essential for Czech — its absence would break Czech language support
        /// </summary>
        [TestMethod]
        public void GetDefaultLetterKey_Czech_C_ShouldContainCaron()
        {
            var result = Languages.GetDefaultLetterKey(LetterKey.VK_C, new[] { Language.CZ });
            CollectionAssert.Contains(result, "č", "Czech should contain č for C");
        }

        /// <summary>
        /// Product code: Languages.GetDefaultLetterKey with Language.PL for VK_L
        /// What: Verifies Polish returns the ł (L with stroke) for the L key
        /// Why: ł is fundamental to Polish — its absence would break Polish language support
        /// </summary>
        [TestMethod]
        public void GetDefaultLetterKey_Polish_L_ShouldContainStroke()
        {
            var result = Languages.GetDefaultLetterKey(LetterKey.VK_L, new[] { Language.PL });
            CollectionAssert.Contains(result, "ł", "Polish should contain ł for L");
        }

        /// <summary>
        /// Product code: Languages.GetDefaultLetterKey with Language.VI for VK_A
        /// What: Verifies Vietnamese returns a rich set of A variants (≥10 accented forms)
        /// Why: Vietnamese has extensive diacritics — guards against truncation of the character table
        /// </summary>
        [TestMethod]
        public void GetDefaultLetterKey_Vietnamese_A_ShouldContainMultipleAccents()
        {
            var result = Languages.GetDefaultLetterKey(LetterKey.VK_A, new[] { Language.VI });

            Assert.IsTrue(result.Length >= 10, "Vietnamese should have many accented A characters");
            CollectionAssert.Contains(result, "à", "Vietnamese should contain à");
            CollectionAssert.Contains(result, "ă", "Vietnamese should contain ă");
            CollectionAssert.Contains(result, "â", "Vietnamese should contain â");
        }

        /// <summary>
        /// Product code: Languages.GetDefaultLetterKey with Language.SPECIAL for VK_0
        /// What: Verifies that digit 0 returns superscript (⁰) and subscript (₀) variants
        /// Why: Special characters for digits are used in mathematical/scientific input
        /// </summary>
        [TestMethod]
        public void GetDefaultLetterKey_Special_Digits_ShouldReturnSubscriptsSuperscripts()
        {
            var result = Languages.GetDefaultLetterKey(LetterKey.VK_0, new[] { Language.SPECIAL });
            Assert.IsTrue(result.Length > 0, "Special characters for 0 should exist");
            CollectionAssert.Contains(result, "⁰", "Special 0 should contain superscript 0");
            CollectionAssert.Contains(result, "₀", "Special 0 should contain subscript 0");
        }

        /// <summary>
        /// Product code: Languages.GetDefaultLetterKey with Language.SPECIAL for VK_1
        /// What: Verifies that digit 1 returns fraction (½) and superscript (¹) variants
        /// Why: Fraction characters are commonly needed for recipes, measurements, etc.
        /// </summary>
        [TestMethod]
        public void GetDefaultLetterKey_Special_1_ShouldContainFractions()
        {
            var result = Languages.GetDefaultLetterKey(LetterKey.VK_1, new[] { Language.SPECIAL });
            CollectionAssert.Contains(result, "½", "Special 1 should contain ½");
            CollectionAssert.Contains(result, "¹", "Special 1 should contain ¹");
        }

        /// <summary>
        /// Product code: Languages.GetDefaultLetterKey with Language.CUR
        /// What: Verifies that Currency language returns well-known currency symbols (€, $, £)
        /// Why: Currency symbols are the primary use case — tests exact symbol presence, not just non-null
        /// </summary>
        [TestMethod]
        public void GetDefaultLetterKey_Currency_ShouldReturnCurrencySymbols()
        {
            var euroResult = Languages.GetDefaultLetterKey(LetterKey.VK_E, new[] { Language.CUR });
            CollectionAssert.Contains(euroResult, "€", "Currency VK_E should contain Euro symbol");

            var dollarResult = Languages.GetDefaultLetterKey(LetterKey.VK_S, new[] { Language.CUR });
            CollectionAssert.Contains(dollarResult, "$", "Currency VK_S should contain Dollar symbol");

            var poundResult = Languages.GetDefaultLetterKey(LetterKey.VK_P, new[] { Language.CUR });
            CollectionAssert.Contains(poundResult, "£", "Currency VK_P should contain Pound symbol");
        }

        /// <summary>
        /// Product code: Languages.GetDefaultLetterKey with Language.DE for VK_Q
        /// What: Verifies that a key with no accented characters for a language returns empty
        /// Why: Guards against spurious characters appearing for keys that should have no accents
        /// </summary>
        [TestMethod]
        public void GetDefaultLetterKey_UnusedKeyForLanguage_ShouldReturnEmpty()
        {
            var result = Languages.GetDefaultLetterKey(LetterKey.VK_Q, new[] { Language.DE });
            Assert.AreEqual(0, result.Length, "German should have no accented characters for Q");
        }

        /// <summary>
        /// Product code: Languages.GetDefaultLetterKeyALL (cache path via ConcurrentDictionary)
        /// What: Verifies that calling with all languages twice returns the same cached array instance
        /// Why: Cache hit should return the identical object reference, not a recomputed copy
        /// </summary>
        [TestMethod]
        public void GetDefaultLetterKey_AllLanguages_ShouldReturnCachedResults()
        {
            var allLanguages = Enum.GetValues<Language>();

            var result1 = Languages.GetDefaultLetterKey(LetterKey.VK_A, allLanguages);
            var result2 = Languages.GetDefaultLetterKey(LetterKey.VK_A, allLanguages);

            Assert.IsTrue(result1.Length > 0, "All languages combined should have accented A characters");
            CollectionAssert.AreEqual(result1, result2, "Cached results should be identical");

            // Verify cache hit returns same object instance, not just equal contents
            Assert.AreSame(result1, result2, "Second call should return cached reference");
        }

        /// <summary>
        /// Product code: Languages.GetDefaultLetterKey across all LetterKey × Language combinations
        /// What: Smoke-tests every letter key with all languages to ensure no exceptions are thrown
        /// Why: Catches missing switch arms or null returns that would crash the toolbar at runtime
        /// </summary>
        [TestMethod]
        public void GetDefaultLetterKey_AllLanguages_EachLetterKey_ShouldNotThrow()
        {
            var allLanguages = Enum.GetValues<Language>();
            var allLetterKeys = Enum.GetValues<LetterKey>();

            foreach (var letterKey in allLetterKeys)
            {
                var result = Languages.GetDefaultLetterKey(letterKey, allLanguages);
                Assert.IsNotNull(result, $"Result for {letterKey} should not be null");

                Assert.AreEqual(result.Length, result.Distinct().Count(), $"Result for {letterKey} should have no duplicates");
            }
        }
    }
}
