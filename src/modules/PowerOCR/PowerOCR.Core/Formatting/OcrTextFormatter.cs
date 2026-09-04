// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using PowerOCR.Core.Models;

namespace PowerOCR.Core.Formatting;

public static partial class OcrTextFormatter
{
    [GeneratedRegex(@"[ ]{2,}")]
    private static partial Regex RepeatedSpacesRegex();

    public static string FormatDocument(OcrDocument document, string languageTag)
    {
        bool useOcrLineText = UsesSpaces(languageTag);
        bool isRightToLeft = CultureInfo.GetCultureInfo(languageTag).TextInfo.IsRightToLeft;
        var lines = new List<string>(document.Lines.Count);

        foreach (OcrLineData line in document.Lines)
        {
            string text = useOcrLineText ? line.Text : JoinCjkAwareWords(line.Words);
            if (isRightToLeft)
            {
                text = string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Reverse());
            }

            lines.Add(text);
        }

        return string.Join(Environment.NewLine, lines).Trim();
    }

    public static string FormatSingleLine(OcrDocument document, string languageTag)
    {
        if (UsesSpaces(languageTag))
        {
            return CollapseToSingleLine(FormatDocument(document, languageTag));
        }

        return JoinCjkAwareWords(document.Words.ToList()).Trim();
    }

    public static string CollapseToSingleLine(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        string collapsed = text
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\n', ' ')
            .Replace('\r', ' ');
        return RepeatedSpacesRegex().Replace(collapsed, " ").Trim();
    }

    public static bool UsesSpaces(string languageTag)
        => !languageTag.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
           && !languageTag.StartsWith("ja", StringComparison.OrdinalIgnoreCase);

    internal static string JoinCjkAwareWords(IReadOnlyList<OcrWordData> words)
    {
        var builder = new StringBuilder();
        WordBoundary? previousBoundary = null;

        foreach (OcrWordData wordData in words)
        {
            string word = wordData.Text;
            if (!TryGetWordBoundary(word, out WordBoundary currentBoundary))
            {
                continue;
            }

            if (previousBoundary is WordBoundary previous
                && ShouldInsertSpace(previous, currentBoundary))
            {
                builder.Append(' ');
            }

            builder.Append(word);
            previousBoundary = currentBoundary;
        }

        return builder.ToString();
    }

    private static bool ShouldInsertSpace(WordBoundary left, WordBoundary right)
    {
        UnicodeCategory leftCategory = Rune.GetUnicodeCategory(left.LastRune);
        UnicodeCategory rightCategory = Rune.GetUnicodeCategory(right.FirstRune);

        if (IsOpeningPunctuation(leftCategory)
            || IsClosingPunctuation(rightCategory)
            || IsSuffixPunctuation(right.FirstRune)
            || IsCjkTightPunctuation(left.LastRune)
            || IsCjkTightPunctuation(right.FirstRune)
            || IsCombiningMark(rightCategory))
        {
            return false;
        }

        return left.UsesSpaces || right.UsesSpaces;
    }

    private static bool IsOpeningPunctuation(UnicodeCategory category)
        => category is UnicodeCategory.OpenPunctuation or UnicodeCategory.InitialQuotePunctuation;

    private static bool IsClosingPunctuation(UnicodeCategory category)
        => category is UnicodeCategory.ClosePunctuation or UnicodeCategory.FinalQuotePunctuation;

    private static bool IsSuffixPunctuation(Rune rune)
        => rune.Value is ',' or '.' or ':' or ';' or '!' or '?' or '%'
            or 0x2025 // TWO DOT LEADER
            or 0x2026; // HORIZONTAL ELLIPSIS

    private static bool IsCjkTightPunctuation(Rune rune)
        => rune.Value is 0x3001 // IDEOGRAPHIC COMMA
            or 0x3002 // IDEOGRAPHIC FULL STOP
            or 0x30FB // KATAKANA MIDDLE DOT
            or 0xFF01 // FULLWIDTH EXCLAMATION MARK
            or 0xFF05 // FULLWIDTH PERCENT SIGN
            or 0xFF0C // FULLWIDTH COMMA
            or 0xFF0E // FULLWIDTH FULL STOP
            or 0xFF1A // FULLWIDTH COLON
            or 0xFF1B // FULLWIDTH SEMICOLON
            or 0xFF1F // FULLWIDTH QUESTION MARK
            or 0xFF61 // HALFWIDTH IDEOGRAPHIC FULL STOP
            or 0xFF64 // HALFWIDTH IDEOGRAPHIC COMMA
            or 0xFF65; // HALFWIDTH KATAKANA MIDDLE DOT

    private static bool IsSpaceJoiningCategory(UnicodeCategory category)
        => category is UnicodeCategory.UppercaseLetter
            or UnicodeCategory.LowercaseLetter
            or UnicodeCategory.TitlecaseLetter
            or UnicodeCategory.DecimalDigitNumber;

    private static bool IsCombiningMark(UnicodeCategory category)
        => category is UnicodeCategory.NonSpacingMark
            or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.EnclosingMark;

    private static bool TryGetWordBoundary(string text, out WordBoundary boundary)
    {
        Rune firstRune = default;
        Rune lastRune = default;
        bool foundRune = false;
        bool usesSpaces = false;

        foreach (Rune rune in text.EnumerateRunes())
        {
            if (!foundRune)
            {
                firstRune = rune;
                foundRune = true;
            }

            lastRune = rune;
            usesSpaces |= IsSpaceJoiningCategory(Rune.GetUnicodeCategory(rune));
        }

        boundary = new WordBoundary(firstRune, lastRune, usesSpaces);
        return foundRune;
    }

    private readonly record struct WordBoundary(Rune FirstRune, Rune LastRune, bool UsesSpaces);
}
