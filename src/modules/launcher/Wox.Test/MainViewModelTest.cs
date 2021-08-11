// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerLauncher.ViewModel;

namespace Wox.Test
{
    [TestClass]
    public class MainViewModelTest
    {
        [TestMethod]
        public void MainViewModelGetAutoCompleteTextReturnsEmptyStringWhenInputIsNull()
        {
            // Arrange
            int index = 0;
            string input = null;
            string query = "M";

            // Act
            string autoCompleteText = MainViewModel.GetAutoCompleteText(index, input, query);

            // Assert
            Assert.AreEqual(string.Empty, autoCompleteText);
        }

        [TestMethod]
        public void MainViewModelGetAutoCompleteTextReturnsEmptyStringWhenInputIsEmpty()
        {
            // Arrange
            int index = 0;
            string input = string.Empty;
            string query = "M";

            // Act
            string autoCompleteText = MainViewModel.GetAutoCompleteText(index, input, query);

            // Assert
            Assert.AreEqual(string.Empty, autoCompleteText);
        }

        [TestMethod]
        public void MainViewModelGetAutoCompleteTextReturnsEmptyStringWhenQueryIsNull()
        {
            // Arrange
            int index = 0;
            string input = "M";
            string query = null;

            // Act
            string autoCompleteText = MainViewModel.GetAutoCompleteText(index, input, query);

            // Assert
            Assert.AreEqual(string.Empty, autoCompleteText);
        }

        [TestMethod]
        public void MainViewModelGetAutoCompleteTextReturnsEmptyStringWhenQueryIsEmpty()
        {
            // Arrange
            int index = 0;
            string input = "M";
            string query = string.Empty;

            // Act
            string autoCompleteText = MainViewModel.GetAutoCompleteText(index, input, query);

            // Assert
            Assert.AreEqual(string.Empty, autoCompleteText);
        }

        [TestMethod]
        public void MainViewModelGetAutoCompleteTextReturnsEmptyStringWhenIndexIsNonZero()
        {
            // Arrange
            int index = 2;
            string input = "Visual";
            string query = "Vis";

            // Act
            string autoCompleteText = MainViewModel.GetAutoCompleteText(index, input, query);

            // Assert
            Assert.AreEqual(string.Empty, autoCompleteText);
        }

        [TestMethod]
        public void MainViewModelGetAutoCompleteTextReturnsMatchingStringWhenIndexIsZeroAndMatch()
        {
            // Arrange
            int index = 0;
            string input = "VISUAL";
            string query = "VIs";
            string expectedAutoCompleteText = "VIsUAL";

            // Act
            string autoCompleteText = MainViewModel.GetAutoCompleteText(index, input, query);

            // Assert
            Assert.AreEqual(expectedAutoCompleteText, autoCompleteText);
        }

        [TestMethod]
        public void MainViewModelGetAutoCompleteTextReturnsEmptyStringWhenIndexIsZeroAndNoMatch()
        {
            // Arrange
            int index = 0;
            string input = "VISUAL";
            string query = "Vim";
            string expectedAutoCompleteText = string.Empty;

            // Act
            string autoCompleteText = MainViewModel.GetAutoCompleteText(index, input, query);

            // Assert
            Assert.AreEqual(expectedAutoCompleteText, autoCompleteText);
        }

        [TestMethod]
        public void MainViewModelGetSearchTextReturnsEmptyStringWhenInputIsNull()
        {
            // Arrange
            int index = 0;
            string input = null;
            string query = "M";

            // Act
            string autoCompleteText = MainViewModel.GetSearchText(index, input, query);

            // Assert
            Assert.AreEqual(string.Empty, autoCompleteText);
        }

        [TestMethod]
        public void MainViewModelGetSearchTextReturnsEmptyStringWhenInputIsEmpty()
        {
            // Arrange
            int index = 0;
            string input = string.Empty;
            string query = "M";

            // Act
            string autoCompleteText = MainViewModel.GetSearchText(index, input, query);

            // Assert
            Assert.AreEqual(string.Empty, autoCompleteText);
        }

        [TestMethod]
        public void MainViewModelGetSearchTextReturnsInputStringWhenQueryIsNull()
        {
            // Arrange
            int index = 0;
            string input = "Visual";
            string query = null;

            // Act
            string autoCompleteText = MainViewModel.GetSearchText(index, input, query);

            // Assert
            Assert.AreEqual(input, autoCompleteText);
        }

        [TestMethod]
        public void MainViewModelGetSearchTextReturnsInputStringWhenQueryIsEmpty()
        {
            // Arrange
            int index = 0;
            string input = "Visual";
            string query = string.Empty;

            // Act
            string autoCompleteText = MainViewModel.GetSearchText(index, input, query);

            // Assert
            Assert.AreEqual(input, autoCompleteText);
        }

        [TestMethod]
        public void MainViewModelGetSearchTextReturnsMatchingStringWithCaseWhenIndexIsZeroAndMatch()
        {
            // Arrange
            int index = 0;
            string input = "VISUAL";
            string query = "VIs";
            string expectedAutoCompleteText = "VIsUAL";

            // Act
            string autoCompleteText = MainViewModel.GetSearchText(index, input, query);

            // Assert
            Assert.AreEqual(expectedAutoCompleteText, autoCompleteText);
        }

        [TestMethod]
        public void MainViewModelGetSearchTextReturnsInputWhenIndexIsZeroAndNoMatch()
        {
            // Arrange
            int index = 0;
            string input = "VISUAL";
            string query = "Vim";

            // Act
            string autoCompleteText = MainViewModel.GetSearchText(index, input, query);

            // Assert
            Assert.AreEqual(input, autoCompleteText);
        }

        [TestMethod]
        public void ShouldAutoCompleteTextBeEmptyShouldReturnFalseWhenAutoCompleteTextIsEmpty()
        {
            // Arrange
            string queryText = "Te";
            string autoCompleteText = string.Empty;

            // Act
            bool result = MainViewModel.ShouldAutoCompleteTextBeEmpty(queryText, autoCompleteText);

            // Assert
            Assert.AreEqual(false, result);
        }

        [TestMethod]
        public void ShouldAutoCompleteTextBeEmptyShouldReturnTrueWhenQueryTextMatchAutoCompleteText()
        {
            // Arrange
            string queryText = "Te";
            string autoCompleteText = "Teams";

            // Act
            bool result = MainViewModel.ShouldAutoCompleteTextBeEmpty(queryText, autoCompleteText);

            // Assert
            Assert.AreEqual(false, result);
        }

        [TestMethod]
        public void ShouldAutoCompleteTextBeEmptyShouldReturnTrueWhenQueryTextIsEmpty()
        {
            // Arrange
            string queryText = string.Empty;
            string autoCompleteText = "Teams";

            // Act
            bool result = MainViewModel.ShouldAutoCompleteTextBeEmpty(queryText, autoCompleteText);

            // Assert
            Assert.AreEqual(true, result);
        }

        [TestMethod]
        public void ShouldAutoCompleteTextBeEmptyShouldReturnTrueWhenQueryTextDoesNotMatchAutoCompleteText()
        {
            // Arrange
            string queryText = "TE";
            string autoCompleteText = "Teams";

            // Act
            bool result = MainViewModel.ShouldAutoCompleteTextBeEmpty(queryText, autoCompleteText);

            // Assert
            Assert.AreEqual(true, result);
        }
    }
}
