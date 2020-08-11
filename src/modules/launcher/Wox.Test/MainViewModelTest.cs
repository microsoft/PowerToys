// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using NUnit.Framework;
using PowerLauncher.ViewModel;

namespace Wox.Test
{
    [TestFixture]
    internal class MainViewModelTest
    {
        [Test]
        public void MainViewModel_GetAutoCompleteTextReturnsEmptyString_WhenInputIsNull()
        {
            // Arrange
            int index = 0;
            string input = null;
            string query = "M";

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
            string query = "M";

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
            string query = null;

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
            string query = string.Empty;

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
            string query = "Vis";

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
            string query = "VIs";
            string expectedAutoCompleteText = "VIsUAL";

            // Act
            string autoCompleteText = MainViewModel.GetAutoCompleteText(index, input, query);

            // Assert
            Assert.AreEqual(autoCompleteText, expectedAutoCompleteText);
        }

        [Test]
        public void MainViewModel_GetAutoCompleteTextReturnsEmptyString_WhenIndexIsZeroAndNoMatch()
        {
            // Arrange
            int index = 0;
            string input = "VISUAL";
            string query = "Vim";
            string expectedAutoCompleteText = string.Empty;

            // Act
            string autoCompleteText = MainViewModel.GetAutoCompleteText(index, input, query);

            // Assert
            Assert.AreEqual(autoCompleteText, expectedAutoCompleteText);
        }

        [Test]
        public void MainViewModel_GetSearchTextReturnsEmptyString_WhenInputIsNull()
        {
            // Arrange
            int index = 0;
            string input = null;
            string query = "M";

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
            string query = "M";

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
            string query = null;

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
            string query = string.Empty;

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
            string query = "VIs";
            string expectedAutoCompleteText = "VIsUAL";

            // Act
            string autoCompleteText = MainViewModel.GetSearchText(index, input, query);

            // Assert
            Assert.AreEqual(autoCompleteText, expectedAutoCompleteText);
        }

        [Test]
        public void MainViewModel_GetSearchTextReturnsInput_WhenIndexIsZeroAndNoMatch()
        {
            // Arrange
            int index = 0;
            string input = "VISUAL";
            string query = "Vim";

            // Act
            string autoCompleteText = MainViewModel.GetSearchText(index, input, query);

            // Assert
            Assert.AreEqual(autoCompleteText, input);
        }
    }
}
