#pragma once

#include <Windows.h>

namespace WorkspacesWindowProperties
{
    namespace Properties
    {
        const wchar_t LaunchedByWorkspacesID[] = L"PowerToys_LaunchedByWorkspaces";
        const wchar_t WorkspacesAppID[] = L"PowerToys_WorkspacesAppId";
    }

    inline void StampWorkspacesLaunchedProperty(HWND window, const std::wstring appId)
    {
        ::SetPropW(window, Properties::LaunchedByWorkspacesID, reinterpret_cast<HANDLE>(1));

        GUID guid;
        HRESULT hr = CLSIDFromString(appId.c_str(), static_cast<LPCLSID> (&guid));
        if (hr != S_OK)
        {
            return;
        }

        const size_t workspacesAppIDLength = wcslen(Properties::WorkspacesAppID);
        wchar_t* workspacesAppIDPart = new wchar_t[workspacesAppIDLength + 2];
        std::memcpy(&workspacesAppIDPart[0], &Properties::WorkspacesAppID, workspacesAppIDLength * sizeof(wchar_t));
        workspacesAppIDPart[workspacesAppIDLength + 1] = 0;

        uint16_t parts[8];
        std::memcpy(&parts[0], &guid, sizeof(GUID));
        for (unsigned char partIndex = 0; partIndex < 8; partIndex++)
        {
            workspacesAppIDPart[workspacesAppIDLength] = '0' + partIndex;
            ::SetPropW(window, workspacesAppIDPart, reinterpret_cast<HANDLE>(parts[partIndex]));
        }
    }
}
