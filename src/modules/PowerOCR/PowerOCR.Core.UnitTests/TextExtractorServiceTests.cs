// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerOCR.Core.Imaging;
using PowerOCR.Core.Models;
using PowerOCR.Core.Ocr;
using PowerOCR.Core.Services;
using Windows.Globalization;
using Windows.Media.Ocr;

namespace PowerOCR.Core.UnitTests;

[TestClass]
public sealed class TextExtractorServiceTests
{
    private static readonly Language EnglishLanguage = new("en-US");
    private static readonly Language ChineseLanguage = new("zh-CN");

    private static OcrDocument MakeDocument()
    {
        return new OcrDocument(
        [
            new OcrLineData(
                "Hello world",
                new OcrRect(0, 0, 110, 20),
                [
                    new OcrWordData("Hello", new OcrRect(0, 0, 50, 20)),
                    new OcrWordData("world", new OcrRect(60, 0, 50, 20)),
                ]),
            new OcrLineData(
                "Foo bar",
                new OcrRect(0, 30, 80, 20),
                [
                    new OcrWordData("Foo", new OcrRect(0, 30, 30, 20)),
                    new OcrWordData("bar", new OcrRect(40, 30, 40, 20)),
                ]),
        ]);
    }

    private static OcrLineData MakeCell(string text, double x, double y)
        => new(text, new OcrRect(x, y, 40, 20), [new(text, new OcrRect(x, y, 40, 20))]);

    private sealed class FakeRecognizer : IOcrRecognizer
    {
        public required OcrDocument Document { get; init; }

        public int ReceivedBitmapWidth { get; private set; }

        public int ReceivedBitmapHeight { get; private set; }

        public Task<OcrDocument> RecognizeAsync(
            Bitmap bitmap,
            Language language,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReceivedBitmapWidth = bitmap.Width;
            ReceivedBitmapHeight = bitmap.Height;
            return Task.FromResult(Document);
        }
    }

    [TestMethod]
    public async Task ExtractAsync_RegionMode_ReturnsMultilineText()
    {
        var recognizer = new FakeRecognizer { Document = MakeDocument() };
        var service = new TextExtractorService(new BitmapPreprocessor(), recognizer);
        using var bitmap = new Bitmap(200, 100, PixelFormat.Format32bppArgb);
        var request = new OcrExtractionRequest(bitmap, EnglishLanguage, OcrCaptureMode.Region);

        string result = await service.ExtractAsync(request, CancellationToken.None);

        Assert.IsTrue(result.Contains('\n') || result.Contains("\r\n"), "Expected multiline text");
        StringAssert.Contains(result, "Hello world");
        StringAssert.Contains(result, "Foo bar");
    }

    [TestMethod]
    public async Task ExtractAsync_SingleLineMode_CollapsesLineBreaks()
    {
        var recognizer = new FakeRecognizer { Document = MakeDocument() };
        var service = new TextExtractorService(new BitmapPreprocessor(), recognizer);
        using var bitmap = new Bitmap(200, 100, PixelFormat.Format32bppArgb);
        var request = new OcrExtractionRequest(bitmap, EnglishLanguage, OcrCaptureMode.SingleLine);

        string result = await service.ExtractAsync(request, CancellationToken.None);

        Assert.IsFalse(result.Contains('\n'), "Expected single line");
        Assert.IsFalse(result.Contains('\r'), "Expected single line");
        StringAssert.Contains(result, "Hello");
        StringAssert.Contains(result, "Foo");
    }

