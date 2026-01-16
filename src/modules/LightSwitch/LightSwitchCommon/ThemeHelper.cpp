#include <windows.h>
#include <shellapi.h>
#include <thread>
#include <atomic>
#include <chrono>
#include <tlhelp32.h>
#include <psapi.h>
#include <vector>

#include "ThemeHelper.h"
#include "SettingsConstants.h"
#include <common/logger/logger.h>

// Controls changing the themes.

// Global flag to control the monitoring thread
static std::atomic<bool> g_stopMonitoring(false);
static std::thread g_monitorThread;
static std::atomic<bool> g_monitorRunning(false);

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
            Logger::info(L"[LightSwitch] Reset ColorPrevalence to default when switching to light mode.");
        }

        SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, 0, reinterpret_cast<LPARAM>(L"ImmersiveColorSet"), SMTO_ABORTIFHUNG, 5000, nullptr);
        SendMessageTimeout(HWND_BROADCAST, WM_THEMECHANGED, 0, 0, SMTO_ABORTIFHUNG, 5000, nullptr);
    }
}

static bool GetProcessImagePath(DWORD pid, std::wstring& outPath)
{
    outPath.clear();

    HANDLE hProc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, pid);
    if (!hProc)
        return false;

    wchar_t buf[MAX_PATH]{};
    DWORD size = ARRAYSIZE(buf);

    bool ok = QueryFullProcessImageNameW(hProc, 0, buf, &size) != 0;
    CloseHandle(hProc);

    if (!ok)
        return false;

    outPath.assign(buf, size);
    return true;
}

static bool IsSettingsFrameWindow(HWND hwnd)
{
    wchar_t cls[128]{};
    if (!GetClassNameW(hwnd, cls, ARRAYSIZE(cls)))
        return false;

    if (wcscmp(cls, L"ApplicationFrameWindow") != 0)
        return false;

    if (!IsWindowVisible(hwnd))
        return false;

    DWORD pid = 0;
    GetWindowThreadProcessId(hwnd, &pid);
    if (!pid)
        return false;

    std::wstring imagePath;
    if (!GetProcessImagePath(pid, imagePath))
        return false;

    // Direct hit: Settings process
    if (imagePath.find(L"SystemSettings.exe") != std::wstring::npos)
        return true;

    // Hosted case: sometimes you see ApplicationFrameHost instead.
    // Use window title as a fallback signal.
    if (imagePath.find(L"ApplicationFrameHost.exe") != std::wstring::npos)
    {
        wchar_t title[256]{};
        GetWindowTextW(hwnd, title, ARRAYSIZE(title));
        if (wcslen(title) > 0 && wcsstr(title, L"Settings") != nullptr)
            return true;
    }

    return false;
}

