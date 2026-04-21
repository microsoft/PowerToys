// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PowerAccent.Common.UnitTests;

[TestClass]
public sealed class CharacterMappingsTests
{
    // Every Language enum value must appear in All exactly once. If a value is missing,
    // GetCharacters will silently return no characters for that language. If it appears
    // more than once, the second entry is dead code.
    [TestMethod]
    public void All_ContainsEveryLanguageEnumValue_ExactlyOnce()
    {
        foreach (Language lang in Enum.GetValues<Language>())
        {
            var count = CharacterMappings.All.Count(e => e.Id == lang);
            Assert.AreEqual(
                1,
                count,
                $"Language.{lang} appears {count} time(s) in CharacterMappings.All — expected exactly 1.");
        }
    }

    // Every entry in All must have a non-empty Identifier. A blank identifier would
    // produce a malformed resource key (e.g. "QuickAccent_SelectedLanguage_") that
    // silently resolves to an empty string in the Settings UI.
    [TestMethod]
    public void All_EveryEntry_HasNonEmptyIdentifier()
    {
        foreach (var entry in CharacterMappings.All)
        {
            Assert.IsFalse(
                string.IsNullOrWhiteSpace(entry.Identifier),
                $"Language.{entry.Id} has a null or whitespace Identifier.");
        }
    }

    // Every entry in All must have a non-null Characters dictionary. A null would throw
    // at runtime inside GetCharacters.
    [TestMethod]
    public void All_EveryEntry_HasNonNullCharacters()
    {
        foreach (var entry in CharacterMappings.All)
        {
            Assert.IsNotNull(
                entry.Characters,
                $"Language.{entry.Id} has a null Characters dictionary.");
        }
    }

    // Every LanguageGroup enum value must be used by at least one entry in All. This
    // guards against a new group being added to the enum but forgotten in the data, which
    // would make it impossible to test or exercise that group path.
    [TestMethod]
    public void All_EveryLanguageGroupValue_IsUsedAtLeastOnce()
    {
        var usedGroups = CharacterMappings.All.Select(e => e.Group).ToHashSet();
        foreach (LanguageGroup group in Enum.GetValues<LanguageGroup>())
        {
            // UserDefined is reserved for future use and may not yet be populated.
            if (group == LanguageGroup.UserDefined)
            {
                continue;
            }

            Assert.IsTrue(
                usedGroups.Contains(group),
                $"LanguageGroup.{group} is defined in the enum but no entry in CharacterMappings.All uses it.");
        }
    }

    // GetCharacters with an empty language array must return an empty array without
    // throwing.
    [TestMethod]
    public void GetCharacters_EmptyLanguages_ReturnsEmpty()
    {
        var result = CharacterMappings.GetCharacters(LetterKey.VK_A, []);
        Assert.AreEqual(0, result.Length);
    }

    // GetCharacters with all languages must return a non-empty result for a key that is
    // mapped in at least one language (VK_A is mapped in the majority of languages).
    [TestMethod]
    public void GetCharacters_AllLanguages_ReturnsNonEmptyForCommonKey()
    {
        var allLangs = Enum.GetValues<Language>();
        var result = CharacterMappings.GetCharacters(LetterKey.VK_A, allLangs);
        Assert.IsTrue(result.Length > 0, "Expected at least one character for VK_A across all languages.");
    }

    // GetCharacters must deduplicate characters that appear in multiple languages.
    // If two languages both map VK_A to the same character, it should appear only once.
    [TestMethod]
    public void GetCharacters_DeduplicatesCharactersAcrossLanguages()
    {
        var allLangs = Enum.GetValues<Language>();
        var result = CharacterMappings.GetCharacters(LetterKey.VK_A, allLangs);
        var distinct = result.Distinct().ToArray();
        CollectionAssert.AreEquivalent(
            distinct,
            result,
            "GetCharacters returned duplicate characters. Results should be deduplicated.");
    }

    // Calling GetCharacters twice with all languages should return the same results,
    // confirming the cache path is consistent.
    [TestMethod]
    public void GetCharacters_AllLanguagesCachedResult_IsConsistent()
    {
        var allLangs = Enum.GetValues<Language>();
        var first = CharacterMappings.GetCharacters(LetterKey.VK_E, allLangs);
        var second = CharacterMappings.GetCharacters(LetterKey.VK_E, allLangs);
        CollectionAssert.AreEqual(first, second, "Cached and non-cached results for VK_E differ.");
    }

    // GetCharacters for a single language should return exactly that language's
    // characters for a key it maps. The test derives both the language and key from the
    // live data so it stays valid regardless of future mapping changes.
    [TestMethod]
    public void GetCharacters_SingleLanguage_ReturnsOnlyThatLanguagesCharacters()
    {
        var langInfo = CharacterMappings.All.First(l => l.Characters.Count > 0);
        var (key, expected) = langInfo.Characters.First();

        var result = CharacterMappings.GetCharacters(key, [langInfo.Id]);

        CollectionAssert.AreEquivalent(
            expected,
            result,
            $"GetCharacters for Language.{langInfo.Id} / LetterKey.{key} did not match the mapped characters.");
    }

    // GetCharacters for a key that is not mapped in a given language should return empty.
    // The test finds a language and an absent key from the live data so it stays valid
    // regardless of future mapping changes.
    [TestMethod]
    public void GetCharacters_UnmappedKey_ReturnsEmpty()
    {
        var allKeys = Enum.GetValues<LetterKey>().ToHashSet();
        var langInfo = CharacterMappings.All.First(l => allKeys.Except(l.Characters.Keys).Any());
        var absentKey = allKeys.Except(langInfo.Characters.Keys).First();

        var result = CharacterMappings.GetCharacters(absentKey, [langInfo.Id]);

        Assert.AreEqual(
            0,
            result.Length,
            $"Expected empty result for Language.{langInfo.Id} / LetterKey.{absentKey}, which has no mapping.");
    }
}
