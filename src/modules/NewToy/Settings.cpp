#include "pch.h"
#include "Settings.h"
#include <algorithm>

std::vector<std::wstring> split(const std::wstring input, const std::wstring& regex)
{
    // passing -1 as the submatch index parameter performs splitting
    std::wregex re(regex);
    std::wsregex_token_iterator
        first{ input.begin(), input.end(), re, -1 },
        last;
    return { first, last };
}

PowerToysSettings::HotkeyObject ModuleSettings::hotkeyFromString(std::wstring hotkeyStr)
{
    // Assumptions: >=2 keys. Separate by +. Not containing +. Last key is the main key i.e. Alt+A is allowed but not A+Alt.Space to be entered as Space. Case insensitive. Assume only 1 non-modifier key
    bool win_pressed = false;
    bool ctrl_pressed = false;
    bool alt_pressed = false;
    bool shift_pressed = false;
    UINT key_code;
    std::vector<std::wstring> keyList;
    auto layout = GetKeyboardLayout(0);
    // Split based on plus sign
    keyList = split(hotkeyStr, L"\\+");
    // Remove spaces
    for_each(begin(keyList), end(keyList), [](std::wstring& str) {
        str.erase(std::remove_if(str.begin(), str.end(), [](auto x) { return std::isspace(x); }), str.end());
    });
    for_each(begin(keyList), end(keyList), [&](std::wstring& str) {
        std::transform(str.begin(), str.end(), str.begin(), ::toupper);
        if (str == L"WIN")
            win_pressed = true;
        else if (str == L"CTRL")
            ctrl_pressed = true;
        else if (str == L"ALT")
            alt_pressed = true;
        else if (str == L"SHIFT")
            shift_pressed = true;
        else if (str == L"") // +
            key_code = VK_OEM_PLUS;
        else if (str == L"Space")
            key_code = VK_SPACE;
        else if (str == L"Tab")
            key_code = VK_TAB;
        // TODO: Arrow keys, Fn keys, Ins, Home, PgU/D, Del, End, Numpad
        else
        {
            // Removes flag bits
            key_code = VkKeyScanEx(str[0], layout) & 0xFF;
        }
    });
    // Use VkScan
    return PowerToysSettings::HotkeyObject::from_settings(win_pressed, ctrl_pressed, alt_pressed, shift_pressed, key_code);
}