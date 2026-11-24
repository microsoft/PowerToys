#pragma once

#include <filesystem>
#include <map>
#include <optional>
#include <string>

#include <WorkspacesLib/AppUtils.h>

namespace Utils
{
    class PwaHelper
    {
    public:
        PwaHelper();
        explicit PwaHelper(std::filesystem::path localAppData);
        ~PwaHelper() = default;

        std::optional<std::wstring> GetEdgeAppId(const std::wstring& windowAumid);
        std::optional<std::wstring> GetChromeAppId(const std::wstring& windowAumid);
        std::wstring SearchPwaName(const std::wstring& pwaAppId, const std::wstring& windowAumid) const;

    private:
        void InitEdgeAppIds();

        std::wstring GetAppIdFromCommandLineArgs(const std::wstring& commandLineArgs) const;

        std::filesystem::path m_localAppData;
        bool m_edgeInitialized{};
        std::map<std::wstring, std::wstring> m_edgeAppIds;
        std::map<std::wstring, std::wstring> m_pwaAppIdsToAppNames;
        // One helper spans the windows in a snapshot or arrangement operation.
        std::map<std::wstring, std::pair<std::wstring, std::wstring>> m_chromeAppsByAumid;
    };
}
