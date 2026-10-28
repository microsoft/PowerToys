// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

internal static class WordBoundaryRules
{
    internal static bool IsWordCharacter(char character)
        => char.IsLetterOrDigit(character) || character == '_';

    internal static bool IsTerminator(char character)
        => char.IsWhiteSpace(character) || char.IsPunctuation(character) || char.IsSymbol(character);

    internal static bool StartsAtWordBoundary(ReadOnlySpan<char> textBefore, ReadOnlySpan<char> trigger)
        => trigger.Length == 0 || !IsWordCharacter(trigger[0])
            || textBefore.Length == 0 || !IsWordCharacter(textBefore[^1]);
}