    [TestMethod]
    public async Task ExtractAsync_SingleLineMode_ChineseJoinsAcrossOcrLinesWithoutSpaces()
    {
        var document = new OcrDocument(
        [
            new OcrLineData(
                "你 好",
                new OcrRect(0, 0, 40, 20),
                [new("你", new(0, 0, 20, 20)), new("好", new(20, 0, 20, 20))]),
            new OcrLineData(
                "世 界",
                new OcrRect(0, 30, 40, 20),
                [new("世", new(0, 30, 20, 20)), new("界", new(20, 30, 20, 20))]),
        ]);
        var recognizer = new FakeRecognizer { Document = document };
        var service = new TextExtractorService(new BitmapPreprocessor(), recognizer);
        using var bitmap = new Bitmap(200, 100, PixelFormat.Format32bppArgb);
        var request = new OcrExtractionRequest(bitmap, ChineseLanguage, OcrCaptureMode.SingleLine);

        string result = await service.ExtractAsync(request, CancellationToken.None);

        Assert.AreEqual("你好世界", result);
    }

    [TestMethod]
    public async Task ExtractAsync_TableMode_ReturnsTabbedText()
    {
        var document = new OcrDocument(
        [
            MakeCell("A1", 0, 0),
            MakeCell("B1", 100, 0),
            MakeCell("A2", 0, 40),
            MakeCell("B2", 100, 40),
        ]);
        var recognizer = new FakeRecognizer { Document = document };
        var service = new TextExtractorService(new BitmapPreprocessor(), recognizer);
        using var bitmap = new Bitmap(200, 100, PixelFormat.Format32bppArgb);
        var request = new OcrExtractionRequest(bitmap, EnglishLanguage, OcrCaptureMode.Table);

        string result = await service.ExtractAsync(request, CancellationToken.None);

        Assert.AreEqual($"A1\tB1{Environment.NewLine}A2\tB2", result);
    }

    [TestMethod]
    public async Task ExtractAsync_WordMode_ReturnsClickedWord()
    {
        var recognizer = new FakeRecognizer { Document = MakeDocument() };
        var service = new TextExtractorService(new BitmapPreprocessor(), recognizer);
        using var bitmap = new Bitmap(200, 100, PixelFormat.Format32bppArgb);

        // The click is expressed in original bitmap coordinates.
        var request = new OcrExtractionRequest(bitmap, EnglishLanguage, OcrCaptureMode.Word, new OcrPoint(25, 10));

        string result = await service.ExtractAsync(request, CancellationToken.None);

        Assert.AreEqual("Hello", result);
    }

    [TestMethod]
    public async Task ExtractAsync_OversizedBitmap_UsesUnscaledBitmapWidth()
    {
        int oversizedWidth = (int)Math.Floor(OcrEngine.MaxImageDimension / 1.5) + 1;
        var recognizer = new FakeRecognizer { Document = MakeDocument() };
        var service = new TextExtractorService(new BitmapPreprocessor(), recognizer);
        using var bitmap = new Bitmap(oversizedWidth, 100, PixelFormat.Format32bppArgb);
        var request = new OcrExtractionRequest(bitmap, EnglishLanguage, OcrCaptureMode.Region);

        await service.ExtractAsync(request, CancellationToken.None);

        Assert.AreEqual(oversizedWidth, recognizer.ReceivedBitmapWidth);
    }

    [TestMethod]
    public async Task ExtractAsync_ThinBitmap_PadsOnlyShortAxisAndRetainsEnhancedScale()
    {
        int widthAtUpscalingBoundary = (int)Math.Floor(OcrEngine.MaxImageDimension / 1.5);
        var recognizer = new FakeRecognizer { Document = MakeDocument() };
        var service = new TextExtractorService(new BitmapPreprocessor(), recognizer);
        using var bitmap = new Bitmap(widthAtUpscalingBoundary, 40, PixelFormat.Format32bppArgb);
        var request = new OcrExtractionRequest(bitmap, EnglishLanguage, OcrCaptureMode.Region);

        await service.ExtractAsync(request, CancellationToken.None);

        Assert.AreEqual((int)Math.Round(widthAtUpscalingBoundary * 1.5), recognizer.ReceivedBitmapWidth);
        Assert.AreEqual(80, recognizer.ReceivedBitmapHeight);
    }

