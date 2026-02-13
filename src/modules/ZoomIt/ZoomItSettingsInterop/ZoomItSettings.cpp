#include "pch.h"
#include "ZoomItSettings.h"
#include "ZoomItSettings.g.cpp"
#include "../ZoomIt/ZoomItSettings.h"
#include <common/SettingsAPI/settings_objects.h>
#include <common/utils/color.h>
#include <map>
#pragma comment(lib, "Crypt32.lib") // For the CryptStringToBinaryW and CryptBinaryToStringW functions

namespace winrt::PowerToys::ZoomItSettingsInterop::implementation
{
    ClassRegistry reg(_T("Software\\Sysinternals\\") APPNAME);

    const unsigned int SPECIAL_SEMANTICS_SHORTCUT = 1;
    const unsigned int SPECIAL_SEMANTICS_COLOR = 2;
    const unsigned int SPECIAL_SEMANTICS_LOG_FONT = 3;
    const unsigned int SPECIAL_SEMANTICS_RECORDING_FORMAT = 4;
    const unsigned int SPECIAL_SEMANTICS_RECORD_SCALING_GIF = 5;
    const unsigned int SPECIAL_SEMANTICS_RECORD_SCALING_MP4 = 6;

    std::vector<unsigned char> base64_decode(const std::wstring& base64_string)
    {
        DWORD binary_len = 0;
        // Get the required buffer size for the binary data
        if (!CryptStringToBinaryW(base64_string.c_str(), 0, CRYPT_STRING_BASE64, nullptr, &binary_len, nullptr, nullptr))
        {
            throw std::runtime_error("Error in CryptStringToBinaryW (getting size)");
        }

        std::vector<unsigned char> binary_data(binary_len);

        // Decode the Base64 string into binary data
        if (!CryptStringToBinaryW(base64_string.c_str(), 0, CRYPT_STRING_BASE64, binary_data.data(), &binary_len, nullptr, nullptr))
        {
            throw std::runtime_error("Error in CryptStringToBinaryW (decoding)");
        }

        return binary_data;
    }

