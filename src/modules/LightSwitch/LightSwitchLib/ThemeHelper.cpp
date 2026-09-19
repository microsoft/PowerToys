#include "pch.h"
#include "ThemeHelper.h"
#include <logger/logger.h>

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
        auto result = RegSetValueExW(key, name, 0, REG_DWORD, reinterpret_cast<const BYTE*>(&value), sizeof(value));
        if (result != ERROR_SUCCESS)
        {
            return result;
        }
        bool actual = false;
        result = ReadThemeValue(key, name, actual);
        if (result != ERROR_SUCCESS)
        {
            return result;
        }
        return actual == isLight ? ERROR_SUCCESS : ERROR_WRITE_FAULT;
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

        result = LightSwitchThemeHelpers::WriteThemeValue(key, name, isLight);
        if (result == ERROR_SUCCESS && system && isLight)
        {
            const DWORD value = 0;
            const auto colorResult = RegSetValueExW(key, L"ColorPrevalence", 0, REG_DWORD, reinterpret_cast<const BYTE*>(&value), sizeof(value));
            if (colorResult != ERROR_SUCCESS)
            {
                Logger::warn(L"[LightSwitchLib] Could not reset ColorPrevalence (error: {}).", colorResult);
            }
        }
        RegCloseKey(key);

        if (result == ERROR_SUCCESS)
        {
            BroadcastThemeChange();
            if (system && isLight)
            {
                SendMessageTimeoutW(HWND_BROADCAST, WM_DWMCOLORIZATIONCOLORCHANGED, 0, 0, SMTO_ABORTIFHUNG, 5000, nullptr);
            }
            // The broadcasts may take time. Report the final registry state as well.
            bool actual = false;
            result = ReadPersonalizationTheme(name, actual);
            if (result == ERROR_SUCCESS && actual != isLight)
            {
                result = ERROR_WRITE_FAULT;
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
bool IsNightLightEnabled()
{
    HKEY hKey;
    const wchar_t* path = NIGHT_LIGHT_REGISTRY_PATH;

    if (RegOpenKeyExW(HKEY_CURRENT_USER, path, 0, KEY_READ, &hKey) != ERROR_SUCCESS)
        return false;

    // RegGetValueW will set size to the size of the data and we expect that to be at least 25 bytes (we need to access bytes 23 and 24)
    DWORD size = 0;
    if (RegGetValueW(hKey, nullptr, L"Data", RRF_RT_REG_BINARY, nullptr, nullptr, &size) != ERROR_SUCCESS || size < 25)
    {
        RegCloseKey(hKey);
        return false;
    }

    std::vector<BYTE> data(size);
    if (RegGetValueW(hKey, nullptr, L"Data", RRF_RT_REG_BINARY, nullptr, data.data(), &size) != ERROR_SUCCESS)
    {
        RegCloseKey(hKey);
        return false;
    }

    RegCloseKey(hKey);
    return data[23] == 0x10 && data[24] == 0x00;
}
