// Port Based on https://stackoverflow.com/a/62811758/5001796
#include "theme_helpers.h"
#include "dwmapi.h"
#include <windows.h>
#include <iostream>
#include <vector>

#define STATUS_SUCCESS 0x00000000
#define DWMWA_USE_IMMERSIVE_DARK_MODE 20
#define HKEY_WINDOWS_THEME L"Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize"

typedef NTSTATUS(WINAPI* RtlGetVersionPtr)(PRTL_OSVERSIONINFOW);

namespace
{
    RTL_OSVERSIONINFOW GetRealOSVersion()
    {
        HMODULE hMod = ::GetModuleHandleW(L"ntdll.dll");
        if (hMod)
        {
            RtlGetVersionPtr fxPtr = (RtlGetVersionPtr)::GetProcAddress(hMod, "RtlGetVersion");
            if (fxPtr != nullptr)
            {
                RTL_OSVERSIONINFOW rovi = { 0 };
                rovi.dwOSVersionInfoSize = sizeof(rovi);
                if (STATUS_SUCCESS == fxPtr(&rovi))
                {
                    return rovi;
                }
            }
        }
        RTL_OSVERSIONINFOW info = { 0 };
        return info;
    }

    DWORD WINAPI CheckImmersiveDarkMode(LPVOID lpParam)
    {
        HWND window = (HWND)lpParam;
        HANDLE hEvent;
        HKEY hKey;

        // Open the Key to listen
        RegOpenKeyEx(HKEY_CURRENT_USER, HKEY_WINDOWS_THEME, 0, KEY_NOTIFY, &hKey);

        while (true)
        {
            // Create an event.
            hEvent = CreateEvent(NULL, TRUE, FALSE, NULL);

            // Watch the registry key for a change of value.
            RegNotifyChangeKeyValue(hKey,
                                    TRUE,
                                    REG_NOTIFY_CHANGE_LAST_SET,
                                    hEvent,
                                    TRUE);

            WaitForSingleObject(hEvent, INFINITE);

            auto theme = ThemeHelpers::GetSystemTheme();
            ThemeHelpers::SetImmersiveDarkMode(window, theme == CurrentTheme::Dark);
        }

        return 0;
    }
}

// based on https://stackoverflow.com/questions/51334674/how-to-detect-windows-10-light-dark-mode-in-win32-application
CurrentTheme ThemeHelpers::GetSystemTheme()
{
    // The value is expected to be a REG_DWORD, which is a signed 32-bit little-endian
    auto buffer = std::vector<char>(4);
    auto cbData = static_cast<DWORD>(buffer.size() * sizeof(char));
    auto res = RegGetValueW(
        HKEY_CURRENT_USER,
        HKEY_WINDOWS_THEME,
        L"AppsUseLightTheme",
        RRF_RT_REG_DWORD, // expected value type
        nullptr,
        buffer.data(),
        &cbData);

    if (res != ERROR_SUCCESS)
    {
        return CurrentTheme::Light;
    }

    // convert bytes written to our buffer to an int, assuming little-endian
    auto i = int(buffer[3] << 24 |
                 buffer[2] << 16 |
                 buffer[1] << 8 |
                 buffer[0]);

    return CurrentTheme(i);
}

bool ThemeHelpers::SupportsImmersiveDarkMode()
{
    auto info = GetRealOSVersion();
    return info.dwMajorVersion >= 10 && info.dwBuildNumber >= 18985;
}

void ThemeHelpers::SetImmersiveDarkMode(HWND window, bool enabled)
{
    if (ThemeHelpers::SupportsImmersiveDarkMode())
    {
        int useImmersiveDarkMode = enabled ? 1 : 0;
        DwmSetWindowAttribute(window, DWMWA_USE_IMMERSIVE_DARK_MODE, &useImmersiveDarkMode, sizeof(useImmersiveDarkMode));
    }
}

void ThemeHelpers::RegisterForImmersiveDarkMode(HWND window)
{
    if (ThemeHelpers::SupportsImmersiveDarkMode())
    {
        ThemeHelpers::SetImmersiveDarkMode(window, ThemeHelpers::GetSystemTheme() == CurrentTheme::Dark);

        DWORD dwThreadId;
        CreateThread(NULL, 0, (LPTHREAD_START_ROUTINE)CheckImmersiveDarkMode, window, 0, &dwThreadId);
    }
}