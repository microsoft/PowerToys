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
		};

		using AppList = std::vector<AppData>; // path; data

        AppList GetAppsList();
        std::optional<AppData> GetApp(const std::wstring& appPath, const AppList& apps);
	}
}