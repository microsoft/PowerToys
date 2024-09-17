#include "pch.h"
#include "LaunchingStatus.h"

#include <common/logger/logger.h>

LaunchingStatus::LaunchingStatus(const WorkspacesData::WorkspacesProject& project, std::function<void(const WorkspacesData::LaunchingAppStateMap&)> updateCallback) :
    m_updateCallback(updateCallback)
{
    for (const auto& app : project.apps)
    {
        m_appsState.insert({ app, { app, nullptr, LaunchingState::Waiting } });
    }

    if (m_updateCallback)
    {
        m_updateCallback(Get());
    }
}

const WorkspacesData::LaunchingAppStateMap& LaunchingStatus::Get() const noexcept
{
    return m_appsState;
}

void LaunchingStatus::Update(const WorkspacesData::WorkspacesProject::Application& app, LaunchingState state)
{
    if (!m_appsState.contains(app))
    {
        Logger::error(L"Error updating state: app {} is not tracked in the project", app.name);
        return;
    }

    m_appsState[app].state = state;

    if (m_updateCallback)
    {
        m_updateCallback(Get());
    }
}
