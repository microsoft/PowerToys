// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using ScreenTranslator.Core.Layout;
using ScreenTranslator.Core.Translation;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace ScreenTranslator.Core.Ocr;

/// <summary>
/// Universal fallback OCR backend using Windows.Media.Ocr.OcrEngine.
/// Available across all Windows 10/11 installations.
/// </summary>
public sealed class WindowsMediaOcrBackend : IOcrBackend
{
    public string BackendName => "Windows.Media.Ocr (Standard/Fallback)";

    public bool IsAvailable => OcrEngine.AvailableRecognizerLanguages.Count > 0;

    public async Task<IReadOnlyList<TranslationLine>> RecognizeTextAsync(
        SoftwareBitmap bitmap,
        PhysicalRect capturedRegionPhysical,
        string? sourceLanguageTag = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (bitmap == null || bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0)
        {
            return Array.Empty<TranslationLine>();
        }

        Language? language = ResolveLanguage(sourceLanguageTag);

        OcrEngine? engine = language != null
            ? OcrEngine.TryCreateFromLanguage(language)
            : (OcrEngine.IsLanguageSupported(new Language("en-US"))
                ? OcrEngine.TryCreateFromLanguage(new Language("en-US"))
                : (OcrEngine.AvailableRecognizerLanguages.Count > 0
                    ? OcrEngine.TryCreateFromLanguage(OcrEngine.AvailableRecognizerLanguages[0])
                    : null));

        if (engine == null)
        {
            throw new InvalidOperationException("Windows OCR could not be created because no supported OCR language is installed.");
        }

        bool convertedLocally = false;
        SoftwareBitmap convertedBitmap;
        if (bitmap.BitmapPixelFormat == BitmapPixelFormat.Bgra8 && bitmap.BitmapAlphaMode == BitmapAlphaMode.Premultiplied)
        {
            convertedBitmap = bitmap;
        }
        else
        {
            convertedBitmap = SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            convertedLocally = true;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            OcrResult result = await engine.RecognizeAsync(convertedBitmap);
            cancellationToken.ThrowIfCancellationRequested();

            if (result == null || result.Lines == null || result.Lines.Count == 0)
            {
                return Array.Empty<TranslationLine>();
            }

            List<TranslationLine> lines = new();

            foreach (OcrLine ocrLine in result.Lines)
            {
                if (string.IsNullOrWhiteSpace(ocrLine.Text))
                {
                    continue;
                }

                List<PhysicalRect> wordRects = new();
                foreach (OcrWord word in ocrLine.Words)
                {
                    wordRects.Add(new PhysicalRect(
                        capturedRegionPhysical.X + word.BoundingRect.X,
                        capturedRegionPhysical.Y + word.BoundingRect.Y,
                        word.BoundingRect.Width,
                        word.BoundingRect.Height));
                }

                PhysicalRect lineBoundingBox = OverlayLayoutHelper.CombineWordRects(wordRects);
                if (lineBoundingBox.IsEmpty)
                {
                    lineBoundingBox = new PhysicalRect(
                        capturedRegionPhysical.X,
                        capturedRegionPhysical.Y,
                        capturedRegionPhysical.Width,
                        capturedRegionPhysical.Height);
                }

                lines.Add(new TranslationLine(ocrLine.Text.Trim(), lineBoundingBox, 1.0, null));
            }

            return lines;
        }
        finally
        {
            if (convertedLocally)
            {
                convertedBitmap.Dispose();
            }
        }
    }

    public static Language? ResolveLanguage(string? sourceLanguageTag)
    {
        if (!string.IsNullOrWhiteSpace(sourceLanguageTag) &&
            !string.Equals(sourceLanguageTag, "auto", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(sourceLanguageTag, "system", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var requestedLang = new Language(sourceLanguageTag.Trim());
                if (OcrEngine.IsLanguageSupported(requestedLang))
                {
                    return requestedLang;
                }

                throw new InvalidOperationException(
                    $"Windows OCR language '{sourceLanguageTag}' is not installed. Install the corresponding Windows language and OCR capability or choose Auto.");
            }
            catch (ArgumentException ex)
            {
                throw new InvalidOperationException($"OCR language tag '{sourceLanguageTag}' is invalid.", ex);
            }
        }

        return GetPreferredLanguage();
    }

    public static Language? GetPreferredLanguage()
    {
        try
        {
            var userLanguages = Windows.System.UserProfile.GlobalizationPreferences.Languages;
            if (userLanguages != null && userLanguages.Count > 0)
            {
                foreach (string langTag in userLanguages)
                {
                    Language lang = new(langTag);
                    if (OcrEngine.IsLanguageSupported(lang))
                    {
                        return lang;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Exception querying preferred globalization languages: {ex.Message}");
        }

        return OcrEngine.AvailableRecognizerLanguages.Count > 0 ? OcrEngine.AvailableRecognizerLanguages[0] : null;
    }
}
