#pragma once

#include <WorkspacesLib/AppUtils.h>

namespace Utils
{
    class PwaHelper
    {
    public:
        void UpdatePwaApp(Apps::AppData* appData, HWND window);

    private:
        void InitEdgeAppIds();
        void InitChromeAppIds();

        std::wstring GetAUMIDFromWindow(HWND hWnd) const;
        std::wstring GetAUMIDFromProcessId(DWORD processId) const;

        std::optional<std::wstring> GetPwaAppId(const std::wstring& windowAumid) const;
        std::wstring SearchPwaName(const std::wstring& pwaAppId, const std::wstring& windowAumid) const;
        std::optional<std::wstring> SearchPwaAppId(const std::wstring& windowAumid) const;

        std::map<std::wstring, std::wstring> m_pwaAumidToAppId;
        std::vector<std::wstring> m_chromeAppIds;
        std::map<std::wstring, std::wstring> m_pwaAppIdsToAppNames;
    };
}
