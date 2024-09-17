#include "pch.h"
#include "ZoomItSettings.h"
#include "ZoomItSettings.g.cpp"
#include "../ZoomIt/ZoomItSettings.h"
#include <common/SettingsAPI/settings_objects.h>
#include <common/utils/color.h>
#include <map>

namespace winrt::PowerToys::ZoomItSettingsInterop::implementation
{
    CRegistry reg(_T("Software\\Sysinternals\\") APPNAME);

    const unsigned int SPECIAL_SEMANTICS_SHORTCUT = 1;
    const unsigned int SPECIAL_SEMANTICS_COLOR = 2;

    std::map<std::wstring, unsigned int> settings_with_special_semantics = {
        { L"ToggleKey", SPECIAL_SEMANTICS_SHORTCUT },
        { L"LiveZoomToggleKey", SPECIAL_SEMANTICS_SHORTCUT },
        { L"DrawToggleKey", SPECIAL_SEMANTICS_SHORTCUT },
        { L"RecordToggleKey", SPECIAL_SEMANTICS_SHORTCUT },
        { L"SnipToggleKey", SPECIAL_SEMANTICS_SHORTCUT },
        { L"BreakTimerKey", SPECIAL_SEMANTICS_SHORTCUT },
        { L"DemoTypeToggleKey", SPECIAL_SEMANTICS_SHORTCUT },
        { L"PenColor", SPECIAL_SEMANTICS_COLOR },
        { L"BreakPenColor", SPECIAL_SEMANTICS_COLOR },
    };

    hstring ZoomItSettings::LoadSettingsJson()
    {
        PowerToysSettings::PowerToyValues _settings(L"ZoomIt",L"ZoomIt");
        reg.ReadRegSettings(RegSettings);
        PREG_SETTING curSetting = RegSettings;
        while (curSetting->Valuename)
        {
            switch (curSetting->Type)
            {
            case SETTING_TYPE_DWORD:
            {
                auto special_semantics = settings_with_special_semantics.find(curSetting->Valuename);
                DWORD value = *static_cast<PDWORD>(curSetting->Setting);
                if (special_semantics == settings_with_special_semantics.end())
                {
                    _settings.add_property<DWORD>(curSetting->Valuename, value);
                }
                else
                {
                    if (special_semantics->second == SPECIAL_SEMANTICS_SHORTCUT)
                    {
                        auto hotkey = PowerToysSettings::HotkeyObject::from_settings(
                            value & (HOTKEYF_EXT << 8), //WIN
                            value & (HOTKEYF_CONTROL << 8),
                            value & (HOTKEYF_ALT << 8),
                            value & (HOTKEYF_SHIFT << 8),
                            value & 0xFF);
                        _settings.add_property(curSetting->Valuename, hotkey.get_json());
                    }
                    else if (special_semantics->second == SPECIAL_SEMANTICS_COLOR)
                    {
                        // PowerToys settings likes colors as #FFFFFF strings.
                        hstring s = winrt::to_hstring(std::format("#{:02x}{:02x}{:02x}", value & 0xFF, (value >> 8) & 0xFF, (value >> 16) & 0xFF));
                        _settings.add_property(curSetting->Valuename, s);
                    }
                }
                break;
            }
            case SETTING_TYPE_BOOLEAN:
                _settings.add_property<bool>(curSetting->Valuename, *static_cast<PBOOLEAN>(curSetting->Setting));
                break;
            case SETTING_TYPE_DOUBLE:
                assert(false); // ZoomIt doesn't use this type of setting.
                break;
            case SETTING_TYPE_WORD:
                assert(false); // ZoomIt doesn't use this type of setting.
                break;
            case SETTING_TYPE_STRING:
                _settings.add_property<std::wstring>(curSetting->Valuename, static_cast<PTCHAR>(curSetting->Setting));
                break;
            case SETTING_TYPE_DWORD_ARRAY:
                assert(false); // ZoomIt doesn't use this type of setting.
                break;
            case SETTING_TYPE_WORD_ARRAY:
                assert(false); // ZoomIt doesn't use this type of setting.
                break;
            case SETTING_TYPE_BINARY:
                // TODO: How to support this type.
                break;
            }
            curSetting++;
        }

        return _settings.get_raw_json().Stringify();
    }

