#include "pch.h"
#include "LaunchingStatus.h"

#include <common/logger/logger.h>

LaunchingStatus::LaunchingStatus(const WorkspacesData::WorkspacesProject& project)
{
    std::unique_lock lock(m_mutex);
    for (const auto& app : project.apps)
    {
        m_appsState.insert({ app, { app, nullptr, LaunchingState::Waiting } });
    }
}

bool LaunchingStatus::AllLaunched() noexcept
{
    std::shared_lock lock(m_mutex);
    for (const auto& [app, data] : m_appsState)
    {
        if (data.state == LaunchingState::Waiting)
        {
            return false;
        }
    }

    return true;
}

bool LaunchingStatus::AllLaunchedAndMoved() noexcept
{
    std::shared_lock lock(m_mutex);
    for (const auto& [app, data] : m_appsState)
    {
        if (data.state != LaunchingState::Failed && 
            data.state != LaunchingState::Canceled && 
            data.state != LaunchingState::LaunchedAndMoved)
        {
            return false;
        }
    }

    return true;
}

bool LaunchingStatus::AllInstancesOfTheAppLaunchedAndMoved(const WorkspacesData::WorkspacesProject::Application& application) noexcept
{
    std::shared_lock lock(m_mutex);

    for (const auto& [app, state] : m_appsState)
    {
        if (app.name == application.name || app.path == application.path)
        {
            if (state.state == LaunchingState::Launched)
            {
                return false;
            }
        }
    }

    return true;
}

const WorkspacesData::LaunchingAppStateMap& LaunchingStatus::Get() noexcept
{
    std::shared_lock lock(m_mutex);
    return m_appsState;
}

std::optional<WorkspacesData::LaunchingAppState> LaunchingStatus::Get(const WorkspacesData::WorkspacesProject::Application& app) noexcept
{
    std::shared_lock lock(m_mutex);
    if (m_appsState.contains(app))
    {
        return m_appsState.at(app);
    }

    return std::nullopt;
}

std::optional<WorkspacesData::LaunchingAppState> LaunchingStatus::GetNext(LaunchingState state) noexcept
{
    std::shared_lock lock(m_mutex);
    for (const auto& [app, appState] : m_appsState)
    {
        if (appState.state == state)
        {
            return appState;
        }
    }

    return std::nullopt;
}

bool LaunchingStatus::IsWindowProcessed(HWND window) noexcept
{
    std::shared_lock lock(m_mutex);

    for (const auto& [app, state] : m_appsState)
    {
        if (state.window == window)
        {
            return true;
        }
    }

    return false;
}

void LaunchingStatus::Update(const WorkspacesData::WorkspacesProject::Application& app, LaunchingState state)
{
    std::unique_lock lock(m_mutex);
    if (!m_appsState.contains(app))
    {
        Logger::error(L"Error updating state: app {} is not tracked in the project", app.name);
        return;
    }

    m_appsState[app].state = state;
}

void LaunchingStatus::Update(const WorkspacesData::WorkspacesProject::Application& app, HWND window, LaunchingState state)
{
    std::unique_lock lock(m_mutex);
    if (!m_appsState.contains(app))
    {
        Logger::error(L"Error updating state: app {} is not tracked in the project", app.name);
        return;
    }

    m_appsState[app].state = state;
    m_appsState[app].window = window;
}

void LaunchingStatus::Cancel()
{
    std::unique_lock lock(m_mutex);
    for (auto& [app, state] : m_appsState)
    {
        if (state.state == LaunchingState::Waiting)
        {
            state.state = LaunchingState::Canceled;
        }
    }
}