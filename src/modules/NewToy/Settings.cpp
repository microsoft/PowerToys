#include "pch.h"
#include "Settings.h"

std::vector<std::wstring> split(const std::wstring input, const std::wstring& regex)
{
    // passing -1 as the submatch index parameter performs splitting
    std::wregex re(regex);
    std::wsregex_token_iterator
        first{ input.begin(), input.end(), re, -1 },
        last;
    return { first, last };
}

PowerToysSettings::HotkeyObject ModuleSettings::hotkeyFromString()
{
    // Assumptions: >=2 keys. Separate by +. Not containing +. Last key is the main key i.e. Alt+A is allowed but not A+Alt.Space to be entered as Space. Case insensitive
    bool win_pressed = false;
    bool ctrl_pressed = false;
    bool alt_pressed = false;
    bool shift_pressed = false;

    std::vector<std::wstring> keyList;
    keyList = split(newToyLLHotkey, L"\\+");
    return PowerToysSettings::HotkeyObject::from_settings(win_pressed, ctrl_pressed, alt_pressed, shift_pressed, VK_OEM_2);
}