    void ZoomItSettings::SaveSettingsJson(hstring json)
    {
        reg.ReadRegSettings(RegSettings);

        // Parse the input JSON string.
        PowerToysSettings::PowerToyValues valuesFromSettings =
            PowerToysSettings::PowerToyValues::from_json_string(json, L"ZoomIt");

        PREG_SETTING curSetting = RegSettings;
        while (curSetting->Valuename)
        {
            switch (curSetting->Type)
            {
            case SETTING_TYPE_DWORD:
            {
                auto special_semantics = settings_with_special_semantics.find(curSetting->Valuename);
                if (special_semantics == settings_with_special_semantics.end())
                {
                    auto possibleValue = valuesFromSettings.get_uint_value(curSetting->Valuename);
                    if (possibleValue.has_value())
                    {
                        *static_cast<PDWORD>(curSetting->Setting) = possibleValue.value();
                    }
                }
                else
                {
                    if (special_semantics->second == SPECIAL_SEMANTICS_SHORTCUT)
                    {
                        auto possibleValue = valuesFromSettings.get_json(curSetting->Valuename);
                        if (possibleValue.has_value())
                        {
                            auto hotkey = PowerToysSettings::HotkeyObject::from_json(possibleValue.value());
                            unsigned int value = 0;
                            value |= hotkey.get_code();
                            if (hotkey.ctrl_pressed())
                            {
                                value |= (HOTKEYF_CONTROL << 8);
                            }
                            if (hotkey.alt_pressed())
                            {
                                value |= (HOTKEYF_ALT << 8);
                            }
                            if (hotkey.shift_pressed())
                            {
                                value |= (HOTKEYF_SHIFT << 8);
                            }
                            if (hotkey.win_pressed())
                            {
                                value |= (HOTKEYF_EXT << 8);
                            }
                            *static_cast<PDWORD>(curSetting->Setting) = value;
                        }
                    }
                    else if (special_semantics->second == SPECIAL_SEMANTICS_COLOR)
                    {
                        auto possibleValue = valuesFromSettings.get_string_value(curSetting->Valuename);
                        if (possibleValue.has_value())
                        {
                            uint8_t r, g, b;
                            if (checkValidRGB(possibleValue.value(), &r, &g, &b))
                            {
                                *static_cast<PDWORD>(curSetting->Setting) = RGB(r, g, b);
                            }

                        }
                    }
                }
                break;
            }
            case SETTING_TYPE_BOOLEAN:
            {
                auto possibleValue = valuesFromSettings.get_bool_value(curSetting->Valuename);
                if (possibleValue.has_value())
                {
                    *static_cast<PBOOLEAN>(curSetting->Setting) = static_cast<BOOLEAN>(possibleValue.value());
                }
                break;
            }
            case SETTING_TYPE_DOUBLE:
                assert(false); // ZoomIt doesn't use this type of setting.
                break;
            case SETTING_TYPE_WORD:
                assert(false); // ZoomIt doesn't use this type of setting.
                break;
            case SETTING_TYPE_STRING:
            {
                auto possibleValue = valuesFromSettings.get_string_value(curSetting->Valuename);
                if (possibleValue.has_value())
                {
                    const TCHAR* value = possibleValue.value().c_str();
                    _tcscpy_s(static_cast<PTCHAR>(curSetting->Setting), curSetting->Size / sizeof(TCHAR), value);
                }
                break;
            }
            case SETTING_TYPE_DWORD_ARRAY:
                assert(false); // ZoomIt doesn't use this type of setting.
                break;
            case SETTING_TYPE_WORD_ARRAY:
                assert(false); // ZoomIt doesn't use this type of setting.
                break;
            case SETTING_TYPE_BINARY:
                // TODO: How to support this type.
                break;
            }
            curSetting++;
        }
        reg.WriteRegSettings(RegSettings);
    }
}
