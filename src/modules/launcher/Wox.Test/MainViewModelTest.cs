using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using Wox.Plugin;
using PowerLauncher.ViewModel;

namespace Wox.Test
{
    [TestFixture]
    class MainViewModelTest
    {
        [Test]
        public void MainViewModel_GetAutoCompleteTextReturnsEmptyString_WhenInputIsNull()
        {
            // Arrange
            int index = 0;
            string input = null;
            String query = "M";

            // Act
            string autoCompleteText = MainViewModel.GetAutoCompleteText(index, input, query);

            // Assert
            Assert.AreEqual(autoCompleteText, string.Empty);
        }

        [Test]
        public void MainViewModel_GetAutoCompleteTextReturnsEmptyString_WhenInputIsEmpty()
        {
            // Arrange
            int index = 0;
            string input = string.Empty;
            String query = "M";

            // Act
            string autoCompleteText = MainViewModel.GetAutoCompleteText(index, input, query);

            // Assert
            Assert.AreEqual(autoCompleteText, string.Empty);
        }

        [Test]
        public void MainViewModel_GetAutoCompleteTextReturnsEmptyString_WhenQueryIsNull()
        {
            // Arrange
            int index = 0;
            string input = "M";
            String query = null;

            // Act
            string autoCompleteText = MainViewModel.GetAutoCompleteText(index, input, query);

            // Assert
            Assert.AreEqual(autoCompleteText, string.Empty);
        }

        [Test]
        public void MainViewModel_GetAutoCompleteTextReturnsEmptyString_WhenQueryIsEmpty()
        {
            // Arrange
            int index = 0;
            string input = "M";
            String query = string.Empty;

            // Act
            string autoCompleteText = MainViewModel.GetAutoCompleteText(index, input, query);

            // Assert
            Assert.AreEqual(autoCompleteText, string.Empty);
        }

        [Test]
        public void MainViewModel_GetAutoCompleteTextReturnsEmptyString_WhenIndexIsNonZero()
        {
            // Arrange
            int index = 2;
            string input = "Visual";
            String query = "Vis";

            // Act
            string autoCompleteText = MainViewModel.GetAutoCompleteText(index, input, query);

            // Assert
            Assert.AreEqual(autoCompleteText, string.Empty);
        }

        [Test]
        public void MainViewModel_GetAutoCompleteTextReturnsMatchingString_WhenIndexIsZeroAndMatch()
        {
            // Arrange
            int index = 0;
            string input = "VISUAL";
            String query = "VIs";
            string ExpectedAutoCompleteText = "VIsUAL";

            // Act
            string autoCompleteText = MainViewModel.GetAutoCompleteText(index, input, query);

            // Assert
            Assert.AreEqual(autoCompleteText, ExpectedAutoCompleteText);
        }

        [Test]
        public void MainViewModel_GetAutoCompleteTextReturnsEmptyString_WhenIndexIsZeroAndNoMatch()
        {
            // Arrange
            int index = 0;
            string input = "VISUAL";
            String query = "Vim";
            string ExpectedAutoCompleteText = string.Empty;

            // Act
            string autoCompleteText = MainViewModel.GetAutoCompleteText(index, input, query);

            // Assert
            Assert.AreEqual(autoCompleteText, ExpectedAutoCompleteText);
        }

        [Test]
        public void MainViewModel_GetSearchTextReturnsEmptyString_WhenInputIsNull()
        {
            // Arrange
            int index = 0;
            string input = null;
            String query = "M";

            // Act
            string autoCompleteText = MainViewModel.GetSearchText(index, input, query);

            // Assert
            Assert.AreEqual(autoCompleteText, string.Empty);
        }

        [Test]
        public void MainViewModel_GetSearchTextReturnsEmptyString_WhenInputIsEmpty()
        {
            // Arrange
            int index = 0;
            string input = string.Empty;
            String query = "M";

            // Act
            string autoCompleteText = MainViewModel.GetSearchText(index, input, query);

            // Assert
            Assert.AreEqual(autoCompleteText, string.Empty);
        }

        [Test]
        public void MainViewModel_GetSearchTextReturnsInputString_WhenQueryIsNull()
        {
            // Arrange
            int index = 0;
            string input = "Visual";
            String query = null;

            // Act
            string autoCompleteText = MainViewModel.GetSearchText(index, input, query);

            // Assert
            Assert.AreEqual(autoCompleteText, input);
        }

        [Test]
        public void MainViewModel_GetSearchTextReturnsInputString_WhenQueryIsEmpty()
        {
            // Arrange
            int index = 0;
            string input = "Visual";
            String query = string.Empty;

            // Act
            string autoCompleteText = MainViewModel.GetSearchText(index, input, query);

            // Assert
            Assert.AreEqual(autoCompleteText, input);
        }

        [Test]
        public void MainViewModel_GetSearchTextReturnsMatchingStringWithCase_WhenIndexIsZeroAndMatch()
        {
            // Arrange
            int index = 0;
            string input = "VISUAL";
            String query = "VIs";
            string ExpectedAutoCompleteText = "VIsUAL";

            // Act
            string autoCompleteText = MainViewModel.GetSearchText(index, input, query);

            // Assert
            Assert.AreEqual(autoCompleteText, ExpectedAutoCompleteText);
        }

        [Test]
        public void MainViewModel_GetSearchTextReturnsInput_WhenIndexIsZeroAndNoMatch()
        {
            // Arrange
            int index = 0;
            string input = "VISUAL";
            String query = "Vim";

            // Act
            string autoCompleteText = MainViewModel.GetSearchText(index, input, query);

            // Assert
            Assert.AreEqual(autoCompleteText, input);
        }
    }
}
