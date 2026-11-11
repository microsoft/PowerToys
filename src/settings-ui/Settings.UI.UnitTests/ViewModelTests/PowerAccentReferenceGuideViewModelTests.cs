// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests
{
    [OSCondition(OperatingSystems.Windows)]
    [TestClass]
    public sealed class PowerAccentReferenceGuideViewModelTests
    {
        [TestMethod]
        public void Constructor_SelectedLanguages_AppearInSelectedSetsGroup()
        {
            var viewModel = CreateViewModel(["FR"]);

            var selectedGroup = viewModel.FilteredGroups.First();

            Assert.AreEqual("Selected sets", selectedGroup.GroupHeader);
            Assert.Contains(language => language.DisplayName == "French" && language.IsSelected, selectedGroup.Languages);
        }

        [TestMethod]
        public void Constructor_UnselectedLanguages_RemainInLanguageGroups()
        {
            var viewModel = CreateViewModel(["FR"]);

            var languageGroup = viewModel.FilteredGroups.First(group => group.GroupHeader == "Language sets");

            Assert.Contains(language => language.DisplayName == "German" && !language.IsSelected, languageGroup.Languages);
        }

        [TestMethod]
        public void Constructor_NoSelectedLanguages_OmitsSelectedSetsGroup()
        {
            var viewModel = CreateViewModel([]);

            Assert.DoesNotContain(group => group.GroupHeader == "Selected sets", viewModel.FilteredGroups);
        }

        [TestMethod]
        public void Constructor_UnknownSelectedLanguageCode_IsIgnored()
        {
            var viewModel = CreateViewModel(["NotALanguage"]);

            Assert.DoesNotContain(group => group.GroupHeader == "Selected sets", viewModel.FilteredGroups);
        }

        [TestMethod]
        public void SearchQuery_FiltersGroupsToMatchingCharacters()
        {
            var viewModel = CreateViewModel([]);

            viewModel.SearchQuery = "ß";

            Assert.IsFalse(viewModel.IsEmpty);
            Assert.Contains(language => language.DisplayName == "German", viewModel.FilteredGroups.SelectMany(group => group.Languages));
            Assert.Contains(
                character => character.Value.Contains('ß'),
                viewModel.FilteredGroups
                .SelectMany(group => group.Languages)
                .SelectMany(language => language.KeyMappings)
                .SelectMany(mapping => mapping.Characters));
        }

        [TestMethod]
        public void SearchQuery_NoMatches_SetsIsEmpty()
        {
            var viewModel = CreateViewModel([]);

            viewModel.SearchQuery = "No matching character";

            Assert.IsTrue(viewModel.IsEmpty);
            Assert.IsEmpty(viewModel.FilteredGroups);
        }

        [TestMethod]
        public void SearchQuery_Cleared_RestoresAllGroups()
        {
            var viewModel = CreateViewModel([]);
            var originalCount = viewModel.FilteredGroups.Count;

            viewModel.SearchQuery = "ß";
            viewModel.SearchQuery = string.Empty;

            Assert.HasCount(originalCount, viewModel.FilteredGroups);
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
