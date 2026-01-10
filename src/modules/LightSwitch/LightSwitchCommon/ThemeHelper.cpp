#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <logger/logger_settings.h>
#include <logger/logger.h>
#include <utils/logger_helper.h>
#include "ThemeHelper.h"
#include <wil/resource.h>
#include "SettingsConstants.h"

static auto RegKeyGuard(HKEY& hKey) noexcept
{
    return wil::scope_exit([&hKey]() {
        if (hKey == nullptr)
            return;
        if (RegCloseKey(hKey) != ERROR_SUCCESS)
            std::terminate();
    });
}

// Controls changing the themes.
static void ResetColorPrevalence() noexcept
{
    HKEY hKey{};
    auto closeKey = RegKeyGuard(hKey);
    if (RegOpenKeyEx(HKEY_CURRENT_USER,
                     PERSONALIZATION_REGISTRY_PATH,
                     0,
                     KEY_SET_VALUE,
                     &hKey) == ERROR_SUCCESS)
    {
        DWORD value = 0; // back to default value
        RegSetValueEx(hKey, L"ColorPrevalence", 0, REG_DWORD, reinterpret_cast<const BYTE*>(&value), sizeof(value));

        SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, 0, reinterpret_cast<LPARAM>(L"ImmersiveColorSet"), SMTO_ABORTIFHUNG, 5000, nullptr);

        SendMessageTimeout(HWND_BROADCAST, WM_THEMECHANGED, 0, 0, SMTO_ABORTIFHUNG, 5000, nullptr);

        SendMessageTimeout(HWND_BROADCAST, WM_DWMCOLORIZATIONCOLORCHANGED, 0, 0, SMTO_ABORTIFHUNG, 5000, nullptr);
    }
}

void SetAppsTheme(bool mode) noexcept
{
    HKEY hKey{};
    auto closeKey = RegKeyGuard(hKey);
    if (RegOpenKeyEx(HKEY_CURRENT_USER,
                     PERSONALIZATION_REGISTRY_PATH,
                     0,
                     KEY_SET_VALUE,
                     &hKey) == ERROR_SUCCESS)
    {
        DWORD value = mode;
        RegSetValueEx(hKey, L"AppsUseLightTheme", 0, REG_DWORD, reinterpret_cast<const BYTE*>(&value), sizeof(value));

        SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, 0, reinterpret_cast<LPARAM>(L"ImmersiveColorSet"), SMTO_ABORTIFHUNG, 5000, nullptr);

        SendMessageTimeout(HWND_BROADCAST, WM_THEMECHANGED, 0, 0, SMTO_ABORTIFHUNG, 5000, nullptr);
    }
}

void SetSystemTheme(bool mode) noexcept
{
    HKEY hKey{};
    auto closeKey = RegKeyGuard(hKey);
    if (RegOpenKeyEx(HKEY_CURRENT_USER,
                     PERSONALIZATION_REGISTRY_PATH,
                     0,
                     KEY_SET_VALUE,
                     &hKey) == ERROR_SUCCESS)
    {
        DWORD value = mode;
        RegSetValueEx(hKey, L"SystemUsesLightTheme", 0, REG_DWORD, reinterpret_cast<const BYTE*>(&value), sizeof(value));

        if (mode) // if are changing to light mode
        {
            ResetColorPrevalence();
            Logger::info(L"[LightSwitchService] Reset ColorPrevalence to default when switching to light mode.");
        }

        SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, 0, reinterpret_cast<LPARAM>(L"ImmersiveColorSet"), SMTO_ABORTIFHUNG, 5000, nullptr);

        SendMessageTimeout(HWND_BROADCAST, WM_THEMECHANGED, 0, 0, SMTO_ABORTIFHUNG, 5000, nullptr);
    }
}

// Can think of this as "is the current theme light?"
bool GetCurrentSystemTheme() noexcept
{
    HKEY hKey{};
    auto closeKey = RegKeyGuard(hKey);
    DWORD value = 1; // default = light
    DWORD size = sizeof(value);

    if (RegOpenKeyEx(HKEY_CURRENT_USER,
                     PERSONALIZATION_REGISTRY_PATH,
                     0,
                     KEY_READ,
                     &hKey) == ERROR_SUCCESS)
    {
        RegQueryValueEx(hKey, L"SystemUsesLightTheme", nullptr, nullptr, reinterpret_cast<LPBYTE>(&value), &size);
    }

    return value == 1; // true = light, false = dark
}

