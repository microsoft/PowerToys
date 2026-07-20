// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerOCR.Core.Formatting;
using PowerOCR.Core.Imaging;
using PowerOCR.Core.Models;
using PowerOCR.Core.Ocr;
using Windows.Media.Ocr;

namespace PowerOCR.Core.Services;

public sealed class TextExtractorService : ITextExtractorService
{
    private readonly IBitmapPreprocessor _preprocessor;
    private readonly IOcrRecognizer _recognizer;

    public TextExtractorService(IBitmapPreprocessor preprocessor, IOcrRecognizer recognizer)
    {
        ArgumentNullException.ThrowIfNull(preprocessor);
        ArgumentNullException.ThrowIfNull(recognizer);
        _preprocessor = preprocessor;
        _recognizer = recognizer;
    }

    public async Task<string> ExtractAsync(
        OcrExtractionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        double scale = request.Mode == OcrCaptureMode.Word
            || (request.Bitmap.Width * 1.5) > OcrEngine.MaxImageDimension
            || (request.Bitmap.Height * 1.5) > OcrEngine.MaxImageDimension
                ? 1.0
                : 1.5;

        using PreparedBitmap prepared = _preprocessor.Prepare(request.Bitmap, scale);
        OcrDocument document = await _recognizer.RecognizeAsync(
            prepared.Bitmap,
            request.Language,
            cancellationToken);

#pragma warning disable CA2208
        return request.Mode switch
        {
            OcrCaptureMode.Region => OcrTextFormatter.FormatDocument(document, request.Language.LanguageTag),
            OcrCaptureMode.SingleLine => OcrTextFormatter.CollapseToSingleLine(
                OcrTextFormatter.FormatDocument(document, request.Language.LanguageTag)),
            OcrCaptureMode.Table => TableTextFormatter.Format(document.Lines, request.Language.LanguageTag),
            OcrCaptureMode.Word => GetClickedWord(document, TransformPoint(request.ClickPoint, prepared)),
            _ => throw new ArgumentOutOfRangeException(nameof(request.Mode), "Unknown OcrCaptureMode value."),
        };
#pragma warning restore CA2208
    }

    private static OcrPoint TransformPoint(OcrPoint? clickPoint, PreparedBitmap prepared)
    {
        if (clickPoint is null)
        {
            throw new InvalidOperationException("A click point is required for Word extraction mode.");
        }

        return new OcrPoint(
            (clickPoint.Value.X * prepared.Scale) + prepared.OffsetX,
            (clickPoint.Value.Y * prepared.Scale) + prepared.OffsetY);
    }

    private static string GetClickedWord(OcrDocument document, OcrPoint transformedPoint)
    {
        foreach (OcrWordData word in document.Words)
        {
            if (word.Bounds.Contains(transformedPoint))
            {
                return word.Text;
            }
        }

        return string.Empty;
    }
}
