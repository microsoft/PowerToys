#pragma once

#include <functional>

#include <WorkspacesLib/WorkspacesData.h>

class LaunchingStatus
{
public:
    LaunchingStatus(const WorkspacesData::WorkspacesProject& project, std::function<void(const WorkspacesData::LaunchingAppStateMap&)> updateCallback);
    ~LaunchingStatus() = default;

    const WorkspacesData::LaunchingAppStateMap& Get() const noexcept;

    void Update(const WorkspacesData::WorkspacesProject::Application& app, LaunchingState state);
    
private:
    WorkspacesData::LaunchingAppStateMap m_appsState;
    std::function<void(const WorkspacesData::LaunchingAppStateMap&)> m_updateCallback;
};
