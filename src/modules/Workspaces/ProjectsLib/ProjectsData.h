#pragma once

#include <common/utils/json.h>

namespace ProjectsData
{
    std::wstring WorkspacesFile();
    std::wstring TempWorkspacesFile();
    std::wstring LaunchWorkspacesFile();

    struct Project
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

                inline bool operator==(const Position& other) const noexcept
                {
                    return x == other.x && y == other.y && width == other.width && height == other.height;
                }
            };

            std::wstring name;
            std::wstring title;
            std::wstring path;
            std::wstring packageFullName;
            std::wstring commandLineArgs;
            bool isElevated{};
            bool canLaunchElevated{};
            bool isMinimized{};
            bool isMaximized{};
            Position position{};
            unsigned int monitor{};
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

    struct ProjectsList
    {
        std::vector<Project> projects;
    };

    struct AppLaunchInfo
    {
        std::wstring name;
        std::wstring path;
        std::wstring state;
    };

    namespace AppLaunchInfoJSON
    {
        json::JsonObject ToJson(const AppLaunchInfo& data);
    }

    namespace AppLaunchInfoListJSON
    {
        json::JsonObject ToJson(const std::vector<AppLaunchInfo>& data);
    }

    struct AppLaunchData
    {
        std::vector<AppLaunchInfo> appLaunchInfoList;
        int launcherProcessID = 0;
    };

    namespace AppLaunchDataJSON
    {
        json::JsonObject ToJson(const AppLaunchData& data);
    }

    namespace ProjectJSON
    {
        namespace ApplicationJSON
        {
            namespace PositionJSON
            {
                json::JsonObject ToJson(const Project::Application::Position& position);
                std::optional<Project::Application::Position> FromJson(const json::JsonObject& json);
            }

            json::JsonObject ToJson(const Project::Application& data);
            std::optional<Project::Application> FromJson(const json::JsonObject& json);
        }

        namespace MonitorJSON
        {
            namespace MonitorRectJSON
            {
                json::JsonObject ToJson(const Project::Monitor::MonitorRect& data);
                std::optional<Project::Monitor::MonitorRect> FromJson(const json::JsonObject& json);
            }

            json::JsonObject ToJson(const Project::Monitor& data);
            std::optional<Project::Monitor> FromJson(const json::JsonObject& json);
        }

        json::JsonObject ToJson(const Project& data);
        std::optional<Project> FromJson(const json::JsonObject& json);
    }

    namespace ProjectsListJSON
    {
        json::JsonObject ToJson(const std::vector<Project>& data);
        std::optional<std::vector<Project>> FromJson(const json::JsonObject& json);
    }
};
