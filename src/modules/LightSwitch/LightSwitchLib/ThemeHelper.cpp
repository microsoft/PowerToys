#include "pch.h"
#include "ThemeHelper.h"
#include <logger/logger.h>
#include <memory>
#include <new>

namespace LightSwitchThemeHelpers
{
    LSTATUS ReadThemeValue(HKEY key, const wchar_t* name, bool& isLight)
    {
        DWORD value = 0;
        DWORD size = sizeof(value);
        const auto result = RegGetValueW(key, nullptr, name, RRF_RT_REG_DWORD, nullptr, &value, &size);
        if (result != ERROR_SUCCESS)
        {
            return result;
        }
        if (size != sizeof(value) || value > 1)
        {
            return ERROR_INVALID_DATA;
        }
        isLight = value == 1;
        return ERROR_SUCCESS;
    }

    LSTATUS WriteThemeValue(HKEY key, const wchar_t* name, bool isLight)
    {
        const DWORD value = isLight ? 1 : 0;
        return RegSetValueExW(key, name, 0, REG_DWORD, reinterpret_cast<const BYTE*>(&value), sizeof(value));
    }

    LSTATUS UpdateColorPrevalence(HKEY personalizationKey, HKEY stateKey, std::optional<bool> wasLight, bool isLight, bool& changed)
    {
        constexpr wchar_t colorPrevalence[] = L"ColorPrevalence";
        changed = false;
        if (!stateKey)
        {
            wasLight.reset();
        }

        if (isLight)
        {
            // Only a dark-to-light transition captures the user's choice. While already light,
            // the value is 0 because of the light theme and must not replace the saved one.
            LSTATUS saveResult = ERROR_SUCCESS;
            if (wasLight == false)
            {
                DWORD value = 0;
                DWORD size = sizeof(value);
                const auto readResult = RegGetValueW(personalizationKey, nullptr, colorPrevalence, RRF_RT_REG_DWORD, nullptr, &value, &size);
                if (readResult == ERROR_SUCCESS)
                {
                    saveResult = RegSetValueExW(stateKey, SAVED_COLOR_PREVALENCE_VALUE, 0, REG_DWORD, reinterpret_cast<const BYTE*>(&value), sizeof(value));
                }
                else
                {
                    saveResult = RegDeleteValueW(stateKey, SAVED_COLOR_PREVALENCE_VALUE);
                    if (saveResult == ERROR_FILE_NOT_FOUND)
                    {
                        saveResult = ERROR_SUCCESS;
                    }
                }
            }

            const DWORD off = 0;
            const auto clearResult = RegSetValueExW(personalizationKey, colorPrevalence, 0, REG_DWORD, reinterpret_cast<const BYTE*>(&off), sizeof(off));
            changed = clearResult == ERROR_SUCCESS;
            return saveResult != ERROR_SUCCESS ? saveResult : clearResult;
        }

        if (wasLight != true)
        {
            return ERROR_SUCCESS;
        }

        DWORD saved = 0;
        DWORD size = sizeof(saved);
        auto result = RegGetValueW(stateKey, nullptr, SAVED_COLOR_PREVALENCE_VALUE, RRF_RT_REG_DWORD, nullptr, &saved, &size);
        if (result != ERROR_SUCCESS)
        {
            return result == ERROR_FILE_NOT_FOUND ? ERROR_SUCCESS : result;
        }

        result = RegSetValueExW(personalizationKey, colorPrevalence, 0, REG_DWORD, reinterpret_cast<const BYTE*>(&saved), sizeof(saved));
        if (result != ERROR_SUCCESS)
        {
            return result;
        }
        changed = true;
        return RegDeleteValueW(stateKey, SAVED_COLOR_PREVALENCE_VALUE);
    }

    LSTATUS ReadNightLightState(HKEY key, bool& enabled)
    {
        constexpr DWORD minimumSize = 25;
        constexpr DWORD maximumSize = 64 * 1024;
        DWORD size = 0;
        auto result = RegGetValueW(key, nullptr, L"Data", RRF_RT_REG_BINARY, nullptr, nullptr, &size);
        if (result != ERROR_SUCCESS)
        {
            return result;
        }
        if (size < minimumSize)
        {
            return ERROR_INVALID_DATA;
        }
        if (size > maximumSize)
        {
            return ERROR_MORE_DATA;
        }

        // Bound the allocation and recheck the actual size: CloudStore can replace
        // this value between the size query and the data read.
        const std::unique_ptr<BYTE[]> data(new (std::nothrow) BYTE[size]);
        if (!data)
        {
            return ERROR_NOT_ENOUGH_MEMORY;
        }
        result = RegGetValueW(key, nullptr, L"Data", RRF_RT_REG_BINARY, nullptr, data.get(), &size);
        if (result != ERROR_SUCCESS)
        {
            return result;
        }
        if (size < minimumSize)
        {
            return ERROR_INVALID_DATA;
        }

        enabled = data[23] == 0x10 && data[24] == 0x00;
        return ERROR_SUCCESS;
    }
}