bool GetCurrentAppsTheme() noexcept
{
    HKEY hKey{};
    auto closeKey = RegKeyGuard(hKey);
    DWORD value = 1;
    DWORD size = sizeof(value);

    if (RegOpenKeyEx(HKEY_CURRENT_USER,
                     PERSONALIZATION_REGISTRY_PATH,
                     0,
                     KEY_READ,
                     &hKey) == ERROR_SUCCESS)
    {
        RegQueryValueEx(hKey, L"AppsUseLightTheme", nullptr, nullptr, reinterpret_cast<LPBYTE>(&value), &size);
    }

    return value == 1; // true = light, false = dark
}

bool IsNightLightEnabled() noexcept
{
    HKEY hKey{};
    auto closeKey = RegKeyGuard(hKey);

    if (RegOpenKeyExW(HKEY_CURRENT_USER, NIGHT_LIGHT_REGISTRY_PATH, 0, KEY_READ, &hKey) != ERROR_SUCCESS)
        return false;

    // RegGetValueW will set size to the size of the data and we expect that to be at least 25 bytes (we need to access bytes 23 and 24)
    DWORD size = 0;
    if (RegGetValueW(hKey, nullptr, L"Data", RRF_RT_REG_BINARY, nullptr, nullptr, &size) != ERROR_SUCCESS || size < 25)
    {
        return false;
    }

    std::vector<BYTE> data(size);
    if (RegGetValueW(hKey, nullptr, L"Data", RRF_RT_REG_BINARY, nullptr, data.data(), &size) != ERROR_SUCCESS)
    {
        return false;
    }

    return data[23] == 0x10 && data[24] == 0x00;
}

// This function will supplement the wallpaper path setting. It does not cause the wallpaper to change, but for consistency, it is better to set it
static DWORD SetRemainWallpaperPathRegistry(std::wstring const& wallpaperPath) noexcept
{
    HKEY hKey{};
    auto closeKey = RegKeyGuard(hKey);

    if (RegOpenKeyExW(HKEY_CURRENT_USER, L"Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Wallpapers", 0, KEY_WRITE, &hKey) != ERROR_SUCCESS)
    {
        // The key may not exist after updating Windows, so it is not an error
        // The key will be created by the Settings app
        return 0;
    }
    if (auto e = RegSetValueExW(hKey, L"CurrentWallpaperPath", 0, REG_SZ, reinterpret_cast<const BYTE*>(wallpaperPath.data()), static_cast<DWORD>((wallpaperPath.size() + 1u) * sizeof(wchar_t))); e != ERROR_SUCCESS)
    {
        return e;
    }
    DWORD backgroundType = 0; // 0 = picture, 1 = solid color, 2 = slideshow
    if (auto e = RegSetValueExW(hKey, L"BackgroundType", 0, REG_DWORD, reinterpret_cast<const BYTE*>(&backgroundType), static_cast<DWORD>(sizeof(DWORD))); e != ERROR_SUCCESS)
    {
        return e;
    }
    return 0;
}

#define WIN32_LEAN_AND_MEAN
#define COM_NO_WINDOWS_H
#include <string>
#include <unknwn.h>
#include "Shobjidl.h"
#include <winrt/base.h>

// After setting the wallpaper using this method, switching to other virtual desktops will cause the wallpaper to be restored
static HRESULT SetWallpaperViaIDesktopWallpaper(const std::wstring& path, int style) noexcept
{
    auto pos = static_cast<DESKTOP_WALLPAPER_POSITION>(style);
    switch (pos)
    {
    case DWPOS_CENTER:
    case DWPOS_TILE:
    case DWPOS_STRETCH:
    case DWPOS_FIT:
    case DWPOS_FILL:
    case DWPOS_SPAN:
        break;
    default:
        std::terminate();
    }
    auto desktopWallpaper = winrt::try_create_instance<IDesktopWallpaper>(__uuidof(DesktopWallpaper), CLSCTX_LOCAL_SERVER);
    if (!desktopWallpaper)
    {
        return E_FAIL;
    }
    if (auto e = desktopWallpaper->SetPosition(pos); SUCCEEDED(e))
    {
        return e;
    }
    if (auto e = desktopWallpaper->SetWallpaper(nullptr, path.c_str()); SUCCEEDED(e))
    {
        return e;
    }
    return 0;
}

HRESULT SetDesktopWallpaper(const std::wstring& path, int style) noexcept
{
    if (auto e = SetWallpaperViaIDesktopWallpaper(path, style); FAILED(e))
    {
        return e;
    }
    if (auto e = SetRemainWallpaperPathRegistry(path); e != ERROR_SUCCESS)
    {
        return HRESULT_FROM_WIN32(e);
    }
    return 0;
}