    [TestMethod]
    public async Task ExtractAsync_DefaultScaleNearLimit_DoesNotPadLongAxisPastLimit()
    {
        int widthNearLimit = (int)OcrEngine.MaxImageDimension - 10;
        var recognizer = new FakeRecognizer { Document = MakeDocument() };
        var service = new TextExtractorService(new BitmapPreprocessor(), recognizer);
        using var bitmap = new Bitmap(widthNearLimit, 40, PixelFormat.Format32bppArgb);
        var request = new OcrExtractionRequest(bitmap, EnglishLanguage, OcrCaptureMode.Region);

        await service.ExtractAsync(request, CancellationToken.None);

        Assert.AreEqual(widthNearLimit, recognizer.ReceivedBitmapWidth);
        Assert.AreEqual(80, recognizer.ReceivedBitmapHeight);
        Assert.IsTrue(recognizer.ReceivedBitmapWidth <= OcrEngine.MaxImageDimension);
        Assert.IsTrue(recognizer.ReceivedBitmapHeight <= OcrEngine.MaxImageDimension);
    }

    [TestMethod]
    public async Task ExtractAsync_OversizedWordBitmap_DownscalesAndMapsClickPoint()
    {
        int maximumDimension = (int)OcrEngine.MaxImageDimension;
        int sourceWidth = maximumDimension + 100;
        const int SourceHeight = 200;
        double expectedScaleX = maximumDimension / (double)sourceWidth;
        double expectedScaleY = Math.Round(SourceHeight * expectedScaleX) / SourceHeight;
        var clickedWord = new OcrWordData(
            "Target",
            new OcrRect(
                (maximumDimension / 2d) - 5,
                (100 * expectedScaleY) - 5,
                10,
                10));
        var recognizer = new FakeRecognizer
        {
            Document = new OcrDocument(
            [
                new OcrLineData("Target", clickedWord.Bounds, [clickedWord]),
            ]),
        };
        var service = new TextExtractorService(new BitmapPreprocessor(), recognizer);
        using var bitmap = new Bitmap(sourceWidth, SourceHeight, PixelFormat.Format32bppArgb);
        var request = new OcrExtractionRequest(
            bitmap,
            EnglishLanguage,
            OcrCaptureMode.Word,
            new OcrPoint(sourceWidth / 2d, 100));

        string result = await service.ExtractAsync(request, CancellationToken.None);

        Assert.AreEqual("Target", result);
        Assert.IsTrue(recognizer.ReceivedBitmapWidth <= maximumDimension);
        Assert.IsTrue(recognizer.ReceivedBitmapHeight <= maximumDimension);
    }

    [TestMethod]
    public async Task ExtractAsync_WordMode_NoHit_ReturnsEmptyString()
    {
        var recognizer = new FakeRecognizer { Document = MakeDocument() };
        var service = new TextExtractorService(new BitmapPreprocessor(), recognizer);
        using var bitmap = new Bitmap(10, 10, PixelFormat.Format32bppArgb);
        var request = new OcrExtractionRequest(bitmap, EnglishLanguage, OcrCaptureMode.Word, new OcrPoint(50, 50));

        string result = await service.ExtractAsync(request, CancellationToken.None);

        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public async Task ExtractAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var recognizer = new FakeRecognizer { Document = MakeDocument() };
        var service = new TextExtractorService(new BitmapPreprocessor(), recognizer);
        using var bitmap = new Bitmap(200, 100, PixelFormat.Format32bppArgb);
        var request = new OcrExtractionRequest(bitmap, EnglishLanguage, OcrCaptureMode.Region);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => service.ExtractAsync(request, cts.Token));
    }

    [TestMethod]
    public async Task ExtractAsync_NullRequest_ThrowsArgumentNullException()
    {
        var recognizer = new FakeRecognizer { Document = MakeDocument() };
        var service = new TextExtractorService(new BitmapPreprocessor(), recognizer);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => service.ExtractAsync(null!, CancellationToken.None));
    }
}
