// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Pure keystroke -> display-string formatting, ported from the team4
// KeystrokeOverlay KeystrokeEvent.cs (ToString / IsShortcut / GetKeyName / ...).
// No UI dependencies so it can be unit tested directly.
#pragma once

#include <string>

#include "KeystrokeTypes.h"

namespace InputHighlighter::Formatter
{
    // Human-readable display string for the keystroke, or an empty string when
    // there is nothing meaningful to show (e.g. key-up, whitespace char).
    std::wstring Format(const KeystrokeEvent& e);

    // A keystroke is a "shortcut" if it carries a modifier or is a command key.
    bool IsShortcut(const KeystrokeEvent& e);

    // Command keys are non-character keys we still want to surface (Enter, arrows,
    // function keys, ...). Plain letters/digits/punctuation are handled as chars.
    bool IsCommandKey(uint32_t vk);

    // Friendly name/glyph for a virtual key (e.g. "Ctrl", "Enter", arrow glyphs).
    std::wstring GetKeyName(uint32_t vk);

    // Symbol used for a modifier label ("Shift" -> the shift glyph, etc.).
    std::wstring GetModifierSymbol(const std::wstring& modifier);
}
