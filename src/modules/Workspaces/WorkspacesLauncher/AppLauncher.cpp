#include "pch.h"
#include "AppLauncher.h"

#include <filesystem>

#include <shellapi.h>

#include <winrt/Windows.Management.Deployment.h>
#include <winrt/Windows.ApplicationModel.Core.h>

#include <common/utils/winapi_error.h>

#include <WorkspacesLib/AppUtils.h>

#include <RegistryUtils.h>

using namespace winrt;
using namespace Windows::Foundation;
using namespace Windows::Management::Deployment;

namespace AppLauncher
{
    void UpdatePackagedApps(std::vector<WorkspacesData::WorkspacesProject::Application>& apps, const Utils::Apps::AppList& installedApps)
    {
        for (auto& app : apps)
        {
            // Packaged apps have version in the path, it will be outdated after update.
            // We need make sure the current package is up to date.
            if (!app.packageFullName.empty())
            {
                auto installedApp = std::find_if(installedApps.begin(), installedApps.end(), [&](const Utils::Apps::AppData& val) { return val.name == app.name; });
                if (installedApp != installedApps.end() && app.packageFullName != installedApp->packageFullName)
                {
                    std::wstring exeFileName = app.path.substr(app.path.find_last_of(L"\\") + 1);
                    app.packageFullName = installedApp->packageFullName;
                    app.path = installedApp->installPath + L"\\" + exeFileName;
                    Logger::trace(L"Updated package full name for {}: {}", app.name, app.packageFullName);
                }
            }
        }
    }

    bool AllWindowsFound(const LaunchingApps& launchedApps)
    {
        return std::find_if(launchedApps.begin(), launchedApps.end(), [&](const LaunchingApp& val) {
                   return val.window == nullptr;
               }) == launchedApps.end();
    };

    bool AddOpenedWindows(LaunchingApps& launchedApps, const std::vector<HWND>& windows, const Utils::Apps::AppList& installedApps)
    {
        bool statusChanged = false;
        for (HWND window : windows)
        {
            auto installedAppData = Utils::Apps::GetApp(window, installedApps);
            if (!installedAppData.has_value())
            {
                continue;
            }

            auto insertionIter = launchedApps.end();
            for (auto iter = launchedApps.begin(); iter != launchedApps.end(); ++iter)
            {
                if (iter->window == nullptr && (installedAppData.value().name == iter->application.name) || (installedAppData.value().installPath == iter->application.path))
                {
                    insertionIter = iter;
                }

                // keep the window at the same position if it's already opened
                WINDOWPLACEMENT placement{};
                ::GetWindowPlacement(window, &placement);
                HMONITOR monitor = MonitorFromWindow(window, MONITOR_DEFAULTTOPRIMARY);
                UINT dpi = DPIAware::DEFAULT_DPI;
                DPIAware::GetScreenDPIForMonitor(monitor, dpi);

                float x = static_cast<float>(placement.rcNormalPosition.left);
                float y = static_cast<float>(placement.rcNormalPosition.top);
                float width = static_cast<float>(placement.rcNormalPosition.right - placement.rcNormalPosition.left);
                float height = static_cast<float>(placement.rcNormalPosition.bottom - placement.rcNormalPosition.top);

                DPIAware::InverseConvert(monitor, x, y);
                DPIAware::InverseConvert(monitor, width, height);

                WorkspacesData::WorkspacesProject::Application::Position windowPosition{
                    .x = static_cast<int>(std::round(x)),
                    .y = static_cast<int>(std::round(y)),
                    .width = static_cast<int>(std::round(width)),
                    .height = static_cast<int>(std::round(height)),
                };
                if (iter->application.position == windowPosition)
                {
                    Logger::debug(L"{} window already found at {} {}.", iter->application.name, iter->application.position.x, iter->application.position.y);
                    insertionIter = iter;
                    break;
                }
            }

            if (insertionIter != launchedApps.end())
            {
                insertionIter->window = window;
                insertionIter->state = L"launched";
                statusChanged = true;
            }

            if (AllWindowsFound(launchedApps))
            {
                break;
            }
        }
        return statusChanged;
    }
}

