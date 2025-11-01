#include "theme_helpers.h"
#include "dwmapi.h"
#include <windows.h>
#include <iostream>
#include <vector>

typedef void (*THEME_HANDLE)();
DWORD WINAPI _checkTheme(LPVOID lpParam);

#pragma once
class ThemeListener
{
public:
    ThemeListener()
    {
        AppTheme = ThemeHelpers::GetAppTheme();
        dwThreadHandle = CreateThread(NULL, 0, (LPTHREAD_START_ROUTINE)_checkTheme, this, 0, &dwThreadId);
    }
    ~ThemeListener()
    {
        CloseHandle(dwThreadHandle);
        dwThreadId = 0;
    }

    Theme AppTheme;
    void AddChangedHandler(THEME_HANDLE handle);
    void DelChangedHandler(THEME_HANDLE handle);
    void CheckTheme();

private:
    HANDLE dwThreadHandle;
    DWORD dwThreadId;
    std::vector<THEME_HANDLE> handles;
};