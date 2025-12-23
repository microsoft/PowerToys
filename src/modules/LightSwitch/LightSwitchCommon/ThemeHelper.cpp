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

#include <atomic>
#include <charconv>
#include <array>
#include <string>

static bool GetWindowsVersionFromRegistryInternal(int& build, int& revision) noexcept
{
    HKEY hKey{};
    auto closeKey = RegKeyGuard(hKey);
    if (RegOpenKeyExW(HKEY_LOCAL_MACHINE, L"SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion", 0, KEY_READ, &hKey) != ERROR_SUCCESS)
    {
        return false;
    }
    wchar_t buffer[11]{};
    DWORD bufferSize{ sizeof(buffer) };
    if (RegGetValueW(hKey, nullptr, L"CurrentBuildNumber", RRF_RT_REG_SZ, nullptr, static_cast<void*>(buffer), &bufferSize))
    {
        return false;
    }
    char bufferA[11]{};
    std::transform(std::begin(buffer), std::end(buffer), std::begin(bufferA), [](auto c) { return static_cast<char>(c); });
    int bld{};
    if (std::from_chars(bufferA, bufferA + sizeof(bufferA), bld).ec != std::errc{})
    {
        return false;
    }
    DWORD rev{};
    DWORD revSize{ sizeof(rev) };
    if (RegGetValueW(hKey, nullptr, L"UBR", RRF_RT_DWORD, nullptr, &rev, &revSize) != ERROR_SUCCESS)
    {
        return false;
    }
    revision = static_cast<int>(rev);
    build = static_cast<int>(bld);
    return true;
}

static bool GetWindowsVersionFromRegistry(int& build, int& revision) noexcept
{
    static std::atomic<int> build_cache{};
    static std::atomic<int> rev_cache{};

    if (auto bld = build_cache.load(); bld != 0)
    {
        build = bld;
        revision = rev_cache.load();
        return true;
    }

    int bld{};
    int rev{};
    if (auto e = GetWindowsVersionFromRegistryInternal(bld, rev); e == false)
    {
        return e;
    }
    build = bld;
    revision = rev;
    rev_cache.store(rev);
    // Write after rev_cache for condition
    build_cache.store(bld);
    return true;
}

// This function will supplement the wallpaper path setting. It does not cause the wallpaper to change, but for consistency, it is better to set it
static int SetRemainWallpaperPathRegistry(std::wstring const& wallpaperPath) noexcept
{
    HKEY hKey{};
    auto closeKey = RegKeyGuard(hKey);

    if (RegOpenKeyExW(HKEY_CURRENT_USER, L"Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Wallpapers", 0, KEY_WRITE, &hKey) != ERROR_SUCCESS)
    {
        // The key may not exist after updating Windows, so it is not an error
        // The key will be created by the Settings app
        return 0;
    }
    if (RegSetValueExW(hKey, L"CurrentWallpaperPath", 0, REG_SZ, reinterpret_cast<const BYTE*>(wallpaperPath.data()), static_cast<DWORD>((wallpaperPath.size() + 1u) * sizeof(wchar_t))) != ERROR_SUCCESS)
    {
        return 0x301;
    }
    DWORD backgroundType = 0; // 0 = picture, 1 = solid color, 2 = slideshow
    if (RegSetValueExW(hKey, L"BackgroundType", 0, REG_DWORD, reinterpret_cast<const BYTE*>(&backgroundType), static_cast<DWORD>(sizeof(DWORD))) != ERROR_SUCCESS)
    {
        return 0x302;
    }
    return 0;
}

#define WIN32_LEAN_AND_MEAN
#define COM_NO_WINDOWS_H
#include <string>
#include <unknwn.h>
#include <inspectable.h>
#include <restrictederrorinfo.h>
#include "Shobjidl.h"
#include <hstring.h>
#include <winrt/base.h>

#pragma comment(lib, "runtimeobject.lib")

// COM interface definition from https://github.com/MScholtes/VirtualDesktop

inline constexpr GUID CLSID_ImmersiveShell{ 0xC2F03A33, 0x21F5, 0x47FA, { 0xB4, 0xBB, 0x15, 0x63, 0x62, 0xA2, 0xF2, 0x39 } };
inline constexpr GUID CLSID_VirtualDesktopManagerInternal{ 0xC5E0CDCA, 0x7B6E, 0x41B2, { 0x9F, 0xC4, 0xD9, 0x39, 0x75, 0xCC, 0x46, 0x7B } };

struct __declspec(novtable) __declspec(uuid("6D5140C1-7436-11CE-8034-00AA006009FA")) IServiceProvider10 : public IUnknown
{
    virtual HRESULT __stdcall QueryService(REFGUID service, REFIID riid, void** obj) = 0;
};

#undef CreateDesktop

