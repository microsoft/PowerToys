// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.Common.Text;

namespace Microsoft.CmdPal.Common.UnitTests.Text;

[TestClass]
public class StringFolderTests
{
    private readonly StringFolder _folder = new();

    [TestMethod]
    [DataRow(null, "")]
    [DataRow("", "")]
    [DataRow("abc", "ABC")]
    [DataRow("ABC", "ABC")]
    [DataRow("a\\b", "A/B")]
    [DataRow("a/b", "A/B")]
    [DataRow("ÁBC", "ABC")] // Diacritic removal
    [DataRow("ñ", "N")]
    [DataRow("hello world", "HELLO WORLD")]
    public void Fold_RemoveDiacritics_Works(string input, string expected)
    {
        Assert.AreEqual(expected, _folder.Fold(input, removeDiacritics: true));
    }

    [TestMethod]
    [DataRow("abc", "ABC")]
    [DataRow("ÁBC", "ÁBC")] // No diacritic removal
    [DataRow("a\\b", "A/B")]
    public void Fold_KeepDiacritics_Works(string input, string expected)
    {
        Assert.AreEqual(expected, _folder.Fold(input, removeDiacritics: false));
    }

    [TestMethod]
    public void Fold_IsAlreadyFolded_ReturnsSameInstance()
    {
        var input = "ALREADY/FOLDED";
        var result = _folder.Fold(input, removeDiacritics: true);
        Assert.AreSame(input, result);
    }

    [TestMethod]
    public void Fold_WithNonAsciiButNoDiacritics_ReturnsFolded()
    {
        // E.g. Cyrillic or other scripts that might not decompose in a simple way or just upper case
        // "привет" -> "ПРИВЕТ"
        var input = "привет";
        var expected = "ПРИВЕТ";
        Assert.AreEqual(expected, _folder.Fold(input, removeDiacritics: true));
    }
}
