// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using PowerOCR.Core.Formatting;
using PowerOCR.Core.Imaging;
using PowerOCR.Core.Models;
using PowerOCR.Core.Ocr;
using Windows.Media.Ocr;

namespace PowerOCR.Core.Services;

public sealed class TextExtractorService : ITextExtractorService
{
    private const double DefaultScale = 1.0;
    private const double EnhancedScale = 1.5;

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

        double scale = SelectScale(request.Bitmap, request.Mode);

        using PreparedBitmap prepared = _preprocessor.Prepare(request.Bitmap, scale);
        if (!FitsOcrLimit(prepared.Bitmap.Size))
        {
            throw new InvalidOperationException("The prepared bitmap exceeds the OCR engine's maximum dimensions.");
        }

        OcrDocument document = await _recognizer.RecognizeAsync(
            prepared.Bitmap,
            request.Language,
            cancellationToken);

#pragma warning disable CA2208
        return request.Mode switch
        {
            OcrCaptureMode.Region => OcrTextFormatter.FormatDocument(document, request.Language.LanguageTag),
            OcrCaptureMode.SingleLine => OcrTextFormatter.FormatSingleLine(document, request.Language.LanguageTag),
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
            (clickPoint.Value.X * prepared.ScaleX) + prepared.OffsetX,
            (clickPoint.Value.Y * prepared.ScaleY) + prepared.OffsetY);
    }

    private double SelectScale(Bitmap bitmap, OcrCaptureMode mode)
    {
        if (mode != OcrCaptureMode.Word
            && (bitmap.Width * EnhancedScale) <= OcrEngine.MaxImageDimension
            && (bitmap.Height * EnhancedScale) <= OcrEngine.MaxImageDimension
            && FitsOcrLimit(_preprocessor.GetOutputSize(bitmap, EnhancedScale)))
        {
            return EnhancedScale;
        }

        if (FitsOcrLimit(_preprocessor.GetOutputSize(bitmap, DefaultScale)))
        {
            return DefaultScale;
        }

        double maximumDimension = OcrEngine.MaxImageDimension;
        double scale = Math.Min(
            DefaultScale,
            Math.Min(maximumDimension / bitmap.Width, maximumDimension / bitmap.Height));

        if (!FitsOcrLimit(_preprocessor.GetOutputSize(bitmap, scale)))
        {
            throw new InvalidOperationException("The bitmap could not be scaled within the OCR engine's maximum dimensions.");
        }

        return scale;
    }

    private static bool FitsOcrLimit(Size size)
        => size.Width <= OcrEngine.MaxImageDimension
            && size.Height <= OcrEngine.MaxImageDimension;

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
