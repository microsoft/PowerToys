// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "KeystrokeFormatter.h"

#include <algorithm>
#include <cwctype>
#include <vector>

#include <Windows.h>

namespace InputHighlighter::Formatter
{
    namespace
    {
        // Modifier glyphs (Segoe UI Symbol compatible).
        constexpr wchar_t kShiftGlyph[] = L"\u21E7"; // upwards white arrow
        constexpr wchar_t kWinGlyph[] = L"\u229E"; // squared plus

        std::wstring Utf32ToWide(char32_t ch)
        {
            if (ch == 0)
            {
                return std::wstring();
            }

            if (ch <= 0xFFFF)
            {
                return std::wstring(1, static_cast<wchar_t>(ch));
            }

            // Encode as a UTF-16 surrogate pair.
            const char32_t v = ch - 0x10000;
            std::wstring result;
            result.push_back(static_cast<wchar_t>(0xD800 + (v >> 10)));
            result.push_back(static_cast<wchar_t>(0xDC00 + (v & 0x3FF)));
            return result;
        }

        bool IsAllWhitespace(const std::wstring& s)
        {
            if (s.empty())
            {
                return true;
            }

            for (const wchar_t c : s)
            {
                if (!std::iswspace(c))
                {
                    return false;
                }
            }

            return true;
        }

        // Present modifiers in a stable order matching the capture snapshot.
        std::vector<std::wstring> GetModifierList(const std::array<bool, 4>& mods)
        {
            std::vector<std::wstring> list;
            if (mods[Mod_Ctrl])
            {
                list.push_back(L"Ctrl");
            }

            if (mods[Mod_Alt])
            {
                list.push_back(L"Alt");
            }

            if (mods[Mod_Shift])
            {
                list.push_back(L"Shift");
            }

            if (mods[Mod_Win])
            {
                list.push_back(L"Win");
            }

            return list;
        }

        bool Contains(const std::vector<std::wstring>& v, const std::wstring& s)
        {
            return std::find(v.begin(), v.end(), s) != v.end();
        }
    }

    bool IsCommandKey(uint32_t vk)
    {
        switch (vk)
        {
        case VK_SPACE:
        case VK_RETURN:
        case VK_TAB:
        case VK_BACK:
        case VK_ESCAPE:
        case VK_DELETE:
        case VK_INSERT:
        case VK_HOME:
        case VK_END:
        case VK_PRIOR: // Page Up
        case VK_NEXT: // Page Down
        case VK_LEFT:
        case VK_RIGHT:
        case VK_UP:
        case VK_DOWN:
        case VK_SNAPSHOT: // Print Screen
        case VK_PAUSE:
        case VK_CAPITAL: // Caps Lock
        case VK_LWIN:
        case VK_RWIN:
            return true;
        default:
            break;
        }

        // Function keys F1..F24.
        if (vk >= VK_F1 && vk <= VK_F24)
        {
            return true;
        }

        return false;
    }

    std::wstring GetModifierSymbol(const std::wstring& modifier)
    {
        if (modifier == L"Ctrl")
        {
            return L"Ctrl";
        }

        if (modifier == L"Alt")
        {
            return L"Alt";
        }

        if (modifier == L"Shift")
        {
            return kShiftGlyph;
        }

        if (modifier == L"Win")
        {
            return kWinGlyph;
        }

        return modifier;
    }