struct __declspec(novtable) __declspec(uuid("53F5CA0B-158F-4124-900C-057158060B27")) IVirtualDesktopManagerInternal24H2 : public IUnknown
{
    virtual HRESULT __stdcall GetCount(int* count) = 0;
    virtual HRESULT __stdcall MoveViewToDesktop(IInspectable* view, IUnknown* desktop) = 0;
    virtual HRESULT __stdcall CanViewMoveDesktops(IInspectable* view, bool* result) = 0;
    virtual HRESULT __stdcall GetCurrentDesktop(IUnknown** desktop) = 0;
    virtual HRESULT __stdcall GetDesktops(IObjectArray** desktops) = 0;
    virtual HRESULT __stdcall GetAdjacentDesktop(IUnknown* from, int direction, IUnknown** desktop) = 0;
    virtual HRESULT __stdcall SwitchDesktop(IUnknown* desktop) = 0;
    virtual HRESULT __stdcall SwitchDesktopAndMoveForegroundView(IUnknown* desktop) = 0;
    virtual HRESULT __stdcall CreateDesktop(IUnknown** desktop) = 0;
    virtual HRESULT __stdcall MoveDesktop(IUnknown* desktop, int nIndex) = 0;
    virtual HRESULT __stdcall RemoveDesktop(IUnknown* desktop, IUnknown* fallback) = 0;
    virtual HRESULT __stdcall FindDesktop(const GUID* desktopId, IUnknown** desktop) = 0;
    virtual HRESULT __stdcall GetDesktopSwitchIncludeExcludeViews(IUnknown* desktop, IObjectArray** unknown1, IObjectArray** unknown2) = 0;
    virtual HRESULT __stdcall SetDesktopName(IUnknown* desktop, HSTRING name) = 0;
    virtual HRESULT __stdcall SetDesktopWallpaper(IUnknown* desktop, HSTRING path) = 0;
    virtual HRESULT __stdcall UpdateWallpaperPathForAllDesktops(HSTRING path) = 0;
    virtual HRESULT __stdcall CopyDesktopState(IInspectable* pView0, IInspectable* pView1) = 0;
    virtual HRESULT __stdcall CreateRemoteDesktop(HSTRING path, IUnknown** desktop) = 0;
    virtual HRESULT __stdcall SwitchRemoteDesktop(IUnknown* desktop, void* switchType) = 0;
    virtual HRESULT __stdcall SwitchDesktopWithAnimation(IUnknown* desktop) = 0;
    virtual HRESULT __stdcall GetLastActiveDesktop(IUnknown** desktop) = 0;
    virtual HRESULT __stdcall WaitForAnimationToComplete() = 0;
};

// Using this method to set the wallpaper works across virtual desktops, but it does not provide the functionality to set the style
static int SetWallpaperViaIVirtualDesktopManagerInternal(const std::wstring& path) noexcept
{
    int build{};
    int revision{};
    if (!GetWindowsVersionFromRegistry(build, revision))
    {
        return 0x201;
    }
    // Unstable Windows internal API, at least 24H2 required
    if (build < 26100)
    {
        return 0x202;
    }
    auto shell = winrt::try_create_instance<IServiceProvider10>(CLSID_ImmersiveShell, CLSCTX_LOCAL_SERVER);
    if (!shell)
    {
        return 0x203;
    }
    winrt::com_ptr<IVirtualDesktopManagerInternal24H2> virtualDesktopManagerInternal;
    if (shell->QueryService(
            CLSID_VirtualDesktopManagerInternal,
            __uuidof(IVirtualDesktopManagerInternal24H2),
            virtualDesktopManagerInternal.put_void()) != S_OK)
    {
        return 0x204;
    }
    if (virtualDesktopManagerInternal->UpdateWallpaperPathForAllDesktops(static_cast<HSTRING>(winrt::detach_abi(path))) != S_OK)
    {
        return 0x205;
    }
    return 0;
}

// After setting the wallpaper using this method, switching to other virtual desktops will cause the wallpaper to be restored
static int SetWallpaperViaIDesktopWallpaper(const std::wstring& path, int style) noexcept
{
    switch (style)
    {
    case 0: // Fill
    case 1: // Fit
    case 2: // Stretch
    case 3: // Tile
    case 4: // Center
    case 5: // Span
        break;
    default:
        std::terminate();
    }
    auto desktopWallpaper = winrt::try_create_instance<IDesktopWallpaper>(__uuidof(DesktopWallpaper), CLSCTX_LOCAL_SERVER);
    if (!desktopWallpaper)
    {
        return 0x301;
    }
    if (desktopWallpaper->SetPosition(static_cast<DESKTOP_WALLPAPER_POSITION>(style)) != S_OK)
    {
        return 0x302;
    }
    if (desktopWallpaper->SetWallpaper(nullptr, path.c_str()) != S_OK)
    {
        return 0x303;
    }
    return 0;
}

int SetDesktopWallpaper(const std::wstring& path, int style, bool virtualDesktop) noexcept
{
    if (virtualDesktop)
    {
        if (auto e = SetWallpaperViaIVirtualDesktopManagerInternal(path); e != 0)
        {
            return e;
        }
    }
    if (auto e = SetWallpaperViaIDesktopWallpaper(path, style); e != 0)
    {
        return e;
    }
    if (auto e = SetRemainWallpaperPathRegistry(path); e != 0)
    {
        return e;
    }
    return 0;
}