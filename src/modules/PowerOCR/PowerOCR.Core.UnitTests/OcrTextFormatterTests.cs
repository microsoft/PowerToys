// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerOCR.Core.Formatting;
using PowerOCR.Core.Models;

namespace PowerOCR.Core.UnitTests;

[TestClass]
public sealed class OcrTextFormatterTests
{
    [TestMethod]
    public void FormatDocument_LatinLanguage_UsesOcrLineText()
    {
        var document = new OcrDocument(
        [
            new OcrLineData(
                "Hello world",
                new OcrRect(0, 0, 100, 20),
                [new("Hello", new(0, 0, 40, 20)), new("world", new(50, 0, 50, 20))]),
        ]);

        Assert.AreEqual("Hello world", OcrTextFormatter.FormatDocument(document, "en-US"));
    }

    [TestMethod]
    public void FormatDocument_ChineseLanguage_JoinsSingleCharacterWords()
    {
        var document = new OcrDocument(
        [
            new OcrLineData(
                "你 好",
                new OcrRect(0, 0, 40, 20),
                [new("你", new(0, 0, 20, 20)), new("好", new(20, 0, 20, 20))]),
        ]);

        Assert.AreEqual("你好", OcrTextFormatter.FormatDocument(document, "zh-CN"));
    }

    [TestMethod]
    public void FormatDocument_JapaneseLanguage_JoinsSingleCharacterWords()
    {
        var document = new OcrDocument(
        [
            new OcrLineData(
                "日 本",
                new OcrRect(0, 0, 40, 20),
                [new("日", new(0, 0, 20, 20)), new("本", new(20, 0, 20, 20))]),
        ]);

        Assert.AreEqual("日本", OcrTextFormatter.FormatDocument(document, "ja-JP"));
    }

    [TestMethod]
    [DataRow("zh-CN")]
    [DataRow("ja-JP")]
    public void FormatDocument_CjkLanguage_DoesNotInsertSpaceBeforePunctuation(string languageTag)
    {
        var document = new OcrDocument(
        [
            new OcrLineData(
                "2026 。",
                new OcrRect(0, 0, 60, 20),
                [new("2026", new(0, 0, 40, 20)), new("。", new(40, 0, 20, 20))]),
        ]);

        Assert.AreEqual("2026。", OcrTextFormatter.FormatDocument(document, languageTag));
    }

    [TestMethod]
    public void FormatDocument_RightToLeftLanguage_ReversesWordOrderPerLine()
    {
        var document = new OcrDocument(
        [
            new OcrLineData(
                "one two",
                new OcrRect(0, 0, 80, 20),
                [new("one", new(0, 0, 30, 20)), new("two", new(40, 0, 30, 20))]),
        ]);

        Assert.AreEqual("two one", OcrTextFormatter.FormatDocument(document, "ar-SA"));
    }

    [TestMethod]
    public void FormatSingleLine_ChineseLanguage_PreservesSpaceBetweenLatinWordsAcrossLines()
    {
        var document = new OcrDocument(
        [
            new OcrLineData(
                "Power",
                new OcrRect(0, 0, 50, 20),
                [new("Power", new(0, 0, 50, 20))]),
            new OcrLineData(
                "Toys",
                new OcrRect(0, 30, 40, 20),
                [new("Toys", new(0, 30, 40, 20))]),
        ]);

        Assert.AreEqual("Power Toys", OcrTextFormatter.FormatSingleLine(document, "zh-CN"));
    }

    [TestMethod]
    public void JoinCjkAwareWords_MultipleCjkCharactersPerWord_DoesNotInsertSpaces()
    {
        Assert.AreEqual(
            "中文测试。",
            OcrTextFormatter.JoinCjkAwareWords(Words("中文", "测试", "。")));
    }

