using NUnit.Framework;
using PowerLauncher.ViewModel;
using System;
using System.Collections.Generic;
using System.Text;
using Wox.Core.Plugin;
using Wox.Plugin;

namespace Wox.Test
{
    [TestFixture]
    class ResultsViewModelTest
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
                Title = "Dummy Context Menu"
            });
            rvm.SelectedItem = selectedItem;

            // Assert
            Assert.AreEqual(selectedItem.ContextMenuSelectedIndex, ResultViewModel.NoSelectionIndex);
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
                Title = "Dummy Context Menu"
            });
            rvm.SelectedItem = selectedItem;


            // Act
            rvm.SelectNextContextMenuItem();

            // Assert
            Assert.AreEqual(selectedItem.ContextMenuSelectedIndex, 0);
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
                Title = "Dummy Context Menu"
            });
            rvm.SelectedItem = selectedItem;


            // Act
            rvm.SelectNextContextMenuItem();

            // Assert
            Assert.AreEqual(selectedItem.ContextMenuSelectedIndex, 0);
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
                Title = "Dummy Context Menu 1"
            });
            selectedItem.ContextMenuItems.Add(new ContextMenuItemViewModel()
            {
                Title = "Dummy Context Menu 2"
            });
            selectedItem.ContextMenuItems.Add(new ContextMenuItemViewModel()
            {
                Title = "Dummy Context Menu 3"
            });
            rvm.SelectedItem = selectedItem;


            // Act
            rvm.SelectNextContextMenuItem();
            rvm.SelectNextContextMenuItem();
            rvm.SelectNextContextMenuItem();
            rvm.SelectPreviousContextMenuItem();

            // Assert
            Assert.AreEqual(selectedItem.ContextMenuSelectedIndex, 1);
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
                Title = "Dummy Context Menu"
            });
            rvm.SelectedItem = selectedItem;


            // Act
            rvm.SelectNextContextMenuItem();
            rvm.SelectPreviousContextMenuItem();

            // Assert
            Assert.AreEqual(selectedItem.ContextMenuSelectedIndex, ResultViewModel.NoSelectionIndex);
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
                Title = "Dummy Context Menu"
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
                Title = "Dummy Context Menu"
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
