// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using System.Text.RegularExpressions;
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
    }
}
