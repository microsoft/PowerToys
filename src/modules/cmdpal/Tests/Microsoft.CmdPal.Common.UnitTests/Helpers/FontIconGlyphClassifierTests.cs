// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Helpers;

namespace Microsoft.CmdPal.Common.UnitTests.Helpers;

[TestClass]
public class FontIconGlyphClassifierTests
{
    [DataTestMethod]
    [DataRow("", FontIconGlyphKind.None)]
    [DataRow("A", FontIconGlyphKind.Other)]
    [DataRow("\uE8C8", FontIconGlyphKind.FluentSymbol)]
    [DataRow("\uD83D", FontIconGlyphKind.Invalid)]
    [DataRow("C:", FontIconGlyphKind.Invalid)]
    [DataRow(@"C:\Temp\icon.png", FontIconGlyphKind.Invalid)]
    [DataRow("😀", FontIconGlyphKind.Emoji)]
    [DataRow("👨‍👩‍👧‍👦", FontIconGlyphKind.Emoji)]
    [DataRow("❤️", FontIconGlyphKind.Emoji)]
    [DataRow("♥︎", FontIconGlyphKind.Other)]
    [DataRow("1️⃣", FontIconGlyphKind.Emoji)]
    public void Classify_ReturnsExpectedKind(string input, FontIconGlyphKind expected)
    {
        var result = FontIconGlyphClassifier.Classify(input);

        Assert.AreEqual(expected, result);
    }

    [DataTestMethod]
    [DataRow("", false)]
    [DataRow("A", true)]
    [DataRow("\uE8C8", true)]
    [DataRow("\uD83D", false)]
    [DataRow("C:", false)]
    [DataRow(@"C:\Temp\icon.png", false)]
    [DataRow("😀", true)]
    [DataRow("👨‍👩‍👧‍👦", true)]
    [DataRow("❤️", true)]
    public void IsLikelyToBeEmojiOrSymbolIcon_ReturnsExpectedValue(string input, bool expected)
    {
        var result = FontIconGlyphClassifier.IsLikelyToBeEmojiOrSymbolIcon(input);

        Assert.AreEqual(expected, result);
    }
}
