// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using NUnit.Framework;
using PowerLauncher.ViewModel;
using Wox.Plugin;

namespace Wox.Test
{
    [TestFixture]
    internal class ResultsViewModelTest
    {
        [Test]
        public void ContextMenuSelectedIndex_ShouldEqualNoSelectionIndex_WhenInitialized()
        {
            // Arrange
            ResultsViewModel rvm = new ResultsViewModel();
            Result result = new Result();
            ResultViewModel selectedItem = new ResultViewModel(result);
            selectedItem.ContextMenuItems.Add(new ContextMenuItemViewModel()
            {
                Title = "Dummy Context Menu",
            });
            rvm.SelectedItem = selectedItem;

            // Assert
            Assert.AreEqual(ResultViewModel.NoSelectionIndex, selectedItem.ContextMenuSelectedIndex);
        }

        [Test]
        public void SelectNextContextMenuItem_IncrementsContextMenuSelectedIndex_WhenCalled()
        {
            // Arrange
            ResultsViewModel rvm = new ResultsViewModel();
            Result result = new Result();
            ResultViewModel selectedItem = new ResultViewModel(result);
            selectedItem.ContextMenuItems.Add(new ContextMenuItemViewModel()
            {
                Title = "Dummy Context Menu",
            });
            rvm.SelectedItem = selectedItem;

            // Act
            rvm.SelectNextContextMenuItem();

            // Assert
            Assert.AreEqual(0, selectedItem.ContextMenuSelectedIndex);
        }

        [Test]
        public void SelectNextContextMenuItem_DoesnNotIncrementContextMenuSelectedIndex_WhenCalledOnLastItem()
        {
            // Arrange
            ResultsViewModel rvm = new ResultsViewModel();
            Result result = new Result();
            ResultViewModel selectedItem = new ResultViewModel(result);
            selectedItem.ContextMenuItems.Add(new ContextMenuItemViewModel()
            {
                Title = "Dummy Context Menu",
            });
            rvm.SelectedItem = selectedItem;

            // Act
            rvm.SelectNextContextMenuItem();

            // Assert
            Assert.AreEqual(0, selectedItem.ContextMenuSelectedIndex);
        }

        [Test]
        public void SelectPreviousContextMenuItem_DecrementsContextMenuSelectedIndex_WhenCalled()
        {
            // Arrange
            ResultsViewModel rvm = new ResultsViewModel();
            Result result = new Result();
            ResultViewModel selectedItem = new ResultViewModel(result);
            selectedItem.ContextMenuItems.Add(new ContextMenuItemViewModel()
            {
                Title = "Dummy Context Menu 1",
            });
            selectedItem.ContextMenuItems.Add(new ContextMenuItemViewModel()
            {
                Title = "Dummy Context Menu 2",
            });
            selectedItem.ContextMenuItems.Add(new ContextMenuItemViewModel()
            {
                Title = "Dummy Context Menu 3",
            });
            rvm.SelectedItem = selectedItem;

            // Act
            rvm.SelectNextContextMenuItem();
            rvm.SelectNextContextMenuItem();
            rvm.SelectNextContextMenuItem();
            rvm.SelectPreviousContextMenuItem();

            // Assert
            Assert.AreEqual(1, selectedItem.ContextMenuSelectedIndex);
        }

        [Test]
        public void SelectPreviousContextMenuItem_ResetsContextMenuSelectedIndex_WhenCalledOnFirstItem()
        {
            // Arrange
            ResultsViewModel rvm = new ResultsViewModel();
            Result result = new Result();
            ResultViewModel selectedItem = new ResultViewModel(result);
            selectedItem.ContextMenuItems.Add(new ContextMenuItemViewModel()
            {
                Title = "Dummy Context Menu",
            });
            rvm.SelectedItem = selectedItem;

            // Act
            rvm.SelectNextContextMenuItem();
            rvm.SelectPreviousContextMenuItem();

            // Assert
            Assert.AreEqual(ResultViewModel.NoSelectionIndex, selectedItem.ContextMenuSelectedIndex);
        }

        [Test]
        public void IsContextMenuItemSelected_ReturnsTrue_WhenContextMenuItemIsSelected()
        {
            // Arrange
            ResultsViewModel rvm = new ResultsViewModel();
            Result result = new Result();
            ResultViewModel selectedItem = new ResultViewModel(result);
            selectedItem.ContextMenuItems.Add(new ContextMenuItemViewModel()
            {
                Title = "Dummy Context Menu",
            });
            rvm.SelectedItem = selectedItem;

            // Act
            rvm.SelectNextContextMenuItem();
            bool isContextMenuItemSelected = rvm.IsContextMenuItemSelected();

            // Assert
            Assert.IsTrue(isContextMenuItemSelected);
        }

        [Test]
        public void IsContextMenuItemSelected_ReturnsFalse_WhenContextMenuItemIsNotSelected()
        {
            // Arrange
            ResultsViewModel rvm = new ResultsViewModel();
            Result result = new Result();
            ResultViewModel selectedItem = new ResultViewModel(result);
            selectedItem.ContextMenuItems.Add(new ContextMenuItemViewModel()
            {
                Title = "Dummy Context Menu",
            });
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
