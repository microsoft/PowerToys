#include "theme_helpers.h"
#include "dwmapi.h"
#include <windows.h>
#include <iostream>
#include <vector>
#include <mutex>

typedef void (*THEME_HANDLE)();
DWORD WINAPI _checkTheme(LPVOID lpParam);

#pragma once
class ThemeListener
{
public:
    ThemeListener()
    {
        AppTheme = ThemeHelpers::GetAppTheme();
        SystemTheme = ThemeHelpers::GetSystemTheme();
        dwThreadHandle = CreateThread(NULL, 0, (LPTHREAD_START_ROUTINE)_checkTheme, this, 0, &dwThreadId);
    }
    ~ThemeListener()
    {
        CloseHandle(dwThreadHandle);
        dwThreadId = 0;
    }

    Theme AppTheme;
    Theme SystemTheme;
    void AddChangedHandler(THEME_HANDLE handle);
    void DelChangedHandler(THEME_HANDLE handle);
    void AddAppThemeChangedHandler(THEME_HANDLE handle);
    void DelAppThemeChangedHandler(THEME_HANDLE handle);
    void AddSystemThemeChangedHandler(THEME_HANDLE handle);
    void DelSystemThemeChangedHandler(THEME_HANDLE handle);
    void CheckTheme();

private:
    HANDLE dwThreadHandle;
    DWORD dwThreadId;
    std::vector<THEME_HANDLE> handles;
    std::vector<THEME_HANDLE> appThemeHandles;
    std::vector<THEME_HANDLE> systemThemeHandles;
    mutable std::mutex handlesMutex;
};