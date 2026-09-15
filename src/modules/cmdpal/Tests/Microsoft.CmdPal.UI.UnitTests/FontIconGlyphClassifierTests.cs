// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class FontIconGlyphClassifierTests
{
    [TestMethod]
    public void EmptyInputHasNoGlyph()
    {
        Assert.AreEqual(FontIconGlyphKind.None, FontIconGlyphClassifier.Classify(null));
        Assert.AreEqual(FontIconGlyphKind.None, FontIconGlyphClassifier.Classify(string.Empty));
    }

    [TestMethod]
    [DataRow("\uE700")]
    [DataRow("\uF000")]
    [DataRow("\uF8FF")]
    public void FluentPrivateUseCharactersAreSymbols(string text)
    {
        Assert.AreEqual(FontIconGlyphKind.FluentSymbol, FontIconGlyphClassifier.Classify(text));
    }

    [TestMethod]
    [DataRow("A")]
    [DataRow("\uE6FF")]
    [DataRow("\uF900")]
    [DataRow("e\u0301")]
    public void SingleNonEmojiGraphemesUseTheGeneralFont(string text)
    {
        Assert.AreEqual(FontIconGlyphKind.Other, FontIconGlyphClassifier.Classify(text));
    }

    [TestMethod]
    public void IsolatedLowSurrogatePreservesNativeClassifierBehavior()
    {
        var text = new string((char)0xDC00, 1);

        Assert.AreEqual(FontIconGlyphKind.Other, FontIconGlyphClassifier.Classify(text));
    }

    [TestMethod]
    [DataRow("\u231A")]
    [DataRow("\U0001F600")]
    [DataRow("\u2764\uFE0F")]
    [DataRow("2\uFE0F\u20E3")]
    [DataRow("\U0001F469\u200D\U0001F4BB")]
    public void EmojiGraphemesUseTheEmojiFont(string text)
    {
        Assert.AreEqual(FontIconGlyphKind.Emoji, FontIconGlyphClassifier.Classify(text));
    }

    [TestMethod]
    [DataRow("\u2764")]
    [DataRow("\u2764\uFE0E")]
    [DataRow("2\u20E3")]
    public void TextPresentationRemainsGeneralText(string text)
    {
        Assert.AreEqual(FontIconGlyphKind.Other, FontIconGlyphClassifier.Classify(text));
    }

    [TestMethod]
    [DataRow("\uD83D")]
    [DataRow("ab")]
    [DataRow("C:\\icon.png")]
    [DataRow("\U0001F600\U0001F600")]
    public void InvalidOrMultipleGraphemesAreRejected(string text)
    {
        Assert.AreEqual(FontIconGlyphKind.Invalid, FontIconGlyphClassifier.Classify(text));
    }

    [TestMethod]
    public void FontFamilySelectionMatchesTheNativeConverter()
    {
        Assert.AreEqual(
            "Segoe Fluent Icons, Segoe MDL2 Assets",
            FontIconGlyphClassifier.GetFontFamily(FontIconGlyphKind.FluentSymbol, null));
        Assert.AreEqual(
            "Segoe UI Emoji, Segoe UI",
            FontIconGlyphClassifier.GetFontFamily(FontIconGlyphKind.Emoji, null));
        Assert.AreEqual(
            "Custom Font",
            FontIconGlyphClassifier.GetFontFamily(FontIconGlyphKind.Other, "Custom Font"));
        Assert.AreEqual(
            "Segoe UI",
            FontIconGlyphClassifier.GetFontFamily(FontIconGlyphKind.Invalid, "Custom Font"));
    }
}
