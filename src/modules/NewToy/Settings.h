#pragma once
#include <common/settings_objects.h>
#include <regex>

struct ModuleSettings
{
    PowerToysSettings::HotkeyObject newToyShowHotkey = PowerToysSettings::HotkeyObject::from_settings(true, false, false, false, VK_OEM_7);
    PowerToysSettings::HotkeyObject newToyEditHotkey = PowerToysSettings::HotkeyObject::from_settings(true, false, false, false, VK_OEM_2);
    bool bool_prop = true;
    int int_prop = 10;
    std::wstring newToyLLHotkey = L"Win + Alt + Z";
    PowerToysSettings::HotkeyObject hotkeyFromString();
    PowerToysSettings::HotkeyObject newToyLLHotkeyObject = hotkeyFromString();
};