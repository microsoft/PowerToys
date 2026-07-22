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
    public async Task ExtractAsync_TableMode_ReturnsTabbedText()
    {
        var recognizer = new FakeRecognizer { Document = MakeDocument() };
        var service = new TextExtractorService(new BitmapPreprocessor(), recognizer);
        using var bitmap = new Bitmap(200, 100, PixelFormat.Format32bppArgb);
        var request = new OcrExtractionRequest(bitmap, EnglishLanguage, OcrCaptureMode.Table);

        string result = await service.ExtractAsync(request, CancellationToken.None);

        Assert.IsFalse(string.IsNullOrEmpty(result));
    }

    [TestMethod]
    public async Task ExtractAsync_WordMode_ReturnsClickedWord()
    {
        var recognizer = new FakeRecognizer { Document = MakeDocument() };
        var service = new TextExtractorService(new BitmapPreprocessor(), recognizer);
        using var bitmap = new Bitmap(200, 100, PixelFormat.Format32bppArgb);

        // Click point within "Hello" bounds (0,0,50,20) — use scale=1 (small bitmap triggers padding)
        // With scale=1 and padding=8, "Hello" shifts to (8,8..58,28) in prepared bitmap
        // But the service transforms ClickPoint using prepared.Scale and prepared.Offset
        // The click is in original coordinates, so we click at (25, 10) for "Hello"
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
    public async Task ExtractAsync_UpscalingWithPaddingWouldExceedLimit_UsesUnscaledBitmap()
    {
        int widthAtUpscalingBoundary = (int)Math.Floor(OcrEngine.MaxImageDimension / 1.5);
        var recognizer = new FakeRecognizer { Document = MakeDocument() };
        var service = new TextExtractorService(new BitmapPreprocessor(), recognizer);
        using var bitmap = new Bitmap(widthAtUpscalingBoundary, 40, PixelFormat.Format32bppArgb);
        var request = new OcrExtractionRequest(bitmap, EnglishLanguage, OcrCaptureMode.Region);

        await service.ExtractAsync(request, CancellationToken.None);

        Assert.AreEqual(widthAtUpscalingBoundary + 16, recognizer.ReceivedBitmapWidth);
        Assert.AreEqual(80, recognizer.ReceivedBitmapHeight);
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
