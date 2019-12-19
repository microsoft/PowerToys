#pragma once
#include <common/settings_objects.h>
#include <regex>

struct ModuleSettings
{
    PowerToysSettings::HotkeyObject newToyShowHotkey = PowerToysSettings::HotkeyObject::from_settings(true, false, false, false, VK_OEM_7);
    PowerToysSettings::HotkeyObject newToyEditHotkey = PowerToysSettings::HotkeyObject::from_settings(true, false, false, false, VK_OEM_2);
    bool swapWRS = false;
    int int_prop = 10;
    std::wstring newToyLLHotkey = L"Win + Alt + Z";
    PowerToysSettings::HotkeyObject hotkeyFromString(std::wstring);
    PowerToysSettings::HotkeyObject newToyLLHotkeyObject = hotkeyFromString(newToyLLHotkey);
    bool swapMacro = false;
    std::wstring macro_first = L"Win + A";
    std::wstring macro_second = L"Win + F";
    PowerToysSettings::HotkeyObject macro_first_object = hotkeyFromString(macro_first);
    PowerToysSettings::HotkeyObject macro_second_object = hotkeyFromString(macro_second);
};