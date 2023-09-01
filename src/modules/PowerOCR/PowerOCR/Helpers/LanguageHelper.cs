// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Windows.Globalization;
using Windows.Media.Ocr;

namespace PowerOCR.Helpers;

internal static class LanguageHelper
{
    public static bool IsLanguageSpaceJoining(Language selectedLanguage)
    {
        if (selectedLanguage.LanguageTag.StartsWith("zh", StringComparison.InvariantCultureIgnoreCase))
        {
            return false;
        }
        else if (selectedLanguage.LanguageTag.Equals("ja", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    public static Language? GetOCRLanguage()
    {
        // use currently selected Language
        string inputLang = InputLanguageManager.Current.CurrentInputLanguage.Name;

        Language? selectedLanguage = new(inputLang);
        List<Language> possibleOcrLanguages = OcrEngine.AvailableRecognizerLanguages.ToList();

        if (possibleOcrLanguages.Count < 1)
        {
            MessageBox.Show("No possible OCR languages are installed.", "Text Grab");
            return null;
        }

        if (possibleOcrLanguages.All(l => l.LanguageTag != selectedLanguage.LanguageTag))
        {
            List<Language>? similarLanguages = possibleOcrLanguages.Where(
                la => la.AbbreviatedName == selectedLanguage.AbbreviatedName).ToList();

            if (similarLanguages != null)
            {
                selectedLanguage = similarLanguages.Count > 0
                    ? similarLanguages.FirstOrDefault()
                    : possibleOcrLanguages.FirstOrDefault();
            }
        }

        return selectedLanguage;
    }
}
