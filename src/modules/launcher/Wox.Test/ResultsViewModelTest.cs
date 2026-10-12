// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading;
using System.Windows.Input;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerLauncher.ViewModel;
using Wox.Plugin;

namespace Wox.Test
{
    [TestClass]
    public class ResultsViewModelTest
    {
        [DataTestMethod]
        [DataRow(5000, 20000, 0, false)]
        [DataRow(10500, 0, -20000, false)]
        [DataRow(10500, 20000, -20000, false)]
        [DataRow(5000, 20000, 0, true)]
        [DataRow(10500, 0, -20000, true)]
        [DataRow(10500, 20000, -20000, true)]
        public void SortAllowsPluginModifiersToOverrideHighScoringGlobalResults(int competingScore, int programModifier, int competingModifier, bool tuningEnabled)
        {
            var results = new ResultsViewModel();
            var program = new Result
            {
                Title = "Firefox",
                Score = 300,
                Metadata = new PluginMetadata { Name = "Program", WeightBoost = programModifier },
            };
            var competingResult = new Result
            {
                Title = "Firewall",
                Score = competingScore,
                Metadata = new PluginMetadata { Name = "Competing plugin", WeightBoost = competingModifier },
            };
            results.AddResults(new List<Result> { competingResult, program }, CancellationToken.None);

            results.Sort(new MainViewModel.QueryTuningOptions { SearchQueryTuningEnabled = tuningEnabled, SearchClickedItemWeight = 5 });
            results.SelectFirstResult();

            Assert.AreSame(program, results.Results[0].Result);
            Assert.AreSame(program, results.Results[results.SelectedIndex].Result);
        }

        [TestMethod]
        public void SortPreservesDefaultAndUsageOrderingWithoutPluginModifiers()
        {
            var results = new ResultsViewModel();
            var first = new Result { Title = "First", Score = 300, Metadata = new PluginMetadata { Name = "Program" } };
            var frequentlySelected = new Result { Title = "Frequent", Score = 290, SelectedCount = 3, Metadata = first.Metadata };
            results.AddResults(new List<Result> { first, frequentlySelected }, CancellationToken.None);

            results.Sort(new MainViewModel.QueryTuningOptions());

            Assert.AreSame(frequentlySelected, results.Results[0].Result);
        }

        [TestMethod]
        public void ContextMenuSelectedIndexShouldEqualNoSelectionIndexWhenInitialized()
        {
            // Arrange
            ResultsViewModel rvm = new ResultsViewModel();
            Result result = new Result();
            ResultViewModel selectedItem = new ResultViewModel(result, null, null);
            selectedItem.ContextMenuItems.Add(new ContextMenuItemViewModel(null, null, null, null, Key.None, ModifierKeys.None, null));
            rvm.SelectedItem = selectedItem;

            // Assert
            Assert.AreEqual(ResultViewModel.NoSelectionIndex, selectedItem.ContextMenuSelectedIndex);
        }

        [TestMethod]
        public void SelectNextContextMenuItemIncrementsContextMenuSelectedIndexWhenCalled()
        {
            // Arrange
            ResultsViewModel rvm = new ResultsViewModel();
            Result result = new Result();
            ResultViewModel selectedItem = new ResultViewModel(result, null, null);
            selectedItem.ContextMenuItems.Add(new ContextMenuItemViewModel(null, null, null, null, Key.None, ModifierKeys.None, null));
            rvm.SelectedItem = selectedItem;

            // Act
            rvm.SelectNextContextMenuItem();

            // Assert
            Assert.AreEqual(0, selectedItem.ContextMenuSelectedIndex);
        }

