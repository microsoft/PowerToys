#pragma once

#include <shared_mutex>

#include <WorkspacesLib/WorkspacesData.h>

class LaunchingStatus
{
public:
    LaunchingStatus(const WorkspacesData::WorkspacesProject& project);
    ~LaunchingStatus() = default;

    bool AllLaunchedAndMoved() noexcept;
    bool AllLaunched() noexcept;
    const WorkspacesData::LaunchingAppStateMap& Get() noexcept;

    void Update(const WorkspacesData::WorkspacesProject::Application& app, LaunchingState state);
    
private:
    WorkspacesData::LaunchingAppStateMap m_appsState;
    std::shared_mutex m_mutex;
};
