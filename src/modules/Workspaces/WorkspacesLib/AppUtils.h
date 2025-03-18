#pragma once

#include <WorkspacesLib/WorkspacesData.h>

namespace Utils
{
    namespace Apps
    {
        struct AppData
        {
            std::wstring name;
            std::wstring installPath;
            std::wstring packageFullName;
            std::wstring appUserModelId;
            std::wstring pwaAppId;
            bool canLaunchElevated = false;

            bool IsEdge() const;
            bool IsChrome() const;
        };

        using AppList = std::vector<AppData>;

        const std::wstring& GetCurrentFolder();
        const std::wstring& GetCurrentFolderUpper();

        AppList GetAppsList();
        std::optional<AppData> GetApp(const std::wstring& appPath, DWORD pid, const AppList& apps);
        std::optional<AppData> GetApp(HWND window, const AppList& apps);

        bool UpdateAppVersion(WorkspacesData::WorkspacesProject::Application& app, const AppList& installedApps);
        bool UpdateWorkspacesApps(WorkspacesData::WorkspacesProject& workspace, const AppList& installedApps);
    }
}