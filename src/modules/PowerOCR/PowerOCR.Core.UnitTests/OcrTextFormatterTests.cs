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
}