bool LaunchApp(const std::wstring& appPath, const std::wstring& commandLineArgs, bool elevated, ErrorList& launchErrors)
{
    std::wstring dir = std::filesystem::path(appPath).parent_path();

        SHELLEXECUTEINFO sei = { 0 };
        sei.cbSize = sizeof(SHELLEXECUTEINFO);
        sei.hwnd = nullptr;
        sei.fMask = SEE_MASK_NOCLOSEPROCESS | SEE_MASK_NO_CONSOLE;
        sei.lpVerb = elevated ? L"runas" : L"open";
        sei.lpFile = appPath.c_str();
        sei.lpParameters = commandLineArgs.c_str();
        sei.lpDirectory = dir.c_str();
        sei.nShow = SW_SHOWNORMAL;

        if (!ShellExecuteEx(&sei))
        {
            std::wstring error = get_last_error_or_default(GetLastError());
            Logger::error(L"Failed to launch process. {}", error);
            return Error(error);
        }

        return Ok(sei);
    }

    bool LaunchPackagedApp(const std::wstring& packageFullName, ErrorList& launchErrors)
    {
        try
        {
            PackageManager packageManager;
            for (const auto& package : packageManager.FindPackagesForUser({}))
            {
                if (package.Id().FullName() == packageFullName)
                {
                    auto getAppListEntriesOperation = package.GetAppListEntriesAsync();
                    auto appEntries = getAppListEntriesOperation.get();

                    if (appEntries.Size() > 0)
                    {
                        IAsyncOperation<bool> launchOperation = appEntries.GetAt(0).LaunchAsync();
                        bool launchResult = launchOperation.get();
                        return launchResult;
                    }
                    else
                    {
                        Logger::error(L"No app entries found for the package.");
                        launchErrors.push_back({ packageFullName, L"No app entries found for the package." });
                    }
                }
            }
        }
        catch (const hresult_error& ex)
        {
            Logger::error(L"Packaged app launching error: {}", ex.message());
            launchErrors.push_back({ packageFullName, ex.message().c_str() });
        }

        return false;
    }

    bool Launch(const WorkspacesData::WorkspacesProject::Application& app, ErrorList& launchErrors)
    {
        bool launched{ false };

        // packaged apps: check protocol in registry
        // usage example: Settings with cmd args
        if (!app.packageFullName.empty())
        {
            auto names = RegistryUtils::GetUriProtocolNames(app.packageFullName);
            if (!names.empty())
            {
                Logger::trace(L"Launching packaged by protocol with command line args {}", app.name);

                std::wstring uriProtocolName = names[0];
                std::wstring command = std::wstring(uriProtocolName + (app.commandLineArgs.starts_with(L":") ? L"" : L":") + app.commandLineArgs);

                auto res = LaunchApp(command, L"", app.isElevated);
                if (res.isOk())
                {
                    launched = true;
                }
                else
                {
                    launchErrors.push_back({ std::filesystem::path(app.path).filename(), res.error() });
                }
            }
            else
            {
                Logger::info(L"Uri protocol names not found for {}", app.packageFullName);
            }
        }

        // packaged apps: try launching first by AppUserModel.ID
        // usage example: elevated Terminal
        if (!launched && !app.appUserModelId.empty() && !app.packageFullName.empty())
        {
            Logger::trace(L"Launching {} as {}", app.name, app.appUserModelId);
            auto res = LaunchApp(L"shell:AppsFolder\\" + app.appUserModelId, app.commandLineArgs, app.isElevated);
            if (res.isOk())
            {
                launched = true;
            }
            else
            {
                launchErrors.push_back({ std::filesystem::path(app.path).filename(), res.error() });
            }
        }

        // packaged apps: try launching by package full name
        // doesn't work for elevated apps or apps with command line args
        if (!launched && !app.packageFullName.empty() && app.commandLineArgs.empty() && !app.isElevated)
        {
            Logger::trace(L"Launching packaged app {}", app.name);
            launched = LaunchPackagedApp(app.packageFullName, launchErrors);
        }

        if (!launched)
        {
            Logger::trace(L"Launching {} at {}", app.name, app.path);

            DWORD dwAttrib = GetFileAttributesW(app.path.c_str());
            if (dwAttrib == INVALID_FILE_ATTRIBUTES)
            {
                Logger::error(L"File not found at {}", app.path);
                launchErrors.push_back({ std::filesystem::path(app.path).filename(), L"File not found" });
                return false;
            }

            auto res = LaunchApp(app.path, app.commandLineArgs, app.isElevated);
            if (res.isOk())
            {
                launched = true;
            }
            else
            {
                launchErrors.push_back({ std::filesystem::path(app.path).filename(), res.error() });
            }
        }

        Logger::trace(L"{} {} at {}", app.name, (launched ? L"launched" : L"not launched"), app.path);
        return launched;
    }

    bool Launch(WorkspacesData::WorkspacesProject& project, LaunchingStatus& launchingStatus, ErrorList& launchErrors)
    {
        bool launchedSuccessfully{ true };

        auto installedApps = Utils::Apps::GetAppsList();
        UpdatePackagedApps(project.apps, installedApps);

        // Launch apps
        for (auto& app : project.apps)
        {
            if (!Launch(app, launchErrors))
            {
                Logger::error(L"Failed to launch {}", app.name);
                launchingStatus.Update(app, LaunchingState::Failed);
                launchedSuccessfully = false;
            }
            else
            {
                launchingStatus.Update(app, LaunchingState::Launched);
            }
        }

        return launchedSuccessfully;
    }
}