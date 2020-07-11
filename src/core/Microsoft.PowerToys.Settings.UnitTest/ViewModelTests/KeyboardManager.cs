using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Windows.System;

namespace ViewModelTests
{
    [TestClass]
    public class KeyboardManager
    {
        public const string Module = "Keyboard Manager";

        [TestInitialize]
        public void Setup()
        { }

        [TestCleanup]
        public void CleanUp()
        { }

        [TestMethod]
        public void CombineShortcutLists_ShouldReturnEmptyList_WhenBothArgumentsAreEmptyLists()
        {
            // arrange
            var firstList = new List<KeysDataModel>();
            var secondList = new List<AppSpecificKeysDataModel>();

            // act
            var result = KeyboardManagerViewModel.CombineShortcutLists(firstList, secondList);

            // Assert
            var expectedResult = new List<AppSpecificKeysDataModel>();

            Assert.AreEqual(expectedResult.Count(), result.Count());
        }

        [TestMethod]
        public void CombineShortcutLists_ShouldReturnListWithOneAllAppsEntry_WhenFirstArgumentHasOneEntryAndSecondArgumentIsEmpty()
        {
            // arrange
            var firstList = new List<KeysDataModel>();
            var entry = new KeysDataModel();
            entry.OriginalKeys = VirtualKey.Control + ";" + VirtualKey.A;
            entry.NewRemapKeys = VirtualKey.Control + ";" + VirtualKey.V;
            firstList.Add(entry);
            var secondList = new List<AppSpecificKeysDataModel>();

            // act
            var result = KeyboardManagerViewModel.CombineShortcutLists(firstList, secondList);

            // Assert
            var expectedResult = new List<AppSpecificKeysDataModel>();
            var expectedEntry = new AppSpecificKeysDataModel();
            expectedEntry.OriginalKeys = entry.OriginalKeys;
            expectedEntry.NewRemapKeys = entry.NewRemapKeys;
            expectedEntry.TargetApp = "All Apps";
            expectedResult.Add(expectedEntry);
            var x = expectedResult[0].Equals(result[0]);
            Assert.AreEqual(expectedResult.Count(), result.Count());
            Assert.IsTrue(expectedResult[0].Compare(result[0]));
        }

        [TestMethod]
        public void CombineShortcutLists_ShouldReturnListWithOneAppSpecificEntry_WhenFirstArgumentIsEmptyAndSecondArgumentHasOneEntry()
        {
            // arrange
            var firstList = new List<KeysDataModel>();
            var secondList = new List<AppSpecificKeysDataModel>();
            var entry = new AppSpecificKeysDataModel();
            entry.OriginalKeys = VirtualKey.Control + ";" + VirtualKey.A;
            entry.NewRemapKeys = VirtualKey.Control + ";" + VirtualKey.V;
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

            Assert.AreEqual(expectedResult.Count(), result.Count());
            Assert.IsTrue(expectedResult[0].Compare(result[0]));
        }

        [TestMethod]
        public void CombineShortcutLists_ShouldReturnListWithOneAllAppsEntryAndOneAppSpecificEntry_WhenFirstArgumentHasOneEntryAndSecondArgumentHasOneEntry()
        {
            // arrange
            var firstList = new List<KeysDataModel>();
            var firstListEntry = new KeysDataModel();
            firstListEntry.OriginalKeys = VirtualKey.Control + ";" + VirtualKey.A;
            firstListEntry.NewRemapKeys = VirtualKey.Control + ";" + VirtualKey.V;
            firstList.Add(firstListEntry);
            var secondList = new List<AppSpecificKeysDataModel>();
            var secondListEntry = new AppSpecificKeysDataModel();
            secondListEntry.OriginalKeys = VirtualKey.Control + ";" + VirtualKey.B;
            secondListEntry.NewRemapKeys = VirtualKey.Control + ";" + VirtualKey.W;
            secondListEntry.TargetApp = "msedge";
            secondList.Add(secondListEntry);

            // act
            var result = KeyboardManagerViewModel.CombineShortcutLists(firstList, secondList);

            // Assert
            var expectedResult = new List<AppSpecificKeysDataModel>();
            var expectedFirstEntry = new AppSpecificKeysDataModel();
            expectedFirstEntry.OriginalKeys = firstListEntry.OriginalKeys;
            expectedFirstEntry.NewRemapKeys = firstListEntry.NewRemapKeys;
            expectedFirstEntry.TargetApp = "All Apps";
            expectedResult.Add(expectedFirstEntry);
            var expectedSecondEntry = new AppSpecificKeysDataModel();
            expectedSecondEntry.OriginalKeys = secondListEntry.OriginalKeys;
            expectedSecondEntry.NewRemapKeys = secondListEntry.NewRemapKeys;
            expectedSecondEntry.TargetApp = secondListEntry.TargetApp;
            expectedResult.Add(expectedSecondEntry);

            Assert.AreEqual(expectedResult.Count(), result.Count());
            Assert.IsTrue(expectedResult[0].Compare(result[0]));
            Assert.IsTrue(expectedResult[1].Compare(result[1]));
        }
    }
}
