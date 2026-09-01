// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO.Abstractions.TestingHelpers;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace ViewModelTests
{
    [TestClass]
    public class KeyboardManager
    {
        public const string Module = KeyboardManagerSettings.ModuleName;

        [TestInitialize]
        public void Setup()
        {
        }

        [TestCleanup]
        public void CleanUp()
        {
        }

        [TestMethod]
        public void CombineShortcutListsShouldReturnEmptyListWhenBothArgumentsAreEmptyLists()
        {
            // arrange
            var firstList = new List<KeysDataModel>();
            var secondList = new List<AppSpecificKeysDataModel>();

            // act
            var result = KeyboardManagerViewModel.CombineShortcutLists(firstList, secondList);

            // Assert
            var expectedResult = new List<AppSpecificKeysDataModel>();

            Assert.AreEqual(expectedResult.Count, result.Count);
        }

        [TestMethod]
        public void CombineShortcutListsShouldReturnEmptyListWhenBothArgumentsAreNull()
        {
            // act
            var result = KeyboardManagerViewModel.CombineShortcutLists(null, null);

            // Assert
            var expectedResult = new List<AppSpecificKeysDataModel>();

            Assert.AreEqual(expectedResult.Count, result.Count);
        }

        [TestMethod]
        public void CombineShortcutListsShouldReturnListWithOneAppSpecificEntryWhenFirstArgumentIsNullAndSecondArgumentHasOneEntry()
        {
            // arrange
            var secondList = new List<AppSpecificKeysDataModel>();
            var entry = new AppSpecificKeysDataModel();
            entry.OriginalKeys = "17;65";
            entry.NewRemapKeys = "17;86";
            entry.TargetApp = "msedge";
            secondList.Add(entry);

            // act
            var result = KeyboardManagerViewModel.CombineShortcutLists(null, secondList);

            // Assert
            var expectedResult = new List<AppSpecificKeysDataModel>();
            var expectedEntry = new AppSpecificKeysDataModel();
            expectedEntry.OriginalKeys = entry.OriginalKeys;
            expectedEntry.NewRemapKeys = entry.NewRemapKeys;
            expectedEntry.TargetApp = entry.TargetApp;
            expectedResult.Add(expectedEntry);

            Assert.AreEqual(expectedResult.Count, result.Count);
            Assert.IsTrue(expectedResult[0].Compare(result[0]));
        }

        [TestMethod]
        public void CombineShortcutListsShouldReturnListWithOneAllAppsEntryWhenFirstArgumentHasOneEntryAndSecondArgumentIsNull()
        {
            // arrange
            var firstList = new List<KeysDataModel>();
            var entry = new KeysDataModel();
            entry.OriginalKeys = "17;65";
            entry.NewRemapKeys = "17;86";
            firstList.Add(entry);

            // act
            var result = KeyboardManagerViewModel.CombineShortcutLists(firstList, null);

            // Assert
            var expectedResult = new List<AppSpecificKeysDataModel>();
            var expectedEntry = new AppSpecificKeysDataModel();
            expectedEntry.OriginalKeys = entry.OriginalKeys;
            expectedEntry.NewRemapKeys = entry.NewRemapKeys;
            expectedEntry.TargetApp = "All apps";
            expectedResult.Add(expectedEntry);

            Assert.AreEqual(expectedResult.Count, result.Count);
            Assert.IsTrue(expectedResult[0].Compare(result[0]));
        }

        [TestMethod]
        public void CombineShortcutListsShouldReturnListWithOneAllAppsEntryWhenFirstArgumentHasOneEntryAndSecondArgumentIsEmpty()
        {
            // arrange
            var firstList = new List<KeysDataModel>();
            var entry = new KeysDataModel();
            entry.OriginalKeys = "17;65";
            entry.NewRemapKeys = "17;86";
            firstList.Add(entry);
            var secondList = new List<AppSpecificKeysDataModel>();

            // act
            var result = KeyboardManagerViewModel.CombineShortcutLists(firstList, secondList);

            // Assert
            var expectedResult = new List<AppSpecificKeysDataModel>();
            var expectedEntry = new AppSpecificKeysDataModel();
            expectedEntry.OriginalKeys = entry.OriginalKeys;
            expectedEntry.NewRemapKeys = entry.NewRemapKeys;
            expectedEntry.TargetApp = "All apps";
            expectedResult.Add(expectedEntry);
            var x = expectedResult[0].Equals(result[0]);
            Assert.AreEqual(expectedResult.Count, result.Count);
            Assert.IsTrue(expectedResult[0].Compare(result[0]));
        }

        [TestMethod]
        public void CombineShortcutListsShouldReturnListWithOneAppSpecificEntryWhenFirstArgumentIsEmptyAndSecondArgumentHasOneEntry()
        {
            // arrange
            var firstList = new List<KeysDataModel>();
            var secondList = new List<AppSpecificKeysDataModel>();
            var entry = new AppSpecificKeysDataModel();
            entry.OriginalKeys = "17;65";
            entry.NewRemapKeys = "17;86";
            entry.TargetApp = "msedge";
            secondList.Add(entry);

            // act
            var result = KeyboardManagerViewModel.CombineShortcutLists(firstList, secondList);

            // Assert
            var expectedResult = new List<AppSpecificKeysDataModel>();
            var expectedEntry = new AppSpecificKeysDataModel();
            expectedEntry.OriginalKeys = entry.OriginalKeys;
            expectedEntry.NewRemapKeys = entry.NewRemapKeys;
            expectedEntry.TargetApp = entry.TargetApp;
            expectedResult.Add(expectedEntry);

            Assert.AreEqual(expectedResult.Count, result.Count);
            Assert.IsTrue(expectedResult[0].Compare(result[0]));
        }

        [TestMethod]
        public void CombineShortcutListsShouldReturnListWithOneAllAppsEntryAndOneAppSpecificEntryWhenFirstArgumentHasOneEntryAndSecondArgumentHasOneEntry()
        {
            // arrange
            var firstList = new List<KeysDataModel>();
            var firstListEntry = new KeysDataModel();
            firstListEntry.OriginalKeys = "17;65";
            firstListEntry.NewRemapKeys = "17;86";
            firstList.Add(firstListEntry);
            var secondList = new List<AppSpecificKeysDataModel>();
            var secondListEntry = new AppSpecificKeysDataModel();
            secondListEntry.OriginalKeys = "17;66";
            secondListEntry.NewRemapKeys = "17;87";
            secondListEntry.TargetApp = "msedge";
            secondList.Add(secondListEntry);

            // act
            var result = KeyboardManagerViewModel.CombineShortcutLists(firstList, secondList);

            // Assert
            var expectedResult = new List<AppSpecificKeysDataModel>();
            var expectedFirstEntry = new AppSpecificKeysDataModel();
            expectedFirstEntry.OriginalKeys = firstListEntry.OriginalKeys;
            expectedFirstEntry.NewRemapKeys = firstListEntry.NewRemapKeys;
            expectedFirstEntry.TargetApp = "All apps";
            expectedResult.Add(expectedFirstEntry);
            var expectedSecondEntry = new AppSpecificKeysDataModel();
            expectedSecondEntry.OriginalKeys = secondListEntry.OriginalKeys;
            expectedSecondEntry.NewRemapKeys = secondListEntry.NewRemapKeys;
            expectedSecondEntry.TargetApp = secondListEntry.TargetApp;
            expectedResult.Add(expectedSecondEntry);

            Assert.AreEqual(expectedResult.Count, result.Count);
            Assert.IsTrue(expectedResult[0].Compare(result[0]));
            Assert.IsTrue(expectedResult[1].Compare(result[1]));
        }

        [TestMethod]
        public void FilterRemapKeysList_ShouldPreserveConditionWhenCombiningModifierSides()
        {
            var remaps = new List<KeysDataModel>
            {
                new() { OriginalKeys = "162", NewRemapKeys = "65", Condition = "alone" },
                new() { OriginalKeys = "163", NewRemapKeys = "65", Condition = "alone" },
            };

            KeyboardManagerPage.FilterRemapKeysList(remaps);

            Assert.AreEqual(1, remaps.Count);
            Assert.AreEqual("17", remaps[0].OriginalKeys);
            Assert.AreEqual("65", remaps[0].NewRemapKeys);
            Assert.AreEqual("alone", remaps[0].Condition);
        }

        [TestMethod]
        public void FilterRemapKeysList_ShouldNotCombineDifferentConditions()
        {
            var remaps = new List<KeysDataModel>
            {
                new() { OriginalKeys = "162", NewRemapKeys = "65" },
                new() { OriginalKeys = "163", NewRemapKeys = "65", Condition = "alone" },
            };

            KeyboardManagerPage.FilterRemapKeysList(remaps);

            Assert.AreEqual(2, remaps.Count);
        }

        [TestMethod]
        public void LoadProfile_InvalidTextExpansionEnabledType_DoesNotOverwriteProfile()
        {
            const string Profile = """
                {
                  "textReplacements": {
                    "inProcess": [
                      {
                        "id": "11111111-1111-4111-8111-111111111111",
                        "sourceText": "brb",
                        "activationKeys": [32],
                        "replacementText": "be right back",
                        "enabled": "true"
                      }
                    ]
                  }
                }
                """;

            AssertInvalidProfileIsNotOverwritten(Profile);
        }

        [TestMethod]
        public void LoadProfile_InvalidTextExpansionActivationKeyType_DoesNotOverwriteProfile()
        {
            const string Profile = """
                {
                  "textReplacements": {
                    "inProcess": [
                      {
                        "id": "11111111-1111-4111-8111-111111111111",
                        "sourceText": "brb",
                        "activationKeys": ["32"],
                        "replacementText": "be right back",
                        "enabled": true
                      }
                    ]
                  }
                }
                """;

            AssertInvalidProfileIsNotOverwritten(Profile);
        }

        [TestMethod]
        public void LoadProfile_InvalidSingleKeyConditionType_DoesNotOverwriteProfile()
        {
            const string Profile = """
                {
                  "remapKeys": {
                    "inProcess": [
                      {
                        "originalKeys": "65",
                        "newRemapKeys": "66",
                        "condition": 1
                      }
                    ]
                  }
                }
                """;

            AssertInvalidProfileIsNotOverwritten(Profile);
        }

        private static void AssertInvalidProfileIsNotOverwritten(string profileContents)
        {
            var fileSystem = new MockFileSystem();
            var settingsUtils = new SettingsUtils(fileSystem);
            settingsUtils.SaveSettings(new KeyboardManagerSettings().ToJsonString(), Module);

            const string ProfileFileName = "default.json";
            string profilePath = settingsUtils.GetSettingsFilePath(Module, ProfileFileName);
            fileSystem.File.WriteAllText(profilePath, profileContents);

            var generalSettingsRepository = new Mock<ISettingsRepository<GeneralSettings>>();
            generalSettingsRepository.SetupGet(repository => repository.SettingsConfig).Returns(new GeneralSettings());

            var viewModel = new KeyboardManagerViewModel(
                settingsUtils,
                generalSettingsRepository.Object,
                _ => 0,
                _ => 0);

            Assert.AreEqual(profileContents, fileSystem.File.ReadAllText(profilePath));
            Assert.IsFalse(viewModel.LoadProfile());
            Assert.AreEqual(profileContents, fileSystem.File.ReadAllText(profilePath));
        }
    }
}