        [TestMethod]
        public void SelectNextContextMenuItemDoesnNotIncrementContextMenuSelectedIndexWhenCalledOnLastItem()
        {
            // Arrange
            ResultsViewModel rvm = new ResultsViewModel();
            Result result = new Result();
            ResultViewModel selectedItem = new ResultViewModel(result, null, null);
            selectedItem.ContextMenuItems.Add(new ContextMenuItemViewModel(null, null, null, null, Key.None, ModifierKeys.None, null));
            rvm.SelectedItem = selectedItem;

            // Act
            rvm.SelectNextContextMenuItem();

            // Assert
            Assert.AreEqual(0, selectedItem.ContextMenuSelectedIndex);
        }

        [TestMethod]
        public void SelectPreviousContextMenuItemDecrementsContextMenuSelectedIndexWhenCalled()
        {
            // Arrange
            ResultsViewModel rvm = new ResultsViewModel();
            Result result = new Result();
            ResultViewModel selectedItem = new ResultViewModel(result, null, null);
            selectedItem.ContextMenuItems.Add(new ContextMenuItemViewModel(null, null, null, null, Key.None, ModifierKeys.None, null));
            selectedItem.ContextMenuItems.Add(new ContextMenuItemViewModel(null, null, null, null, Key.None, ModifierKeys.None, null));
            selectedItem.ContextMenuItems.Add(new ContextMenuItemViewModel(null, null, null, null, Key.None, ModifierKeys.None, null));
            rvm.SelectedItem = selectedItem;

            // Act
            rvm.SelectNextContextMenuItem();
            rvm.SelectNextContextMenuItem();
            rvm.SelectNextContextMenuItem();
            rvm.SelectPreviousContextMenuItem();

            // Assert
            Assert.AreEqual(1, selectedItem.ContextMenuSelectedIndex);
        }

        [TestMethod]
        public void SelectPreviousContextMenuItemResetsContextMenuSelectedIndexWhenCalledOnFirstItem()
        {
            // Arrange
            ResultsViewModel rvm = new ResultsViewModel();
            Result result = new Result();
            ResultViewModel selectedItem = new ResultViewModel(result, null, null);
            selectedItem.ContextMenuItems.Add(new ContextMenuItemViewModel(null, null, null, null, Key.None, ModifierKeys.None, null));
            rvm.SelectedItem = selectedItem;

            // Act
            rvm.SelectNextContextMenuItem();
            rvm.SelectPreviousContextMenuItem();

            // Assert
            Assert.AreEqual(ResultViewModel.NoSelectionIndex, selectedItem.ContextMenuSelectedIndex);
        }

        [TestMethod]
        public void IsContextMenuItemSelectedReturnsTrueWhenContextMenuItemIsSelected()
        {
            // Arrange
            ResultsViewModel rvm = new ResultsViewModel();
            Result result = new Result();
            ResultViewModel selectedItem = new ResultViewModel(result, null, null);
            selectedItem.ContextMenuItems.Add(new ContextMenuItemViewModel(null, null, null, null, Key.None, ModifierKeys.None, null));
            rvm.SelectedItem = selectedItem;

            // Act
            rvm.SelectNextContextMenuItem();
            bool isContextMenuItemSelected = rvm.IsContextMenuItemSelected();

            // Assert
            Assert.IsTrue(isContextMenuItemSelected);
        }

        [TestMethod]
        public void IsContextMenuItemSelectedReturnsFalseWhenContextMenuItemIsNotSelected()
        {
            // Arrange
            ResultsViewModel rvm = new ResultsViewModel();
            Result result = new Result();
            ResultViewModel selectedItem = new ResultViewModel(result, null, null);
            selectedItem.ContextMenuItems.Add(new ContextMenuItemViewModel(null, null, null, null, Key.None, ModifierKeys.None, null));
            rvm.SelectedItem = selectedItem;

            // Act
            rvm.SelectNextContextMenuItem();
            rvm.SelectPreviousContextMenuItem();
            bool isContextMenuItemSelected = rvm.IsContextMenuItemSelected();

            // Assert
            Assert.IsFalse(isContextMenuItemSelected);
        }
    }
}
