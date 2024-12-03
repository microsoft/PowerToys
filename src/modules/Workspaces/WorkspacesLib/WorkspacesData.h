#pragma once

#include <common/utils/json.h>

#include <WorkspacesLib/LaunchingStateEnum.h>

namespace WorkspacesData
{
    std::wstring WorkspacesFile();
    std::wstring TempWorkspacesFile();

    struct WorkspacesProject
    {
        struct Application
        {
            struct Position
            {
                int x;
                int y;
                int width;
                int height;

                RECT toRect() const noexcept;

                auto operator<=>(const Position&) const = default;
            };

            std::wstring id;
            std::wstring name;
            std::wstring title;
            std::wstring path;
            std::wstring packageFullName;
            std::wstring appUserModelId;
            std::wstring pwaAppId;
            std::wstring commandLineArgs;
            bool isElevated{};
            bool canLaunchElevated{};
            bool isMinimized{};
            bool isMaximized{};
            Position position{};
            unsigned int monitor{};

            auto operator<=>(const Application&) const = default;
        };

        struct Monitor
        {
            struct MonitorRect
            {
                int top;
                int left;
                int width;
                int height;

                inline bool operator==(const MonitorRect& other) const noexcept
                {
                    return top == other.top && left == other.left && width == other.width && height == other.height;
                }
            };

            HMONITOR monitor{};
            std::wstring id;
            std::wstring instanceId;
            unsigned int number{};
            unsigned int dpi{};
            MonitorRect monitorRectDpiAware{};
            MonitorRect monitorRectDpiUnaware{};
        };

        std::wstring id;
        std::wstring name;
        time_t creationTime;
        std::optional<time_t> lastLaunchedTime;
        bool isShortcutNeeded;
        bool moveExistingWindows;
        std::vector<Monitor> monitors;
        std::vector<Application> apps;
    };

    struct WorkspacesList
    {
        std::vector<WorkspacesProject> projects;
    };

    struct LaunchingAppState
    {
        WorkspacesData::WorkspacesProject::Application application;
        HWND window{};
        LaunchingState state { LaunchingState::Waiting };
    };

    using LaunchingAppStateMap = std::map<WorkspacesData::WorkspacesProject::Application, LaunchingAppState>;
    using LaunchingAppStateList = std::vector<std::pair<WorkspacesData::WorkspacesProject::Application, LaunchingState>>;

    struct AppLaunchData
    {
        LaunchingAppStateMap appsStateList;
        int launcherProcessID = 0;
    };

    namespace WorkspacesProjectJSON
    {
        namespace ApplicationJSON
        {
            namespace PositionJSON
            {
                json::JsonObject ToJson(const WorkspacesProject::Application::Position& position);
                std::optional<WorkspacesProject::Application::Position> FromJson(const json::JsonObject& json);
            }

            json::JsonObject ToJson(const WorkspacesProject::Application& data);
            std::optional<WorkspacesProject::Application> FromJson(const json::JsonObject& json);
        }

        namespace MonitorJSON
        {
            namespace MonitorRectJSON
            {
                json::JsonObject ToJson(const WorkspacesProject::Monitor::MonitorRect& data);
                std::optional<WorkspacesProject::Monitor::MonitorRect> FromJson(const json::JsonObject& json);
            }

            json::JsonObject ToJson(const WorkspacesProject::Monitor& data);
            std::optional<WorkspacesProject::Monitor> FromJson(const json::JsonObject& json);
        }

        json::JsonObject ToJson(const WorkspacesProject& data);
        std::optional<WorkspacesProject> FromJson(const json::JsonObject& json);
    }

    namespace WorkspacesListJSON
    {
        json::JsonObject ToJson(const std::vector<WorkspacesProject>& data);
        std::optional<std::vector<WorkspacesProject>> FromJson(const json::JsonObject& json);
    }

    namespace AppLaunchInfoJSON
    {
        json::JsonObject ToJson(const LaunchingAppState& data);
        std::optional<LaunchingAppState> FromJson(const json::JsonObject& json);
    }

    namespace AppLaunchInfoListJSON
    {
        json::JsonObject ToJson(const LaunchingAppStateMap& data);
        std::optional<LaunchingAppStateMap> FromJson(const json::JsonObject& json);
    }

    namespace AppLaunchDataJSON
    {
        json::JsonObject ToJson(const AppLaunchData& data);
    }

};