// Aggressively suppress Settings window - hide, move off-screen, close, and terminate
static void SuppressSettingsWindow(HWND hwnd)
{
    // First, immediately hide it to prevent flash
    ShowWindow(hwnd, SW_HIDE);
    
    // Move it off-screen as backup
    SetWindowPos(hwnd, nullptr, -32000, -32000, 0, 0, 
        SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
    
    // Send close message
    PostMessageW(hwnd, WM_CLOSE, 0, 0);
    
    // Also terminate the process for faster closure
    DWORD processId = 0;
    GetWindowThreadProcessId(hwnd, &processId);
    if (processId != 0)
    {
        HANDLE hProcess = OpenProcess(PROCESS_TERMINATE, FALSE, processId);
        if (hProcess)
        {
            TerminateProcess(hProcess, 0);
            CloseHandle(hProcess);
        }
    }
}

// Find and suppress Settings app windows
static bool FindAndSuppressSettings()
{
    HWND settingsHwnd = nullptr;

    EnumWindows([](HWND hwnd, LPARAM lParam) -> BOOL {
        if (IsSettingsFrameWindow(hwnd))
        {
            *reinterpret_cast<HWND*>(lParam) = hwnd;
            return FALSE; // stop enumeration
        }
        return TRUE;
    },
                reinterpret_cast<LPARAM>(&settingsHwnd));

    if (settingsHwnd)
    {
        SuppressSettingsWindow(settingsHwnd);
        return true;
    }
    return false;
}

// Pre-emptive monitor that catches Settings via process enumeration
// This is the critical function - it must suppress windows BEFORE they become visible
static void PreemptiveSettingsMonitor()
{
    while (!g_stopMonitoring)
    {
        // Check for SystemSettings.exe process and suppress its windows
        HANDLE hSnapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (hSnapshot != INVALID_HANDLE_VALUE)
        {
            PROCESSENTRY32W pe;
            pe.dwSize = sizeof(pe);

            if (Process32FirstW(hSnapshot, &pe))
            {
                do
                {
                    if (_wcsicmp(pe.szExeFile, L"SystemSettings.exe") == 0)
                    {
                        // Found Settings process - enumerate and hide ALL its windows immediately
                        // DO NOT check IsWindowVisible - we need to catch windows before they show
                        EnumWindows([](HWND hwnd, LPARAM lParam) -> BOOL {
                            DWORD pid = 0;
                            GetWindowThreadProcessId(hwnd, &pid);
                            if (pid == static_cast<DWORD>(lParam))
                            {
                                // Hide immediately without visibility check
                                ShowWindow(hwnd, SW_HIDE);
                                SetWindowPos(hwnd, nullptr, -32000, -32000, 0, 0,
                                    SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
                                PostMessageW(hwnd, WM_CLOSE, 0, 0);
                            }
                            return TRUE;
                        }, static_cast<LPARAM>(pe.th32ProcessID));

                        // Terminate the process immediately
                        HANDLE hProc = OpenProcess(PROCESS_TERMINATE, FALSE, pe.th32ProcessID);
                        if (hProc)
                        {
                            TerminateProcess(hProc, 0);
                            CloseHandle(hProc);
                        }
                    }
                } while (Process32NextW(hSnapshot, &pe));
            }
            CloseHandle(hSnapshot);
        }

        // Also check ApplicationFrameHost windows that might be hosting Settings
        // This catches Settings windows even when the process is ApplicationFrameHost.exe
        EnumWindows([](HWND hwnd, LPARAM) -> BOOL {
            wchar_t cls[128]{};
            if (!GetClassNameW(hwnd, cls, ARRAYSIZE(cls)))
                return TRUE;

            if (wcscmp(cls, L"ApplicationFrameWindow") != 0)
                return TRUE;

            // Check window title for Settings - don't require visibility
            wchar_t title[256]{};
            GetWindowTextW(hwnd, title, ARRAYSIZE(title));
            if (wcslen(title) > 0 && wcsstr(title, L"Settings") != nullptr)
            {
                ShowWindow(hwnd, SW_HIDE);
                SetWindowPos(hwnd, nullptr, -32000, -32000, 0, 0,
                    SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
                PostMessageW(hwnd, WM_CLOSE, 0, 0);
                
                DWORD processId = 0;
                GetWindowThreadProcessId(hwnd, &processId);
                if (processId != 0)
                {
                    HANDLE hProcess = OpenProcess(PROCESS_TERMINATE, FALSE, processId);
                    if (hProcess)
                    {
                        TerminateProcess(hProcess, 0);
                        CloseHandle(hProcess);
                    }
                }
            }
            return TRUE;
        }, 0);

        // Very short sleep to catch it quickly (10ms polling)
        std::this_thread::sleep_for(std::chrono::milliseconds(10));
    }
}

// Start the preemptive Settings monitor - call this before any operation that might trigger Settings
void StartSettingsMonitor()
{
    // If already running, do nothing
    if (g_monitorRunning.exchange(true))
    {
        return;
    }

    g_stopMonitoring = false;
    g_monitorThread = std::thread(PreemptiveSettingsMonitor);
    
    // Small delay to ensure monitor is running
    std::this_thread::sleep_for(std::chrono::milliseconds(50));
}

// Stop the preemptive Settings monitor
void StopSettingsMonitor()
{
    if (!g_monitorRunning)
    {
        return;
    }

    g_stopMonitoring = true;
    
    if (g_monitorThread.joinable())
    {
        g_monitorThread.join();
    }
    
    g_monitorRunning = false;

    // Final cleanup pass
    for (int i = 0; i < 5; i++)
    {
        FindAndSuppressSettings();
        std::this_thread::sleep_for(std::chrono::milliseconds(100));
    }
}

void SetThemeFile(const std::wstring& themeFilePath)
{
    // Check if monitor is already running from external call (e.g., ToggleTheme)
    bool externalMonitor = g_monitorRunning;
    
    if (!externalMonitor)
    {
        // Start our own monitor
        StartSettingsMonitor();
    }

    // Execute the theme file
    SHELLEXECUTEINFOW sei{};
    sei.cbSize = sizeof(sei);
    sei.fMask = SEE_MASK_FLAG_NO_UI | SEE_MASK_NOCLOSEPROCESS | SEE_MASK_NOASYNC;
    sei.lpVerb = L"open";
    sei.lpFile = themeFilePath.c_str();
    sei.nShow = SW_HIDE;  // Try to hide from the start

    if (!ShellExecuteExW(&sei))
    {
        DWORD err = GetLastError();
        Logger::error(L"[LightSwitch] ShellExecuteExW failed ({}): {}", err, themeFilePath);
        if (!externalMonitor)
        {
            StopSettingsMonitor();
        }
        return;
    }

    if (sei.hProcess)
    {
        WaitForInputIdle(sei.hProcess, 3000);
        CloseHandle(sei.hProcess);
    }

    // Give time for the theme to be applied
    std::this_thread::sleep_for(std::chrono::milliseconds(1500));

    // Only stop if we started it
    if (!externalMonitor)
    {
        StopSettingsMonitor();
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