    std::wstring GetKeyName(uint32_t vk)
    {
        switch (vk)
        {
        case VK_LSHIFT:
        case VK_RSHIFT:
        case VK_SHIFT:
            return kShiftGlyph;
        case VK_CONTROL:
        case VK_LCONTROL:
        case VK_RCONTROL:
            return L"Ctrl";
        case VK_MENU:
        case VK_LMENU:
        case VK_RMENU:
            return L"Alt";
        case VK_LWIN:
        case VK_RWIN:
            return kWinGlyph;
        case VK_SPACE:
            return L"Space";
        case VK_RETURN:
            return L"Enter";
        case VK_BACK:
            return L"Backspace";
        case VK_TAB:
            return L"Tab";
        case VK_ESCAPE:
            return L"Esc";
        case VK_DELETE:
            return L"Del";
        case VK_INSERT:
            return L"Ins";
        case VK_LEFT:
            return L"\u2190"; // left arrow
        case VK_RIGHT:
            return L"\u2192"; // right arrow
        case VK_UP:
            return L"\u2191"; // up arrow
        case VK_DOWN:
            return L"\u2193"; // down arrow
        default:
            break;
        }

        // Letters A-Z.
        if (vk >= 'A' && vk <= 'Z')
        {
            return std::wstring(1, static_cast<wchar_t>(vk));
        }

        // Top-row digits 0-9.
        if (vk >= '0' && vk <= '9')
        {
            return std::wstring(1, static_cast<wchar_t>(vk));
        }

        // Numpad 0-9.
        if (vk >= VK_NUMPAD0 && vk <= VK_NUMPAD9)
        {
            return L"Num " + std::wstring(1, static_cast<wchar_t>(L'0' + (vk - VK_NUMPAD0)));
        }

        // Function keys.
        if (vk >= VK_F1 && vk <= VK_F24)
        {
            return L"F" + std::to_wstring(vk - VK_F1 + 1);
        }

        // Punctuation / OEM keys (US layout labels).
        switch (vk)
        {
        case VK_OEM_1:
            return L";";
        case VK_OEM_PLUS:
            return L"=";
        case VK_OEM_COMMA:
            return L",";
        case VK_OEM_MINUS:
            return L"-";
        case VK_OEM_PERIOD:
            return L".";
        case VK_OEM_2:
            return L"/";
        case VK_OEM_3:
            return L"`";
        case VK_OEM_4:
            return L"[";
        case VK_OEM_5:
            return L"\\";
        case VK_OEM_6:
            return L"]";
        case VK_OEM_7:
            return L"'";
        case VK_VOLUME_MUTE:
            return L"Mute";
        case VK_VOLUME_DOWN:
            return L"Vol -";
        case VK_VOLUME_UP:
            return L"Vol +";
        case VK_MEDIA_NEXT_TRACK:
            return L"Next";
        case VK_MEDIA_PREV_TRACK:
            return L"Prev";
        case VK_MEDIA_PLAY_PAUSE:
            return L"Play/Pause";
        default:
            break;
        }

        return std::wstring();
    }

    bool IsShortcut(const KeystrokeEvent& e)
    {
        // Any modifier held makes this a shortcut.
        for (const bool held : e.mods)
        {
            if (held)
            {
                return true;
            }
        }

        // Command keys (Enter, Esc, F1, ...) count as shortcuts too.
        if (IsCommandKey(e.vk))
        {
            return true;
        }

        return false;
    }

    std::wstring Format(const KeystrokeEvent& e)
    {
        if (e.type == KeystrokeEventType::Up)
        {
            return std::wstring();
        }

        const bool isCharEvent = e.ch != 0;
        const std::wstring text = isCharEvent ? Utf32ToWide(e.ch) : std::wstring();

        const bool hasCtrl = e.mods[Mod_Ctrl];
        const bool hasAlt = e.mods[Mod_Alt];
        const bool hasWin = e.mods[Mod_Win];

        std::wstring keyName;
        bool haveKeyName = false;

        if (isCharEvent && !hasWin)
        {
            if (IsAllWhitespace(text))
            {
                return std::wstring();
            }

            keyName = text;
            haveKeyName = true;
        }
        else
        {
            if (IsCommandKey(e.vk) || hasCtrl || hasAlt || hasWin)
            {
                keyName = GetKeyName(e.vk);
                haveKeyName = !keyName.empty();
            }
        }

        if (!haveKeyName)
        {
            return std::wstring();
        }

        std::vector<std::wstring> displayParts;
        for (const auto& mod : GetModifierList(e.mods))
        {
            // Don't show Shift when a shifted character already implies it (e.g. "!").
            if (isCharEvent && !hasWin && mod == L"Shift" && !hasCtrl && !hasAlt)
            {
                continue;
            }

            const std::wstring symbol = GetModifierSymbol(mod);
            if (!Contains(displayParts, symbol))
            {
                displayParts.push_back(symbol);
            }
        }

        // Avoid duplicating the key with an already-shown modifier (e.g. Ctrl + Ctrl).
        const std::wstring modSym = GetModifierSymbol(keyName);
        if (!Contains(displayParts, keyName) && !Contains(displayParts, modSym))
        {
            displayParts.push_back(keyName);
        }

        std::wstring result;
        for (size_t i = 0; i < displayParts.size(); ++i)
        {
            if (i != 0)
            {
                result += L" + ";
            }

            result += displayParts[i];
        }

        return result;
    }
}
