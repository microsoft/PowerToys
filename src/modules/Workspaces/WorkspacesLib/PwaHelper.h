#pragma once

#include <functional>

#include <WorkspacesLib/AppUtils.h>

namespace Utils
{
    class PwaHelper
    {
    public:
        PwaHelper();
        ~PwaHelper() = default;

        std::wstring GetAUMIDFromWindow(HWND hWnd) const;

        std::optional<std::wstring> GetEdgeAppId(const std::wstring& windowAumid) const;
        std::optional<std::wstring> GetChromeAppId(const std::wstring& windowAumid) const;   
        std::wstring SearchPwaName(const std::wstring& pwaAppId, const std::wstring& windowAumid) const;
        
    private:
        void InitAppIds(const std::wstring& browserDataFolder, const std::wstring& browserDirPrefix, const std::function<void(const std::wstring&)>& addingAppIdCallback);
        void InitEdgeAppIds();
        void InitChromeAppIds();

        std::wstring GetAppIdFromCommandLineArgs(const std::wstring& commandLineArgs) const;
        std::wstring GetAUMIDFromProcessId(DWORD processId) const;

        std::map<std::wstring, std::wstring> m_edgeAppIds;
        std::vector<std::wstring> m_chromeAppIds;
        std::map<std::wstring, std::wstring> m_pwaAppIdsToAppNames;
    };
}
