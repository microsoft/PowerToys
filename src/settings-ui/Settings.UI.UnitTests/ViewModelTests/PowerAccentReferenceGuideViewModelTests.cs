// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests
{
    [TestClass]
    public sealed class PowerAccentReferenceGuideViewModelTests
    {
        [TestMethod]
        public void Constructor_SelectedLanguages_AppearInSelectedSetsGroup()
        {
            var viewModel = CreateViewModel(["FR"]);

            var selectedGroup = viewModel.FilteredGroups.First();

            Assert.AreEqual("Selected sets", selectedGroup.GroupHeader);
            Assert.IsTrue(selectedGroup.Languages.Any(language => language.DisplayName == "French" && language.IsSelected));
        }

        [TestMethod]
        public void Constructor_UnselectedLanguages_RemainInLanguageGroups()
        {
            var viewModel = CreateViewModel(["FR"]);

            var languageGroup = viewModel.FilteredGroups.First(group => group.GroupHeader == "Language sets");

            Assert.IsTrue(languageGroup.Languages.Any(language => language.DisplayName == "German" && !language.IsSelected));
        }

        [TestMethod]
        public void Constructor_NoSelectedLanguages_OmitsSelectedSetsGroup()
        {
            var viewModel = CreateViewModel([]);

            Assert.IsFalse(viewModel.FilteredGroups.Any(group => group.GroupHeader == "Selected sets"));
        }

        [TestMethod]
        public void Constructor_UnknownSelectedLanguageCode_IsIgnored()
        {
            var viewModel = CreateViewModel(["NotALanguage"]);

            Assert.IsFalse(viewModel.FilteredGroups.Any(group => group.GroupHeader == "Selected sets"));
        }

        [TestMethod]
        public void SearchQuery_FiltersGroupsToMatchingCharacters()
        {
            var viewModel = CreateViewModel([]);

            viewModel.SearchQuery = "ß";

            Assert.IsFalse(viewModel.IsEmpty);
            Assert.IsTrue(viewModel.FilteredGroups.SelectMany(group => group.Languages).Any(language => language.DisplayName == "German"));
            Assert.IsTrue(viewModel.FilteredGroups
                .SelectMany(group => group.Languages)
                .SelectMany(language => language.KeyMappings)
                .SelectMany(mapping => mapping.Characters)
                .Any(character => character.Value.Contains('ß')));
        }

        [TestMethod]
        public void SearchQuery_NoMatches_SetsIsEmpty()
        {
            var viewModel = CreateViewModel([]);

            viewModel.SearchQuery = "No matching character";

            Assert.IsTrue(viewModel.IsEmpty);
            Assert.AreEqual(0, viewModel.FilteredGroups.Count);
        }

        [TestMethod]
        public void SearchQuery_Cleared_RestoresAllGroups()
        {
            var viewModel = CreateViewModel([]);
            var originalCount = viewModel.FilteredGroups.Count;

            viewModel.SearchQuery = "ß";
            viewModel.SearchQuery = string.Empty;

            Assert.AreEqual(originalCount, viewModel.FilteredGroups.Count);
        }

        [TestMethod]
        public void CharacterModel_CombiningMark_UsesDottedCircleDisplayValue()
        {
            var model = new CharacterModel("\u0301");

            Assert.AreEqual("\u0301", model.Value);
            Assert.AreEqual("◌\u0301", model.DisplayValue);
        }

        [TestMethod]
        public void CharacterModel_BaseCharacterWithCombiningMark_UsesOriginalDisplayValue()
        {
            var model = new CharacterModel("y\u0300");

            Assert.AreEqual("y\u0300", model.Value);
            Assert.AreEqual("y\u0300", model.DisplayValue);
        }

        private static PowerAccentReferenceGuideViewModel CreateViewModel(string[] selectedLanguageCodes)
        {
            return new PowerAccentReferenceGuideViewModel(selectedLanguageCodes, GetLocalizedString);
        }

        private static string GetLocalizedString(string resourceId)
        {
            return resourceId switch
            {
                "QuickAccent_ReferenceGuide_SelectedSets" => "Selected sets",
                "QuickAccent_Group_Language" => "Language sets",
                "QuickAccent_Group_Special" => "Special sets",
                "QuickAccent_Group_UserDefined" => "User-defined sets",
                "QuickAccent_SelectedLanguage_French" => "French",
                "QuickAccent_SelectedLanguage_German" => "German",
                _ when resourceId.StartsWith("QuickAccent_SelectedLanguage_", System.StringComparison.Ordinal) => resourceId["QuickAccent_SelectedLanguage_".Length..],
                _ => resourceId,
            };
        }
    }
}
