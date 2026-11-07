// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using System.Globalization;
using System.Text;

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Where words start and end, for the keyboard hook and the editor's reachability check alike. A word start is judged
/// on whole Unicode characters: a surrogate pair is one, and a combining mark goes with the character before it.
/// </summary>
internal static class WordBoundaryRules
{
    // Letters, digits and '_', and the combining marks that continue them.
    internal static bool IsWordCharacter(Rune character)
        => Rune.IsLetterOrDigit(character) || character.Value == '_' || IsCombiningMark(character);

    // Half a surrogate pair is taken for part of a word, so a trigger next to one never expands.
    internal static bool IsWordCharacter(char character)
        => char.IsSurrogate(character) || IsWordCharacter(new Rune(character));

    // A character inside a word never ends it: '_' is connector punctuation, but addr_line is one word.
    internal static bool IsTerminator(char character)
        => !IsWordCharacter(character)
            && (char.IsWhiteSpace(character) || char.IsPunctuation(character) || char.IsSymbol(character));

    internal static bool StartsAtWordBoundary(ReadOnlySpan<char> textBefore, ReadOnlySpan<char> trigger)
        => trigger.IsEmpty || !StartsWithWordCharacter(trigger) || !EndsInWord(textBefore);

    private static bool StartsWithWordCharacter(ReadOnlySpan<char> text)
        => Rune.DecodeFromUtf16(text, out Rune first, out _) != OperationStatus.Done || IsWordCharacter(first);

    // Decided by the last character that isn't a combining mark: e followed by U+0301 ends in a letter, and an emoji
    // followed by U+FE0F in a symbol. Half a surrogate pair, or marks with nothing before them, count as a word.
    private static bool EndsInWord(ReadOnlySpan<char> text)
    {
        for (int end = text.Length; end > 0;)
        {
            if (Rune.DecodeLastFromUtf16(text[..end], out Rune last, out int length) != OperationStatus.Done)
            {
                return true;
            }

            if (!IsCombiningMark(last))
            {
                return IsWordCharacter(last);
            }

            end -= length;
        }

        return !text.IsEmpty;
    }

    private static bool IsCombiningMark(Rune character)
        => Rune.GetUnicodeCategory(character) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark;
}
