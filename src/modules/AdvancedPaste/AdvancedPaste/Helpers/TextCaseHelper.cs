// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AdvancedPaste.Helpers;

public static class TextCaseHelper
{
    private enum PendingSentenceTerminator
    {
        None,
        Ambiguous,
        Hard,
    }

    private enum IdentifierRuneKind
    {
        Separator,
        UppercaseLetter,
        LowercaseLetter,
        UncasedLetter,
        Digit,
        Mark,
    }

    public static string ToLowerCase(string text, CultureInfo culture = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.ToLower(culture ?? CultureInfo.CurrentCulture);
    }

    public static string ToUpperCase(string text, CultureInfo culture = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.ToUpper(culture ?? CultureInfo.CurrentCulture);
    }

    public static string ToTitleCase(string text, CultureInfo culture = null)
    {
        ArgumentNullException.ThrowIfNull(text);

        culture ??= CultureInfo.CurrentCulture;

        // TextInfo.ToTitleCase preserves words that are already all-uppercase as acronyms.
        // Normalize first so this action behaves as a case conversion (for example,
        // "HELLO WORLD" -> "Hello World") rather than preserving the original casing.
        return culture.TextInfo.ToTitleCase(text.ToLower(culture));
    }

    // Unicode sentence-break properties come from SentenceBreakData. The state
    // machine applies the deterministic PowerToys casing policy to those facts.
    public static string ToSentenceCase(string text, CultureInfo culture = null)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length == 0)
        {
            return string.Empty;
        }

        culture ??= CultureInfo.CurrentCulture;

        var result = new StringBuilder(text.Length);
        var segment = new StringBuilder(text.Length);

        var pendingTerminator = PendingSentenceTerminator.None;
        var directAmbiguousContext = false;
        var dottedInitialismComponentCount = 0;
        var currentComponentLetterCount = 0;
        var isDottedInitialismCandidate = true;
        var pendingTerminatorClosesDottedInitialism = false;

        var previousWasCarriageReturn = false;
        for (var index = 0; index < text.Length;)
        {
            var status = Rune.DecodeFromUtf16(text.AsSpan(index), out var rune, out var charsConsumed);
            if (status != OperationStatus.Done)
            {
                // Malformed UTF-16 is opaque to Unicode property and casing APIs.
                // Preserve the code unit and apply the established non-property policy.
                segment.Append(text[index]);
                index++;
                directAmbiguousContext = false;
                pendingTerminatorClosesDottedInitialism = false;
                previousWasCarriageReturn = false;
                continue;
            }

            index += charsConsumed;
            var sentenceBreakType = SentenceBreakData.GetSentenceBreakType(rune.Value);

            if (pendingTerminator != PendingSentenceTerminator.None)
            {
                if (sentenceBreakType == SentenceBreakType.SContinue)
                {
                    pendingTerminator = PendingSentenceTerminator.None;
                    pendingTerminatorClosesDottedInitialism = false;
                    dottedInitialismComponentCount = 0;
                    currentComponentLetterCount = 0;
                    isDottedInitialismCandidate = true;
                }
                else if (!IsSentenceBoundaryIgnorable(sentenceBreakType))
                {
                    if (IsTerminatorTrailingContext(sentenceBreakType))
                    {
                        directAmbiguousContext = false;
                    }
                    else if (sentenceBreakType is SentenceBreakType.ATerm or SentenceBreakType.STerm)
                    {
                        pendingTerminator = sentenceBreakType == SentenceBreakType.STerm ?
                            PendingSentenceTerminator.Hard :
                            PendingSentenceTerminator.Ambiguous;
                        directAmbiguousContext = false;
                        pendingTerminatorClosesDottedInitialism = false;
                    }
                    else if (IsSentenceBreakLetter(sentenceBreakType) || sentenceBreakType == SentenceBreakType.Numeric)
                    {
                        var periodIsInternal = pendingTerminator == PendingSentenceTerminator.Ambiguous &&
                                               (directAmbiguousContext ||
                                                (pendingTerminatorClosesDottedInitialism && sentenceBreakType == SentenceBreakType.Lower));
                        if (!periodIsInternal)
                        {
                            AppendSentenceSegment(result, segment, culture);
                            segment.Clear();
                        }

                        if (directAmbiguousContext)
                        {
                            if (pendingTerminator == PendingSentenceTerminator.Ambiguous &&
                                isDottedInitialismCandidate &&
                                currentComponentLetterCount == 1 &&
                                IsSentenceBreakLetter(sentenceBreakType))
                            {
                                dottedInitialismComponentCount++;
                            }
                            else
                            {
                                dottedInitialismComponentCount = 0;
                                isDottedInitialismCandidate = false;
                            }
                        }
                        else
                        {
                            dottedInitialismComponentCount = 0;
                            isDottedInitialismCandidate = true;
                        }

                        currentComponentLetterCount = 0;
                        pendingTerminator = PendingSentenceTerminator.None;
                        pendingTerminatorClosesDottedInitialism = false;
                    }
                    else
                    {
                        directAmbiguousContext = false;
                        pendingTerminatorClosesDottedInitialism = false;
                    }
                }
            }

            AppendRune(segment, rune);

            var isCarriageReturn = sentenceBreakType == SentenceBreakType.CR;
            var isLineBoundary = IsLineBoundary(sentenceBreakType, rune);
            if (isLineBoundary && !(sentenceBreakType == SentenceBreakType.LF && previousWasCarriageReturn))
            {
                AppendSentenceSegment(result, segment, culture);
                segment.Clear();
                pendingTerminator = PendingSentenceTerminator.None;
                dottedInitialismComponentCount = 0;
                currentComponentLetterCount = 0;
                isDottedInitialismCandidate = true;
            }
            else if (sentenceBreakType == SentenceBreakType.STerm)
            {
                pendingTerminator = PendingSentenceTerminator.Hard;
                directAmbiguousContext = false;
                pendingTerminatorClosesDottedInitialism = false;
                dottedInitialismComponentCount = 0;
                currentComponentLetterCount = 0;
                isDottedInitialismCandidate = true;
            }
            else if (sentenceBreakType == SentenceBreakType.ATerm && pendingTerminator == PendingSentenceTerminator.None)
            {
                pendingTerminator = PendingSentenceTerminator.Ambiguous;
                directAmbiguousContext = true;
                pendingTerminatorClosesDottedInitialism = isDottedInitialismCandidate &&
                                                          dottedInitialismComponentCount > 0 &&
                                                          currentComponentLetterCount == 1;
            }
            else if (IsSentenceBreakLetter(sentenceBreakType))
            {
                currentComponentLetterCount++;
            }
            else if (Rune.IsNumber(rune))
            {
                // Broader than Sentence_Break=Numeric by design: any Unicode number
                // makes a dotted token ineligible to be an alphabetic initialism.
                isDottedInitialismCandidate = false;
            }
            else if (pendingTerminator == PendingSentenceTerminator.None &&
                     !IsSentenceBoundaryIgnorable(sentenceBreakType))
            {
                dottedInitialismComponentCount = 0;
                currentComponentLetterCount = 0;
                isDottedInitialismCandidate = true;
            }

            previousWasCarriageReturn = isCarriageReturn;
        }

        AppendSentenceSegment(result, segment, culture);
        return result.ToString();
    }

    private static void AppendSentenceSegment(StringBuilder result, StringBuilder segment, CultureInfo culture)
    {
        var segmentText = segment.ToString();
        var targetIndex = -1;
        for (var index = 0; index < segmentText.Length;)
        {
            var status = Rune.DecodeFromUtf16(segmentText.AsSpan(index), out var rune, out var charsConsumed);
            if (status == OperationStatus.Done)
            {
                if (Rune.IsLetter(rune))
                {
                    targetIndex = index;
                    break;
                }

                index += charsConsumed;
                continue;
            }

            index++;
        }

        if (targetIndex < 0)
        {
            result.Append(segmentText);
            return;
        }

        result.Append(segmentText.AsSpan(0, targetIndex));
        var loweredTail = segmentText[targetIndex..].ToLower(culture);
        var loweredIndex = 0;
        while (loweredIndex < loweredTail.Length)
        {
            var status = Rune.DecodeFromUtf16(loweredTail.AsSpan(loweredIndex), out var rune, out var charsConsumed);
            if (status == OperationStatus.Done && Rune.IsLetter(rune))
            {
                result.Append(rune.ToString().ToUpper(culture));
                result.Append(loweredTail.AsSpan(loweredIndex + charsConsumed));
                return;
            }

            loweredIndex += status == OperationStatus.Done ? charsConsumed : 1;
        }

        result.Append(loweredTail);
    }

    private static void AppendRune(StringBuilder builder, Rune rune)
    {
        Span<char> buffer = stackalloc char[2];
        var charsWritten = rune.EncodeToUtf16(buffer);
        builder.Append(buffer[..charsWritten]);
    }

    public static string ToggleCase(string text, CultureInfo culture = null)
    {
        ArgumentNullException.ThrowIfNull(text);

        culture ??= CultureInfo.CurrentCulture;
        var result = new StringBuilder(text.Length);

        for (var index = 0; index < text.Length;)
        {
            var status = Rune.DecodeFromUtf16(text.AsSpan(index), out var rune, out var charsConsumed);
            if (status != OperationStatus.Done)
            {
                result.Append(text[index]);
                index++;
                continue;
            }

            index += charsConsumed;
            var category = Rune.GetUnicodeCategory(rune);

            if (category == UnicodeCategory.LowercaseLetter)
            {
                AppendRune(result, Rune.ToUpper(rune, culture));
            }
            else if (category is UnicodeCategory.UppercaseLetter or UnicodeCategory.TitlecaseLetter)
            {
                AppendRune(result, Rune.ToLower(rune, culture));
            }
            else
            {
                AppendRune(result, rune);
            }
        }

        return result.ToString();
    }

    public static string ToCamelCase(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return ToMixedCaseIdentifier(TokenizeIdentifier(text), capitalizeFirstToken: false);
    }

    public static string ToPascalCase(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return ToMixedCaseIdentifier(TokenizeIdentifier(text), capitalizeFirstToken: true);
    }

    public static string ToSnakeCase(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return JoinIdentifierTokens(TokenizeIdentifier(text), "_", uppercase: false);
    }

    public static string ToScreamingSnakeCase(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return JoinIdentifierTokens(TokenizeIdentifier(text), "_", uppercase: true);
    }

    public static string ToKebabCase(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return JoinIdentifierTokens(TokenizeIdentifier(text), "-", uppercase: false);
    }

    private static string ToMixedCaseIdentifier(IReadOnlyList<string> tokens, bool capitalizeFirstToken)
    {
        if (tokens.Count == 0)
        {
            return string.Empty;
        }

        var result = new StringBuilder();

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i].ToLowerInvariant();
            var capitalize = i > 0 || capitalizeFirstToken;
            result.Append(capitalize ? CapitalizeIdentifierToken(token) : token);
        }

        return result.ToString();
    }

    private static string JoinIdentifierTokens(IReadOnlyList<string> tokens, string separator, bool uppercase)
    {
        if (tokens.Count == 0)
        {
            return string.Empty;
        }

        var result = new StringBuilder();

        for (var i = 0; i < tokens.Count; i++)
        {
            if (i > 0)
            {
                result.Append(separator);
            }

            result.Append(uppercase ? tokens[i].ToUpperInvariant() : tokens[i].ToLowerInvariant());
        }

        return result.ToString();
    }

    private static string CapitalizeIdentifierToken(string token)
    {
        var result = new StringBuilder(token.Length);
        var capitalized = false;

        foreach (var rune in token.EnumerateRunes())
        {
            if (!capitalized && Rune.IsLetter(rune))
            {
                result.Append(rune.ToString().ToUpperInvariant());
                capitalized = true;
            }
            else
            {
                AppendRune(result, rune);
            }
        }

        return result.ToString();
    }

    private static IReadOnlyList<string> TokenizeIdentifier(string text)
    {
        var runes = new List<Rune>();
        foreach (var rune in text.EnumerateRunes())
        {
            runes.Add(rune);
        }

        var tokens = new List<string>();
        var currentToken = new StringBuilder();
        var previousKind = IdentifierRuneKind.Separator;

        for (var i = 0; i < runes.Count; i++)
        {
            var rune = runes[i];
            var currentKind = GetIdentifierRuneKind(rune);

            if (currentKind == IdentifierRuneKind.Separator)
            {
                AddTokenIfAny(tokens, currentToken);
                previousKind = IdentifierRuneKind.Separator;
                continue;
            }

            if (currentKind == IdentifierRuneKind.Mark)
            {
                // Preserve a combining mark only when it follows a base in the current
                // token; an orphan mark must not migrate across a removed separator.
                if (currentToken.Length > 0)
                {
                    AppendRune(currentToken, rune);
                }

                continue;
            }

            var nextKind = GetNextSignificantIdentifierRuneKind(runes, i + 1);
            var splitBeforeCurrent = currentToken.Length > 0 &&
                                     currentKind == IdentifierRuneKind.UppercaseLetter &&
                                     (previousKind is IdentifierRuneKind.LowercaseLetter or IdentifierRuneKind.UncasedLetter or IdentifierRuneKind.Digit ||
                                      (previousKind == IdentifierRuneKind.UppercaseLetter && nextKind == IdentifierRuneKind.LowercaseLetter));

            if (splitBeforeCurrent)
            {
                AddTokenIfAny(tokens, currentToken);
            }

            AppendRune(currentToken, rune);
            previousKind = currentKind;
        }

        AddTokenIfAny(tokens, currentToken);
        return tokens;
    }

    private static void AddTokenIfAny(List<string> tokens, StringBuilder currentToken)
    {
        if (currentToken.Length == 0)
        {
            return;
        }

        tokens.Add(currentToken.ToString());
        currentToken.Clear();
    }

    private static IdentifierRuneKind GetNextSignificantIdentifierRuneKind(IReadOnlyList<Rune> runes, int startIndex)
    {
        for (var i = startIndex; i < runes.Count; i++)
        {
            var kind = GetIdentifierRuneKind(runes[i]);
            if (kind != IdentifierRuneKind.Mark)
            {
                return kind;
            }
        }

        return IdentifierRuneKind.Separator;
    }

    private static IdentifierRuneKind GetIdentifierRuneKind(Rune rune)
    {
        return Rune.GetUnicodeCategory(rune) switch
        {
            UnicodeCategory.UppercaseLetter or UnicodeCategory.TitlecaseLetter => IdentifierRuneKind.UppercaseLetter,
            UnicodeCategory.LowercaseLetter => IdentifierRuneKind.LowercaseLetter,
            UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter => IdentifierRuneKind.UncasedLetter,
            UnicodeCategory.DecimalDigitNumber or UnicodeCategory.LetterNumber or UnicodeCategory.OtherNumber => IdentifierRuneKind.Digit,
            UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark => IdentifierRuneKind.Mark,
            _ => IdentifierRuneKind.Separator,
        };
    }

    private static bool IsSentenceBreakLetter(SentenceBreakType type) =>
        type is SentenceBreakType.Upper or SentenceBreakType.Lower or SentenceBreakType.OLetter;

    private static bool IsSentenceBoundaryIgnorable(SentenceBreakType type) =>
        type is SentenceBreakType.Extend or SentenceBreakType.Format;

    private static bool IsTerminatorTrailingContext(SentenceBreakType type) =>
        type is SentenceBreakType.Close or SentenceBreakType.Sp;

    private static bool IsLineBoundary(SentenceBreakType type, Rune rune)
    {
        // VT and FF are an explicit PowerToys clipboard-boundary extension;
        // Unicode classifies them as Sp, not Sep.
        return type is SentenceBreakType.CR or SentenceBreakType.LF or SentenceBreakType.Sep ||
               rune.Value is 0x000B or 0x000C;
    }
}
