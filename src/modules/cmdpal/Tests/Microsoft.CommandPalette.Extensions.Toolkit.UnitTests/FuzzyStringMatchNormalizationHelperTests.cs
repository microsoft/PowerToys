// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests;

[TestClass]
public class FuzzyStringMatchNormalizationHelperTests
{
    [TestMethod]
    public void NormalizeString_ShouldReturnEmpty_WhenInputIsEmpty()
    {
        // Arrange
        var input = string.Empty;

        // Act
        var result = FuzzyStringMatchNormalizationHelper.NormalizeString(input);

        // Assert
        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void NormalizeString_ShouldReturnEmpty_WhenInputIsNull()
    {
        // Arrange
        string input = null;

        // Act
        var result = FuzzyStringMatchNormalizationHelper.NormalizeString(input);

        // Assert
        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    [DataRow("hello", "HELLO")]
    [DataRow("WORLD", "WORLD")]
    [DataRow("12345", "12345")]
    public void NormalizeString_ShouldReturnUpperInvariant_WhenInputHasNoDiacritics(string input, string expected)
    {
        // Act
        var result = FuzzyStringMatchNormalizationHelper.NormalizeString(input);

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("crème brûlée", "CREME BRULEE")]
    [DataRow("résumé", "RESUME")]
    [DataRow("Jalapeño", "JALAPENO")]
    [DataRow("über", "UBER")]
    [DataRow("Čeština", "CESTINA")]
    public void NormalizeString_ShouldRemoveDiacriticsAndUpperInvariant(string input, string expected)
    {
        // Act
        var result = FuzzyStringMatchNormalizationHelper.NormalizeString(input);

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void NormalizeString_ShouldHandleLongStrings()
    {
        // Arrange
        var input = new string('a', 300) + "é" + new string('b', 200);
        var expected = new string('A', 300) + "E" + new string('B', 200);

        // Act
        var result = FuzzyStringMatchNormalizationHelper.NormalizeString(input);

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("안녕하세요", "안녕하세요")] // Korean
    [DataRow("你好", "你好")] // Chinese
    [DataRow("こんにちは", "こんにちは")] // Japanese
    [DataRow("你好 world", "你好 WORLD")] // Mixed Chinese and Latin
    [DataRow("안녕하세요 Hello", "안녕하세요 HELLO")] // Mixed Korean and Latin
    public void NormalizeString_ShouldHandleNonLatinLanguages(string input, string expected)
    {
        // Act
        var result = FuzzyStringMatchNormalizationHelper.NormalizeString(input);

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("Hello World 👍", "HELLO WORLD 👍")] // Emoji
    [DataRow("👍🏻", "👍🏻")] // Emoji with modifier
    [DataRow("👨‍👩‍👧‍👦", "👨‍👩‍👧‍👦")] // Compound Emoji
    [DataRow("Text with\tcontrol\ncharacters", "TEXT WITH\tCONTROL\nCHARACTERS")] // Control characters
    [DataRow("e\u0301\u0308", "E")] // Standalone diacritics
    public void NormalizeString_ShouldHandleComplexUnicode(string input, string expected)
    {
        // Act
        var result = FuzzyStringMatchNormalizationHelper.NormalizeString(input);

        // Assert
        Assert.AreEqual(expected, result);
    }
}
