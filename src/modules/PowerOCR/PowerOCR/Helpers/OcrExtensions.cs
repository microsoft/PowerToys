// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using PowerOCR.Models;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace PowerOCR.Helpers
{
    internal static class OcrExtensions
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

                Regex regexSpaceJoiningWord = new(@"(^[\p{L}-[\p{Lo}]]|\p{Nd}$)|.{2,}");

                foreach (OcrWord ocrWord in ocrLine.Words)
                {
                    string wordString = ocrWord.Text;

                    bool isThisWordSpaceJoining = regexSpaceJoiningWord.IsMatch(wordString);

                    if (isFirstWord || (!isThisWordSpaceJoining && !isPrevWordSpaceJoining))
                    {
                        _ = text.Append(wordString);
                    }
                    else
                    {
                        _ = text.Append(' ').Append(wordString);
                    }

                    isFirstWord = false;
                    isPrevWordSpaceJoining = isThisWordSpaceJoining;
                }
            }
        }

        public static async Task<string> GetRegionsTextAsTableAsync(OCROverlay passedWindow, Rectangle regionScaled, Language? language)
        {
            if (language is null)
            {
                return string.Empty;
            }

            Bitmap bmp = ImageMethods.GetRegionAsBitmap(passedWindow, regionScaled);

            bool scaleBMP = true;

            if (bmp.Width * 1.5 > OcrEngine.MaxImageDimension)
            {
                scaleBMP = false;
            }

            using Bitmap scaledBitmap = scaleBMP ? ImageMethods.ScaleBitmapUniform(bmp, 1.5) : ImageMethods.ScaleBitmapUniform(bmp, 1.0);
            DpiScale dpiScale = VisualTreeHelper.GetDpi(passedWindow);

            OcrResult ocrResult = await GetOcrResultFromImageAsync(scaledBitmap, language);
            List<WordBorder> wordBorders = ResultTable.ParseOcrResultIntoWordBorders(ocrResult, dpiScale);
            return ResultTable.GetWordsAsTable(wordBorders, dpiScale, LanguageHelper.IsLanguageSpaceJoining(language));
        }

        internal static async Task<OcrResult> GetOcrResultFromImageAsync(Bitmap bmp, Language language)
        {
            await using MemoryStream memoryStream = new();
            using WrappingStream wrappingStream = new(memoryStream);

            bmp.Save(wrappingStream, ImageFormat.Bmp);
            wrappingStream.Position = 0;

            BitmapDecoder bmpDecoder = await BitmapDecoder.CreateAsync(wrappingStream.AsRandomAccessStream());
            SoftwareBitmap softwareBmp = await bmpDecoder.GetSoftwareBitmapAsync();

            await memoryStream.DisposeAsync();
            await wrappingStream.DisposeAsync();

            OcrEngine ocrEngine = OcrEngine.TryCreateFromLanguage(language);
            return await ocrEngine.RecognizeAsync(softwareBmp);
        }
    }
}
