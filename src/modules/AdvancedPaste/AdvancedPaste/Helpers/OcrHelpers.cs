// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.System.UserProfile;

namespace AdvancedPaste.Helpers;

public static class OcrHelpers
{
    public static async Task<string> ExtractTextAsync(SoftwareBitmap bitmap, CancellationToken cancellationToken)
    {
        var ocrLanguage = GetOCRLanguage();
        cancellationToken.ThrowIfCancellationRequested();

        OcrEngine ocrEngine;
        if (ocrLanguage is not null)
        {
            ocrEngine = OcrEngine.TryCreateFromLanguage(ocrLanguage) ?? throw new InvalidOperationException("Unable to create OCR engine from specified language");
        }
        else
        {
            ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages() ?? throw new InvalidOperationException("Unable to create OCR engine from user profile language");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var ocrResult = await ocrEngine.RecognizeAsync(bitmap);

        return string.IsNullOrWhiteSpace(ocrResult.Text)
            ? throw new InvalidOperationException("Unable to extract text from image or image does not contain text")
            : ocrResult.Text;
    }

    private static Language GetOCRLanguage()
    {
        var userLanguageTags = GlobalizationPreferences.Languages.ToList();

        var languages = from language in OcrEngine.AvailableRecognizerLanguages
                        let tag = language.LanguageTag
                        where userLanguageTags.Contains(tag)
                        orderby userLanguageTags.IndexOf(tag)
                        select language;

        return languages.FirstOrDefault();
    }
}
