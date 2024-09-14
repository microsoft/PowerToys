#pragma once

#include <Windows.h>

namespace WorkspacesWindowProperties
{
    namespace Properties
    {
        const wchar_t LaunchedByWorkspacesID[] = L"PowerToys_LaunchedByWorkspaces";
    }

    inline void StampWorkspacesLaunchedProperty(HWND window)
    {
        ::SetPropW(window, Properties::LaunchedByWorkspacesID, reinterpret_cast<HANDLE>(1));
    }
}
