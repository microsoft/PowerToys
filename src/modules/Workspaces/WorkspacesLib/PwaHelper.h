#pragma once

#include <WorkspacesLib/AppUtils.h>

namespace Utils
{
    class PwaHelper
    {
    public:
        void UpdatePwaApp(Apps::AppData* appData, HWND window);

    private:
        std::map<std::wstring, std::wstring> m_pwaAumidToAppId;
        std::vector<std::wstring> m_chromeAppIds;
        std::map<std::wstring, std::wstring> m_pwaAppIdsToAppNames;

        void InitAumidToAppId();
        void InitChromeAppIds();
        std::optional<std::wstring> GetAppId_7(HWND hWnd) const;
        std::optional<std::wstring> GetAppId_8(HWND hWnd) const;
        std::wstring GetAppId(HWND hWnd) const;
        std::optional<std::wstring> GetProcessId_7(DWORD dwProcessId) const;
        std::optional<std::wstring> GetProcessId_8(DWORD dwProcessId) const;
        std::wstring GetProcessId(DWORD dwProcessId) const;
        std::optional<std::wstring> GetPwaAppId(const std::wstring& windowAumid) const;
        std::wstring SearchPwaName(const std::wstring& pwaAppId, const std::wstring& windowAumid) const;
        std::optional<std::wstring> SearchPwaAppId(const std::wstring& windowAumid) const;
    };
}
