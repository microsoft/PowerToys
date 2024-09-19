#pragma once

#include <functional>
#include <shared_mutex>

#include <WorkspacesLib/WorkspacesData.h>

class LaunchingStatus
{
public:
    LaunchingStatus(const WorkspacesData::WorkspacesProject& project, std::function<void(const WorkspacesData::LaunchingAppStateMap&)> updateCallback);
    ~LaunchingStatus() = default;

    bool Ready() noexcept;

    void Update(const WorkspacesData::WorkspacesProject::Application& app, LaunchingState state);
    
private:
    WorkspacesData::LaunchingAppStateMap m_appsState;
    std::function<void(const WorkspacesData::LaunchingAppStateMap&)> m_updateCallback;
    std::shared_mutex m_mutex;
};
