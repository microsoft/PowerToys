// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Markup;
using PowerOCR.Models;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using BitmapDecoder = Windows.Graphics.Imaging.BitmapDecoder;

namespace PowerOCR.Helpers;

internal static partial class OcrExtensions
{
    public static void GetTextFromOcrLine(this OcrLine ocrLine, bool isSpaceJoiningOCRLang, StringBuilder text)
    {
        // (when OCR language is zh or ja)
        // matches words in a space-joining language, which contains:
        // - one letter that is not in "other letters" (CJK characters are "other letters")
        // - one number digit
        // - any words longer than one character
        // Chinese and Japanese characters are single-character words
        // when a word is one punctuation/symbol, join it without spaces
        if (isSpaceJoiningOCRLang)
        {
            text.AppendLine(ocrLine.Text);
        }
        else
        {
            bool isFirstWord = true;
            bool isPrevWordSpaceJoining = false;

            Regex regexSpaceJoiningWord = SpaceJoiningRegex();

            foreach (OcrWord ocrWord in ocrLine.Words)
            {
                string wordString = ocrWord.Text;

                bool isThisWordSpaceJoining = regexSpaceJoiningWord.IsMatch(wordString);

                _ = isFirstWord || (!isThisWordSpaceJoining && !isPrevWordSpaceJoining)
                    ? text.Append(wordString)
                    : text.Append(' ').Append(wordString);

                isFirstWord = false;
                isPrevWordSpaceJoining = isThisWordSpaceJoining;
            }
        }
    }

    public static async Task<string> ExtractText(Bitmap bmp, Language? preferredLanguage, System.Windows.Point? singlePoint = null)
    {
        Language? selectedLanguage = preferredLanguage ?? LanguageHelper.GetOCRLanguage();
        if (selectedLanguage == null)
        {
            return string.Empty;
        }

        XmlLanguage lang = XmlLanguage.GetLanguage(selectedLanguage.LanguageTag);
        CultureInfo culture = lang.GetEquivalentCulture();

        bool isSpaceJoiningLang = LanguageHelper.IsLanguageSpaceJoining(selectedLanguage);

        bool scaleBMP = true;

        if (singlePoint != null
            || bmp.Width * 1.5 > OcrEngine.MaxImageDimension)
        {
            scaleBMP = false;
        }

        using Bitmap scaledBitmap = scaleBMP ? ImageMethods.ScaleBitmapUniform(bmp, 1.5) : ImageMethods.ScaleBitmapUniform(bmp, 1.0);
        StringBuilder text = new();

        await using MemoryStream memoryStream = new();
        using WrappingStream wrappingStream = new(memoryStream);

        scaledBitmap.Save(wrappingStream, ImageFormat.Bmp);
        wrappingStream.Position = 0;
        BitmapDecoder bmpDecoder = await BitmapDecoder.CreateAsync(wrappingStream.AsRandomAccessStream());
        SoftwareBitmap softwareBmp = await bmpDecoder.GetSoftwareBitmapAsync();

        OcrEngine ocrEngine = OcrEngine.TryCreateFromLanguage(selectedLanguage);
        OcrResult ocrResult = await ocrEngine.RecognizeAsync(softwareBmp);

        GC.Collect();

        if (singlePoint == null)
        {
            foreach (OcrLine ocrLine in ocrResult.Lines)
            {
                ocrLine.GetTextFromOcrLine(isSpaceJoiningLang, text);
            }
        }
        else
        {
            Windows.Foundation.Point fPoint = new(singlePoint.Value.X, singlePoint.Value.Y);
            foreach (OcrLine ocrLine in ocrResult.Lines)
            {
                foreach (OcrWord ocrWord in ocrLine.Words)
                {
                    if (ocrWord.BoundingRect.Contains(fPoint))
                    {
                        _ = text.Append(ocrWord.Text);
                    }
                }
            }
        }

        if (culture.TextInfo.IsRightToLeft)
        {
            string[] textListLines = text.ToString().Split(new char[] { '\n', '\r' });

            _ = text.Clear();
            foreach (string textLine in textListLines)
            {
                List<string> wordArray = textLine.Split().ToList();
                wordArray.Reverse();
                _ = text.Append(string.Join(' ', wordArray));

                if (textLine.Length > 0)
                {
                    _ = text.Append('\n');
                }
            }

            return text.ToString();
        }

        return text.ToString();
    }

    [GeneratedRegex("(^[\\p{L}-[\\p{Lo}]]|\\p{Nd}$)|.{2,}")]
    private static partial Regex SpaceJoiningRegex();
}
