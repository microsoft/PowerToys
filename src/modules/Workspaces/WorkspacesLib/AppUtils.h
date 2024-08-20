#pragma once

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
            bool canLaunchElevated = false;
        };

        using AppList = std::vector<AppData>;

        const std::wstring& GetCurrentFolder();
        const std::wstring& GetCurrentFolderUpper();

        AppList GetAppsList();
        std::optional<AppData> GetApp(const std::wstring& appPath, const AppList& apps);
        std::optional<AppData> GetApp(HWND window, const AppList& apps);
    }
}