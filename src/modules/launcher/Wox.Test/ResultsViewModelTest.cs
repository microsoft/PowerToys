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
        public void ResultsViewModel_HasNoContextMenuSelected_WhenInitialized()
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
        public void ResultsViewModel_SelectsNextContextMenu_WhenSelectNextContextMenuItemIsCalled()
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
        public void ResultsViewModel_SelectsLastContextMenu_WhenSelectNextContextMenuItemIsCalledOnLastItem()
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
        public void ResultsViewModel_SelectsPreviousContextMenu_WhenSelectPreviousContextMenuItemIsCalled()
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
        public void ResultsViewModel_UnSelectAllContextMenuItems_WhenSelectPreviousContextMenuItemIsCalledOnFirstItem()
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
        public void ResultsViewModel_ReturnsTrueIfContextMenuItemIsSelected_WhenIsContextMenuItemSelectedIsCalled()
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
            Assert.IsTrue(rvm.IsContextMenuItemSelected());
        }

        [Test]
        public void ResultsViewModel_ReturnsFalseIfNoContextMenuItemIsSelected_WhenIsContextMenuItemSelectedIsCalled()
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
            Assert.IsFalse(rvm.IsContextMenuItemSelected());
        }
    }
}