namespace
{
    LSTATUS ReadPersonalizationTheme(const wchar_t* name, bool& isLight)
    {
        HKEY key = nullptr;
        auto result = RegOpenKeyExW(HKEY_CURRENT_USER, PERSONALIZATION_REGISTRY_PATH, 0, KEY_QUERY_VALUE, &key);
        if (result == ERROR_SUCCESS)
        {
            result = LightSwitchThemeHelpers::ReadThemeValue(key, name, isLight);
            RegCloseKey(key);
        }
        return result;
    }

    void BroadcastThemeChange()
    {
        SendMessageTimeoutW(HWND_BROADCAST, WM_SETTINGCHANGE, 0, reinterpret_cast<LPARAM>(L"ImmersiveColorSet"), SMTO_ABORTIFHUNG, 5000, nullptr);
        SendMessageTimeoutW(HWND_BROADCAST, WM_THEMECHANGED, 0, 0, SMTO_ABORTIFHUNG, 5000, nullptr);
    }

    LSTATUS WritePersonalizationTheme(const wchar_t* name, bool isLight, bool system)
    {
        HKEY key = nullptr;
        auto result = RegOpenKeyExW(HKEY_CURRENT_USER, PERSONALIZATION_REGISTRY_PATH, 0, KEY_QUERY_VALUE | KEY_SET_VALUE, &key);
        if (result != ERROR_SUCCESS)
        {
            return result;
        }

        std::optional<bool> wasLight;
        HKEY stateKey = nullptr;
        if (system)
        {
            bool current = false;
            if (LightSwitchThemeHelpers::ReadThemeValue(key, name, current) == ERROR_SUCCESS)
            {
                wasLight = current;
            }
            const auto stateResult = RegCreateKeyExW(HKEY_CURRENT_USER, LIGHT_SWITCH_STATE_REGISTRY_PATH, 0, nullptr, 0, KEY_QUERY_VALUE | KEY_SET_VALUE, nullptr, &stateKey, nullptr);
            if (stateResult != ERROR_SUCCESS)
            {
                stateKey = nullptr;
                Logger::warn(L"[LightSwitchLib] Could not open Light Switch state key (error: {}).", stateResult);
            }
        }

        result = LightSwitchThemeHelpers::WriteThemeValue(key, name, isLight);
        bool colorChanged = false;
        if (result == ERROR_SUCCESS && system)
        {
            const auto colorResult = LightSwitchThemeHelpers::UpdateColorPrevalence(key, stateKey, wasLight, isLight, colorChanged);
            if (colorResult != ERROR_SUCCESS)
            {
                Logger::warn(L"[LightSwitchLib] Could not update ColorPrevalence (error: {}).", colorResult);
            }
        }
        if (stateKey)
        {
            RegCloseKey(stateKey);
        }
        RegCloseKey(key);

        if (result == ERROR_SUCCESS)
        {
            BroadcastThemeChange();
            if (colorChanged)
            {
                SendMessageTimeoutW(HWND_BROADCAST, WM_DWMCOLORIZATIONCOLORCHANGED, 0, 0, SMTO_ABORTIFHUNG, 5000, nullptr);
            }
        }
        return result;
    }
}

LSTATUS TryGetSystemTheme(bool& isLight)
{
    return ReadPersonalizationTheme(L"SystemUsesLightTheme", isLight);
}

LSTATUS TryGetAppsTheme(bool& isLight)
{
    return ReadPersonalizationTheme(L"AppsUseLightTheme", isLight);
}

LSTATUS TrySetSystemTheme(bool isLight)
{
    return WritePersonalizationTheme(L"SystemUsesLightTheme", isLight, true);
}

LSTATUS TrySetAppsTheme(bool isLight)
{
    return WritePersonalizationTheme(L"AppsUseLightTheme", isLight, false);
}

void SetSystemTheme(bool isLight)
{
    const auto result = TrySetSystemTheme(isLight);
    if (result != ERROR_SUCCESS)
    {
        Logger::warn(L"[LightSwitchLib] Could not set system theme (error: {}).", result);
    }
}

void SetAppsTheme(bool isLight)
{
    const auto result = TrySetAppsTheme(isLight);
    if (result != ERROR_SUCCESS)
    {
        Logger::warn(L"[LightSwitchLib] Could not set apps theme (error: {}).", result);
    }
}

bool GetCurrentSystemTheme()
{
    bool isLight = true;
    TryGetSystemTheme(isLight);
    return isLight;
}

bool GetCurrentAppsTheme()
{
    bool isLight = true;
    TryGetAppsTheme(isLight);
    return isLight;
}

LSTATUS TryGetNightLightState(bool& enabled)
{
    HKEY key = nullptr;
    auto result = RegOpenKeyExW(HKEY_CURRENT_USER, NIGHT_LIGHT_REGISTRY_PATH, 0, KEY_QUERY_VALUE, &key);
    if (result == ERROR_SUCCESS)
    {
        result = LightSwitchThemeHelpers::ReadNightLightState(key, enabled);
        RegCloseKey(key);
    }
    return result;
}
