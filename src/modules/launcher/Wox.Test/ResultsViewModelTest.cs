// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerLauncher.ViewModel;
using Wox.Plugin;

namespace Wox.Test
{
    [TestClass]
    public class ResultsViewModelTest
    {
        [TestMethod]
        public void ContextMenuSelectedIndexShouldEqualNoSelectionIndexWhenInitialized()
        {
            // Arrange
            ResultsViewModel rvm = new ResultsViewModel();
            Result result = new Result();
            ResultViewModel selectedItem = new ResultViewModel(result);
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
            ResultViewModel selectedItem = new ResultViewModel(result);
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
            ResultViewModel selectedItem = new ResultViewModel(result);
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
            ResultViewModel selectedItem = new ResultViewModel(result);
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
            ResultViewModel selectedItem = new ResultViewModel(result);
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
            ResultViewModel selectedItem = new ResultViewModel(result);
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
            ResultViewModel selectedItem = new ResultViewModel(result);
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
