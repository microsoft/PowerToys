#pragma once

#include <Windows.h>

namespace ProjectsWindowProperties
{
    namespace Properties
    {
        const wchar_t LaunchedByProjectsID[] = L"PowerToys_LaunchedByProjects";
    }

    inline void StampProjectsLaunchedProperty(HWND window)
    {
        ::SetPropW(window, Properties::LaunchedByProjectsID, reinterpret_cast<HANDLE>(1));
    }
}
