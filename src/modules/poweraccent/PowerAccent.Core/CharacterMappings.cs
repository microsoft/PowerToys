// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Character mapping data has moved to PowerAccent.Common.CharacterMappings.
// This file provides a thin adapter so that callers in PowerAccent.Core can pass the
// WinRT LetterKey (from PowerAccentKeyboardService) without needing to know about the
// managed copy defined in PowerAccent.Common.
using CommonLanguage = global::PowerAccent.Common.Language;
using CommonLetterKey = global::PowerAccent.Common.LetterKey;
using CommonMappings = global::PowerAccent.Common.CharacterMappings;
using WinRtLetterKey = PowerToys.PowerAccentKeyboardService.LetterKey;

namespace PowerAccent.Core;

internal static class CharacterMappings
{
    public static string[] GetCharacters(WinRtLetterKey letter, CommonLanguage[] langs)
    {
        // The managed and WinRT LetterKey enums share identical numeric values, so a
        // direct cast via int is safe. If the IDL values ever change, the unit tests
        // in PowerAccent.Common will catch the mismatch.
        var managedKey = (CommonLetterKey)(int)letter;
        return CommonMappings.GetCharacters(managedKey, langs);
    }
}
