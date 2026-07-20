// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using PowerOCR.Core.Models;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace PowerOCR.Core.Ocr;

public sealed class WindowsOcrRecognizer : IOcrRecognizer
{
    public async Task<OcrDocument> RecognizeAsync(
        Bitmap bitmap,
        Language language,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentNullException.ThrowIfNull(language);

        OcrEngine? engine = OcrEngine.TryCreateFromLanguage(language);
        if (engine is null)
        {
            throw new InvalidOperationException(
                $"OCR engine could not be created for language '{language.LanguageTag}'.");
        }

        var stream = new MemoryStream();
        try
        {
            bitmap.Save(stream, ImageFormat.Bmp);
            stream.Position = 0;

            var randomAccessStream = stream.AsRandomAccessStream();
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(randomAccessStream).AsTask(cancellationToken);
            SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync().AsTask(cancellationToken);

            try
            {
                Windows.Media.Ocr.OcrResult ocrResult =
                    await engine.RecognizeAsync(softwareBitmap).AsTask(cancellationToken);

                IReadOnlyList<OcrLineData> lines = ocrResult.Lines
                    .Select(MapLine)
                    .ToList();

                return new OcrDocument(lines);
            }
            finally
            {
                softwareBitmap.Dispose();
            }
        }
        finally
        {
            stream.Dispose();
        }
    }

    private static OcrLineData MapLine(Windows.Media.Ocr.OcrLine line)
    {
        IReadOnlyList<OcrWordData> words = line.Words
            .Select(word => new OcrWordData(
                word.Text,
                new OcrRect(
                    word.BoundingRect.X,
                    word.BoundingRect.Y,
                    word.BoundingRect.Width,
                    word.BoundingRect.Height)))
            .ToList();

        OcrRect bounds = words.Count == 0
            ? new OcrRect(0, 0, 0, 0)
            : words.Select(word => word.Bounds).Aggregate((left, right) => left.Union(right));
        return new OcrLineData(line.Text, bounds, words);
    }
}
