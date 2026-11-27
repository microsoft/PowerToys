// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using KeyboardManagerEditorUI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeyboardManagerEditorUI.UnitTests
{
    [TestClass]
    public class URLShortcutTests
    {
        #region DisplayUrl Tests

        [TestMethod]
        public void DisplayUrl_ShouldReturnSingleUrl_WhenOnlyOneUrlIsSet()
        {
            // Arrange
            var shortcut = new URLShortcut { URL = "https://google.com" };

            // Act & Assert
            Assert.AreEqual("https://google.com", shortcut.DisplayUrl);
        }

        [TestMethod]
        public void DisplayUrl_ShouldTrimWhitespace_WhenSingleUrlHasSpaces()
        {
            // Arrange
            var shortcut = new URLShortcut { URL = "  https://google.com  " };

            // Act & Assert
            Assert.AreEqual("https://google.com", shortcut.DisplayUrl);
        }

        [TestMethod]
        public void DisplayUrl_ShouldShowFirstUrlAndCount_WhenMultipleUrlsWithNewline()
        {
            // Arrange
            var shortcut = new URLShortcut
            {
                URL = "https://google.com\nhttps://reddit.com\nhttps://github.com",
            };

            // Act & Assert
            Assert.AreEqual("https://google.com (+2 more)", shortcut.DisplayUrl);
        }

        [TestMethod]
        public void DisplayUrl_ShouldShowFirstUrlAndCount_WhenMultipleUrlsWithCarriageReturnNewline()
        {
            // Arrange
            var shortcut = new URLShortcut
            {
                URL = "https://google.com\r\nhttps://reddit.com\r\nhttps://github.com",
            };

            // Act & Assert
            Assert.AreEqual("https://google.com (+2 more)", shortcut.DisplayUrl);
        }

        [TestMethod]
        public void DisplayUrl_ShouldShowPlusOneMore_WhenTwoUrls()
        {
            // Arrange
            var shortcut = new URLShortcut
            {
                URL = "https://google.com\nhttps://reddit.com",
            };

            // Act & Assert
            Assert.AreEqual("https://google.com (+1 more)", shortcut.DisplayUrl);
        }

        [TestMethod]
        public void DisplayUrl_ShouldIgnoreEmptyLines_WhenUrlsHaveBlankLinesBetween()
        {
            // Arrange
            var shortcut = new URLShortcut
            {
                URL = "https://google.com\n\n\nhttps://reddit.com",
            };

            // Act & Assert
            Assert.AreEqual("https://google.com (+1 more)", shortcut.DisplayUrl);
        }

        [TestMethod]
        public void DisplayUrl_ShouldReturnEmptyString_WhenUrlIsEmpty()
        {
            // Arrange
            var shortcut = new URLShortcut { URL = string.Empty };

            // Act & Assert
            Assert.AreEqual(string.Empty, shortcut.DisplayUrl);
        }

        [TestMethod]
        public void DisplayUrl_ShouldHandleFilePaths_WhenLocalPathIsUsed()
        {
            // Arrange
            var shortcut = new URLShortcut { URL = @"C:\Users\test\file.pdf" };

            // Act & Assert
            Assert.AreEqual(@"C:\Users\test\file.pdf", shortcut.DisplayUrl);
        }

        #endregion

        #region TooltipText Tests

        [TestMethod]
        public void TooltipText_ShouldReturnSingleUrl_WhenOnlyOneUrlIsSet()
        {
            // Arrange
            var shortcut = new URLShortcut { URL = "https://google.com" };

            // Act & Assert
            Assert.AreEqual("https://google.com", shortcut.TooltipText);
        }

        [TestMethod]
        public void TooltipText_ShouldReturnAllUrls_WhenMultipleUrlsAreSet()
        {
            // Arrange
            var shortcut = new URLShortcut
            {
                URL = "https://google.com\nhttps://reddit.com\nhttps://github.com",
            };

            // Act
            var tooltip = shortcut.TooltipText;

            // Assert - should contain all three URLs separated by newlines
            var lines = tooltip.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            Assert.AreEqual(3, lines.Length);
            Assert.AreEqual("https://google.com", lines[0]);
            Assert.AreEqual("https://reddit.com", lines[1]);
            Assert.AreEqual("https://github.com", lines[2]);
        }

        [TestMethod]
        public void TooltipText_ShouldTrimEachUrl_WhenUrlsHaveWhitespace()
        {
            // Arrange
            var shortcut = new URLShortcut
            {
                URL = "  https://google.com  \n  https://reddit.com  ",
            };

            // Act
            var tooltip = shortcut.TooltipText;

            // Assert
            var lines = tooltip.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            Assert.AreEqual(2, lines.Length);
            Assert.AreEqual("https://google.com", lines[0]);
            Assert.AreEqual("https://reddit.com", lines[1]);
        }

        [TestMethod]
        public void TooltipText_ShouldSkipEmptyLines_WhenUrlsHaveBlankLinesBetween()
        {
            // Arrange
            var shortcut = new URLShortcut
            {
                URL = "https://google.com\n\n\nhttps://reddit.com",
            };

            // Act
            var tooltip = shortcut.TooltipText;

            // Assert - empty lines should be filtered out
            var lines = tooltip.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            Assert.AreEqual(2, lines.Length);
            Assert.AreEqual("https://google.com", lines[0]);
            Assert.AreEqual("https://reddit.com", lines[1]);
        }

        [TestMethod]
        public void TooltipText_ShouldHandleCRLF_WhenWindowsLineEndingsUsed()
        {
            // Arrange
            var shortcut = new URLShortcut
            {
                URL = "https://google.com\r\nhttps://reddit.com",
            };

            // Act
            var tooltip = shortcut.TooltipText;

            // Assert
            var lines = tooltip.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            Assert.AreEqual(2, lines.Length);
            Assert.AreEqual("https://google.com", lines[0]);
            Assert.AreEqual("https://reddit.com", lines[1]);
        }

        [TestMethod]
        public void TooltipText_ShouldReturnEmpty_WhenUrlIsEmpty()
        {
            // Arrange
            var shortcut = new URLShortcut { URL = string.Empty };

            // Act & Assert
            Assert.AreEqual(string.Empty, shortcut.TooltipText);
        }

        [TestMethod]
        public void TooltipText_ShouldHandleMixedContent_WhenUrlsAndFilePathsMixed()
        {
            // Arrange
            var shortcut = new URLShortcut
            {
                URL = "https://google.com\nC:\\docs\\readme.pdf",
            };

            // Act
            var tooltip = shortcut.TooltipText;

            // Assert
            var lines = tooltip.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            Assert.AreEqual(2, lines.Length);
            Assert.AreEqual("https://google.com", lines[0]);
            Assert.AreEqual("C:\\docs\\readme.pdf", lines[1]);
        }

        #endregion

        #region Backward Compatibility Tests

        [TestMethod]
        public void DisplayUrl_ShouldWorkIdentically_WhenLegacySingleUrlMapping()
        {
            // Arrange - simulates an old mapping with a single URL (no newlines)
            var shortcut = new URLShortcut { URL = "https://example.com/path?q=test&lang=en" };

            // Act & Assert - should return the URL as-is
            Assert.AreEqual("https://example.com/path?q=test&lang=en", shortcut.DisplayUrl);
            Assert.AreEqual("https://example.com/path?q=test&lang=en", shortcut.TooltipText);
        }

        [TestMethod]
        public void DisplayUrl_ShouldNotSplitOnOtherDelimiters_WhenUrlContainsSemicolonsOrPipes()
        {
            // Arrange - URL with query params containing semicolons (should NOT be split)
            var shortcut = new URLShortcut { URL = "https://example.com/path?a=1;b=2|c=3" };

            // Act & Assert - the entire URL is a single entry
            Assert.AreEqual("https://example.com/path?a=1;b=2|c=3", shortcut.DisplayUrl);
            Assert.AreEqual("https://example.com/path?a=1;b=2|c=3", shortcut.TooltipText);
        }

        #endregion
    }
}
