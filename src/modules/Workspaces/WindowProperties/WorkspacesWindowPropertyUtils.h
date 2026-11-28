#pragma once

#include <Windows.h>

namespace WorkspacesWindowProperties
{
    namespace Properties
    {
        const wchar_t LaunchedByWorkspacesID[] = L"PowerToys_LaunchedByWorkspaces";
        const wchar_t WorkspacesAppID[] = L"PowerToys_WorkspacesAppId";
    }

    inline void StampWorkspacesLaunchedProperty(HWND window)
    {
        ::SetPropW(window, Properties::LaunchedByWorkspacesID, reinterpret_cast<HANDLE>(1));
    }

    inline void StampWorkspacesGuidProperty(HWND window, const std::wstring& appId)
    {
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

        // the size of the HANDLE type can vary on different systems: 4 or 8 bytes. As we can set only a HANDLE as a property, we need more properties (2 or 4) to be able to store a GUID (16 bytes)
        const int numberOfProperties = sizeof(GUID) / sizeof(HANDLE);

        uint64_t parts[numberOfProperties];
        std::memcpy(&parts[0], &guid, sizeof(GUID));
        for (unsigned char partIndex = 0; partIndex < numberOfProperties; partIndex++)
        {
            workspacesAppIDPart[workspacesAppIDLength] = '0' + partIndex;
            ::SetPropW(window, workspacesAppIDPart, reinterpret_cast<HANDLE>(parts[partIndex]));
        }
    }

    inline const std::wstring GetGuidFromHwnd(HWND window)
    {
        const size_t workspacesAppIDLength = wcslen(Properties::WorkspacesAppID);
        wchar_t* workspacesAppIDPart = new wchar_t[workspacesAppIDLength + 2];
        std::memcpy(&workspacesAppIDPart[0], &Properties::WorkspacesAppID, workspacesAppIDLength * sizeof(wchar_t));
        workspacesAppIDPart[workspacesAppIDLength + 1] = 0;

        // the size of the HANDLE type can vary on different systems: 4 or 8 bytes. As we can set only a HANDLE as a property, we need more properties (2 or 4) to be able to store a GUID (16 bytes)
        const int numberOfProperties = sizeof(GUID) / sizeof(HANDLE);

        uint64_t parts[numberOfProperties];
        for (unsigned char partIndex = 0; partIndex < numberOfProperties; partIndex++)
        {
            workspacesAppIDPart[workspacesAppIDLength] = '0' + partIndex;
            HANDLE rawData = GetPropW(window, workspacesAppIDPart);
            if (rawData)
            {
                parts[partIndex] = reinterpret_cast<uint64_t>(rawData);
            }
            else
            {
                return L"";
            }
        }

        GUID guid;
        std::memcpy(&guid, &parts[0], sizeof(GUID));
        WCHAR* guidString;
        StringFromCLSID(guid, &guidString);

        return guidString;
    }
}
