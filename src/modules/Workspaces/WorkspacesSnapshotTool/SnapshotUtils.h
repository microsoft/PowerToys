#pragma once

#include <WorkspacesLib/WorkspacesData.h>
#include <workspaces-common/GuidUtils.h>
#include <cwchar>
#include <set>
#include <stdexcept>

namespace SnapshotUtils
{
    inline void AssignUniqueApplicationIds(std::vector<WorkspacesData::WorkspacesProject::Application>& apps)
    {
        auto less = [](const std::wstring& left, const std::wstring& right) {
            return _wcsicmp(left.c_str(), right.c_str()) < 0;
        };
        std::set<std::wstring, decltype(less)> used(less);
        for (auto& app : apps)
        {
            // Window tags can survive previous launches; keep only the first use of an ID.
            while (app.id.empty() || !used.insert(app.id).second)
            {
                app.id = CreateGuidString();
                if (app.id.empty())
                {
                    throw std::runtime_error("Could not generate a capture application ID");
                }
            }
        }
    }

    std::vector<WorkspacesData::WorkspacesProject::Application> GetApps(bool isGuidNeeded, const std::function<unsigned int(HWND)> getMonitorNumberFromWindowHandle, const std::function<WorkspacesData::WorkspacesProject::Monitor::MonitorRect(unsigned int)> getMonitorRect);
};