    [TestMethod]
    public void JoinCjkAwareWords_SupplementaryPlaneCjkCharacter_DoesNotInsertSpace()
    {
        Assert.AreEqual(
            "\U00020000\U00020001。",
            OcrTextFormatter.JoinCjkAwareWords(Words("\U00020000", "\U00020001", "。")));
    }

    [TestMethod]
    public void JoinCjkAwareWords_ClosingPunctuation_DoesNotInsertLeadingSpace()
    {
        Assert.AreEqual(
            "2026。",
            OcrTextFormatter.JoinCjkAwareWords(Words("2026", "。")));
    }

    [TestMethod]
    public void JoinCjkAwareWords_QuotedLatinWord_DoesNotInsertSpacesInsideQuotes()
    {
        Assert.AreEqual(
            "「PowerToys」",
            OcrTextFormatter.JoinCjkAwareWords(Words("「", "PowerToys", "」")));
    }

    [TestMethod]
    public void JoinCjkAwareWords_ClosingQuoteBeforeLatinWord_InsertsFollowingSpace()
    {
        Assert.AreEqual(
            "“PowerToys” OCR",
            OcrTextFormatter.JoinCjkAwareWords(Words("“", "PowerToys", "”", "OCR")));
    }

    [TestMethod]
    public void JoinCjkAwareWords_DecomposedLatinWord_PreservesFollowingSpace()
    {
        Assert.AreEqual(
            "Cafe\u0301 工具",
            OcrTextFormatter.JoinCjkAwareWords(Words("Cafe\u0301", "工具")));
    }

    [TestMethod]
    public void JoinCjkAwareWords_EmDashBetweenLatinWords_PreservesSpaces()
    {
        Assert.AreEqual(
            "PowerToys — OCR",
            OcrTextFormatter.JoinCjkAwareWords(Words("PowerToys", "—", "OCR")));
    }

    [TestMethod]
    public void JoinCjkAwareWords_EllipsisBeforeLatinWord_PreservesFollowingSpace()
    {
        Assert.AreEqual(
            "Wait… OCR",
            OcrTextFormatter.JoinCjkAwareWords(Words("Wait", "…", "OCR")));
    }

    [TestMethod]
    public void JoinCjkAwareWords_LatinWordsAndPunctuation_PreservesWordSpacing()
    {
        Assert.AreEqual(
            "PowerToys, OCR",
            OcrTextFormatter.JoinCjkAwareWords(Words("PowerToys", ",", "OCR")));
    }

    [TestMethod]
    public void JoinCjkAwareWords_MixedCjkAndLatin_PreservesSpacesAroundLatinWord()
    {
        Assert.AreEqual(
            "使用 PowerToys 工具",
            OcrTextFormatter.JoinCjkAwareWords(Words("使用", "PowerToys", "工具")));
    }

    [TestMethod]
    public void JoinCjkAwareWords_JapaneseProlongedSoundMark_DoesNotInsertSpaces()
    {
        Assert.AreEqual(
            "コード",
            OcrTextFormatter.JoinCjkAwareWords(Words("コ", "ー", "ド")));
    }

    [TestMethod]
    public void JoinCjkAwareWords_AsciiInfixPunctuation_PreservesSpaces()
    {
        Assert.AreEqual(
            "A & B",
            OcrTextFormatter.JoinCjkAwareWords(Words("A", "&", "B")));
    }

    [TestMethod]
    public void CollapseToSingleLine_EmptyText_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, OcrTextFormatter.CollapseToSingleLine(string.Empty));
    }

    [TestMethod]
    public void CollapseToSingleLine_MultipleLineEndings_CollapsesWhitespace()
    {
        Assert.AreEqual(
            "one two three",
            OcrTextFormatter.CollapseToSingleLine(" one\r\n  two\nthree "));
    }

    private static IReadOnlyList<OcrWordData> Words(params string[] words)
        => words
            .Select((word, index) => new OcrWordData(word, new OcrRect(index * 20, 0, 20, 20)))
            .ToList();
}