    std::wstring base64_encode(const unsigned char* data, size_t length)
    {
        DWORD base64_len = 0;
        // Get the required buffer size for Base64 string
        if (!CryptBinaryToStringW(data, static_cast<DWORD>(length), CRYPT_STRING_BASE64 | CRYPT_STRING_NOCRLF, nullptr, &base64_len))
        {
            throw std::runtime_error("Error in CryptBinaryToStringW (getting size)");
        }

        std::wstring base64_string(base64_len, '\0');

        // Encode the binary data to Base64
        if (!CryptBinaryToStringW(data, static_cast<DWORD>(length), CRYPT_STRING_BASE64 | CRYPT_STRING_NOCRLF, &base64_string[0], &base64_len))
        {
            throw std::runtime_error("Error in CryptBinaryToStringW (encoding)");
        }

        // Resize the wstring to remove any trailing null character.
        if (!base64_string.empty() && base64_string.back() == L'\0')
        {
            base64_string.pop_back();
        }

        return base64_string;
    }

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
        { L"Font", SPECIAL_SEMANTICS_LOG_FONT },
        { L"RecordingFormat", SPECIAL_SEMANTICS_RECORDING_FORMAT },
        { L"RecordScalingGIF", SPECIAL_SEMANTICS_RECORD_SCALING_GIF },
        { L"RecordScalingMP4", SPECIAL_SEMANTICS_RECORD_SCALING_MP4 },
    };

    hstring ZoomItSettings::LoadSettingsJson()
    {
        PowerToysSettings::PowerToyValues _settings(L"ZoomIt",L"ZoomIt");
        reg.ReadRegSettings(RegSettings);
        PREG_SETTING curSetting = RegSettings;
        while (curSetting->ValueName)
        {
            switch (curSetting->Type)
            {
            case SETTING_TYPE_DWORD:
            {
                auto special_semantics = settings_with_special_semantics.find(curSetting->ValueName);
                DWORD value = *static_cast<PDWORD>(curSetting->Setting);
                if (special_semantics == settings_with_special_semantics.end())
                {
                    _settings.add_property<DWORD>(curSetting->ValueName, value);
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
                        _settings.add_property(curSetting->ValueName, hotkey.get_json());
                    }
                    else if (special_semantics->second == SPECIAL_SEMANTICS_RECORDING_FORMAT)
                    {
                        std::wstring formatString = (value == 0) ? L"GIF" : L"MP4";
                        _settings.add_property(L"RecordFormat", formatString);
                    }
                    else if (special_semantics->second == SPECIAL_SEMANTICS_COLOR)
                    {
                        /* PowerToys settings likes colors as #FFFFFF strings.
                        But currently these Settings are internal state for ZoomIt, not something that we really need to send Settings.
                        Code is kept here as a reference if a future color Setting ends up being configured.
                        hstring s = winrt::to_hstring(std::format("#{:02x}{:02x}{:02x}", value & 0xFF, (value >> 8) & 0xFF, (value >> 16) & 0xFF));
                        _settings.add_property(curSetting->ValueName, s);
                        */
                    }
                }
                break;
            }
            case SETTING_TYPE_BOOLEAN:
                _settings.add_property<bool>(curSetting->ValueName, *static_cast<PBOOLEAN>(curSetting->Setting));
                break;
            case SETTING_TYPE_DOUBLE:
                assert(false); // ZoomIt doesn't use this type of setting.
                break;
            case SETTING_TYPE_WORD:
                assert(false); // ZoomIt doesn't use this type of setting.
                break;
            case SETTING_TYPE_STRING:
                _settings.add_property<std::wstring>(curSetting->ValueName, static_cast<PTCHAR>(curSetting->Setting));
                break;
            case SETTING_TYPE_DWORD_ARRAY:
                assert(false); // ZoomIt doesn't use this type of setting.
                break;
            case SETTING_TYPE_WORD_ARRAY:
                assert(false); // ZoomIt doesn't use this type of setting.
                break;
            case SETTING_TYPE_BINARY:
                auto special_semantics = settings_with_special_semantics.find(curSetting->ValueName);
                if (special_semantics != settings_with_special_semantics.end() && special_semantics->second == SPECIAL_SEMANTICS_LOG_FONT)
                {
                    // This is the font setting. It's a special case where the default value needs to be calculated if it's still 0.
                    if (g_LogFont.lfFaceName[0] == L'\0')
                    {
                        GetObject(GetStockObject(DEFAULT_GUI_FONT), sizeof g_LogFont, &g_LogFont);
                        g_LogFont.lfWeight = FW_NORMAL;
                        auto hDc = CreateCompatibleDC(NULL);
                        g_LogFont.lfHeight = -MulDiv(8, GetDeviceCaps(hDc, LOGPIXELSY), 72);
                        DeleteDC(hDc);
                    }
                }

                // Base64 encoding is likely the best way to serialize a byte array into JSON.
                auto encodedFont = base64_encode(static_cast<PBYTE>(curSetting->Setting), curSetting->Size);
                _settings.add_property<std::wstring>(curSetting->ValueName, encodedFont);
                break;
            }
            curSetting++;
        }

        DWORD recordScaling = (g_RecordingFormat == static_cast<RecordingFormat>(0)) ? g_RecordScalingGIF : g_RecordScalingMP4;
        _settings.add_property<DWORD>(L"RecordScaling", recordScaling);

        return _settings.get_raw_json().Stringify();
    }

    void ZoomItSettings::SaveSettingsJson(hstring json)
    {
        reg.ReadRegSettings(RegSettings);

        // Parse the input JSON string.
        PowerToysSettings::PowerToyValues valuesFromSettings =
            PowerToysSettings::PowerToyValues::from_json_string(json, L"ZoomIt");

        bool formatChanged = false;

        PREG_SETTING curSetting = RegSettings;
        while (curSetting->ValueName)
        {
            switch (curSetting->Type)
            {
            case SETTING_TYPE_DWORD:
            {
                auto special_semantics = settings_with_special_semantics.find(curSetting->ValueName);
                if (special_semantics == settings_with_special_semantics.end())
                {
                    auto possibleValue = valuesFromSettings.get_uint_value(curSetting->ValueName);
                    if (possibleValue.has_value())
                    {
                        *static_cast<PDWORD>(curSetting->Setting) = possibleValue.value();
                    }
                }
                else
                {
                    if (special_semantics->second == SPECIAL_SEMANTICS_SHORTCUT)
                    {
                        auto possibleValue = valuesFromSettings.get_json(curSetting->ValueName);
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
                    else if (special_semantics->second == SPECIAL_SEMANTICS_RECORDING_FORMAT)
                    {
                        // Convert string ("GIF" or "MP4") to DWORD enum value (0=GIF, 1=MP4)
                        auto possibleValue = valuesFromSettings.get_string_value(L"RecordFormat");
                        if (possibleValue.has_value())
                        {
                            RecordingFormat oldFormat = g_RecordingFormat;
                            DWORD formatValue = (possibleValue.value() == L"GIF") ? 0 : 1;
                            RecordingFormat newFormat = static_cast<RecordingFormat>(formatValue);

                            *static_cast<PDWORD>(curSetting->Setting) = formatValue;

                            if (oldFormat != newFormat)
                            {
                                formatChanged = true;

                                if (oldFormat == static_cast<RecordingFormat>(0))
                                {
                                    g_RecordScalingGIF = g_RecordScaling;
                                }
                                else
                                {
                                    g_RecordScalingMP4 = g_RecordScaling;
                                }

                                if (newFormat == static_cast<RecordingFormat>(0))
                                {
                                    g_RecordScaling = g_RecordScalingGIF;
                                }
                                else
                                {
                                    g_RecordScaling = g_RecordScalingMP4;
                                }
                            }
                        }
                    }
                    else if (special_semantics->second == SPECIAL_SEMANTICS_COLOR)
                    {
                        /* PowerToys settings likes colors as #FFFFFF strings.
                        But currently these Settings are internal state for ZoomIt, not something that we really need to save from Settings.
                        Code is kept here as a reference if a future color Setting ends up being configured.
                        auto possibleValue = valuesFromSettings.get_string_value(curSetting->ValueName);
                        if (possibleValue.has_value())
                        {
                            uint8_t r, g, b;
                            if (checkValidRGB(possibleValue.value(), &r, &g, &b))
                            {
                                *static_cast<PDWORD>(curSetting->Setting) = RGB(r, g, b);
                            }
                        }
                        */
                    }
                }
                break;
            }
            case SETTING_TYPE_BOOLEAN:
            {
                auto possibleValue = valuesFromSettings.get_bool_value(curSetting->ValueName);
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
                auto possibleValue = valuesFromSettings.get_string_value(curSetting->ValueName);
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
                auto possibleValue = valuesFromSettings.get_string_value(curSetting->ValueName);
                if (possibleValue.has_value())
                {
                    // Base64 encoding is likely the best way to serialize a byte array into JSON.
                    auto decodedValue = base64_decode(possibleValue.value());
                    assert(curSetting->Size == decodedValue.size()); // Should right now only be used for LOGFONT, so let's hard check it to avoid any insecure overflows.
                    memcpy(static_cast<PBYTE>(curSetting->Setting), decodedValue.data(), decodedValue.size());
                }
                break;
            }
            curSetting++;
        }

        auto recordScalingValue = valuesFromSettings.get_uint_value(L"RecordScaling");
        if (recordScalingValue.has_value() && !formatChanged)
        {
            g_RecordScaling = recordScalingValue.value();

            if (g_RecordingFormat == static_cast<RecordingFormat>(0))
            {
                g_RecordScalingGIF = recordScalingValue.value();
            }
            else
            {
                g_RecordScalingMP4 = recordScalingValue.value();
            }
        }

        reg.WriteRegSettings(RegSettings);
    }
}
