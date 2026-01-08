#include <windows.h>
#include <logger/logger_settings.h>
#include <logger/logger.h>
#include <utils/logger_helper.h>
#include "ThemeHelper.h"
#include <SettingsConstants.h>
#include <windows.h>
#include <shellapi.h>

// Controls changing the themes.

static void ResetColorPrevalence()
{
    HKEY hKey;
    if (RegOpenKeyEx(HKEY_CURRENT_USER,
                     PERSONALIZATION_REGISTRY_PATH,
                     0,
                     KEY_SET_VALUE,
                     &hKey) == ERROR_SUCCESS)
    {
        DWORD value = 0; // back to default value
        RegSetValueEx(hKey, L"ColorPrevalence", 0, REG_DWORD, reinterpret_cast<const BYTE*>(&value), sizeof(value));
        RegCloseKey(hKey);

        SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, 0, reinterpret_cast<LPARAM>(L"ImmersiveColorSet"), SMTO_ABORTIFHUNG, 5000, nullptr);

        SendMessageTimeout(HWND_BROADCAST, WM_THEMECHANGED, 0, 0, SMTO_ABORTIFHUNG, 5000, nullptr);

        SendMessageTimeout(HWND_BROADCAST, WM_DWMCOLORIZATIONCOLORCHANGED, 0, 0, SMTO_ABORTIFHUNG, 5000, nullptr);
    }
}

void SetAppsTheme(bool mode)
{
    HKEY hKey;
    if (RegOpenKeyEx(HKEY_CURRENT_USER,
                     PERSONALIZATION_REGISTRY_PATH,
                     0,
                     KEY_SET_VALUE,
                     &hKey) == ERROR_SUCCESS)
    {
        DWORD value = mode;
        RegSetValueEx(hKey, L"AppsUseLightTheme", 0, REG_DWORD, reinterpret_cast<const BYTE*>(&value), sizeof(value));
        RegCloseKey(hKey);

        SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, 0, reinterpret_cast<LPARAM>(L"ImmersiveColorSet"), SMTO_ABORTIFHUNG, 5000, nullptr);

        SendMessageTimeout(HWND_BROADCAST, WM_THEMECHANGED, 0, 0, SMTO_ABORTIFHUNG, 5000, nullptr);
    }
}

void SetSystemTheme(bool mode)
{
    HKEY hKey;
    if (RegOpenKeyEx(HKEY_CURRENT_USER,
                     PERSONALIZATION_REGISTRY_PATH,
                     0,
                     KEY_SET_VALUE,
                     &hKey) == ERROR_SUCCESS)
    {
        DWORD value = mode;
        RegSetValueEx(hKey, L"SystemUsesLightTheme", 0, REG_DWORD, reinterpret_cast<const BYTE*>(&value), sizeof(value));
        RegCloseKey(hKey);

        if (mode) // if are changing to light mode
        {
            ResetColorPrevalence();
            Logger::info(L"[LightSwitchService] Reset ColorPrevalence to default when switching to light mode.");
        }

        SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, 0, reinterpret_cast<LPARAM>(L"ImmersiveColorSet"), SMTO_ABORTIFHUNG, 5000, nullptr);

        SendMessageTimeout(HWND_BROADCAST, WM_THEMECHANGED, 0, 0, SMTO_ABORTIFHUNG, 5000, nullptr);
    }
}

void SetThemeFile(const std::wstring& themeFilePath)
{
    SHELLEXECUTEINFOW sei{};
    sei.cbSize = sizeof(sei);
    sei.fMask = SEE_MASK_FLAG_NO_UI | SEE_MASK_NOCLOSEPROCESS;
    sei.lpVerb = L"open";
    sei.lpFile = themeFilePath.c_str();
    sei.nShow = SW_SHOWNORMAL;

    if (!ShellExecuteExW(&sei))
    {
        DWORD err = GetLastError();
        Logger::error(L"[LightSwitch] ShellExecuteExW failed ({}): {}", err, themeFilePath);
        return;
    }

    if (sei.hProcess)
    {
        WaitForInputIdle(sei.hProcess, 2000);
        CloseHandle(sei.hProcess);
    }
}

// Can think of this as "is the current theme light?"
bool GetCurrentSystemTheme()
{
    HKEY hKey;
    DWORD value = 1; // default = light
    DWORD size = sizeof(value);

    if (RegOpenKeyEx(HKEY_CURRENT_USER,
                     PERSONALIZATION_REGISTRY_PATH,
                     0,
                     KEY_READ,
                     &hKey) == ERROR_SUCCESS)
    {
        RegQueryValueEx(hKey, L"SystemUsesLightTheme", nullptr, nullptr, reinterpret_cast<LPBYTE>(&value), &size);
        RegCloseKey(hKey);
    }

    return value == 1; // true = light, false = dark
}

bool GetCurrentAppsTheme()
{
    HKEY hKey;
    DWORD value = 1;
    DWORD size = sizeof(value);

    if (RegOpenKeyEx(HKEY_CURRENT_USER,
                     PERSONALIZATION_REGISTRY_PATH,
                     0,
                     KEY_READ,
                     &hKey) == ERROR_SUCCESS)
    {
        RegQueryValueEx(hKey, L"AppsUseLightTheme", nullptr, nullptr, reinterpret_cast<LPBYTE>(&value), &size);
        RegCloseKey(hKey);
    }

    return value == 1; // true = light, false = dark
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