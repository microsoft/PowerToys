#include "pch.h"
#include "AppLauncher.h"

#include <filesystem>

#include <shellapi.h>

#include <winrt/Windows.Management.Deployment.h>
#include <winrt/Windows.ApplicationModel.Core.h>

#include <common/utils/winapi_error.h>

#include <RegistryUtils.h>

using namespace winrt;
using namespace Windows::Foundation;
using namespace Windows::Management::Deployment;

namespace AppLauncher
{
    namespace NonLocalizable
    {
        const std::wstring EdgeFilename = L"msedge.exe";
        const std::wstring EdgePwaFilename = L"msedge_proxy.exe";
        const std::wstring ChromeFilename = L"chrome.exe";
        const std::wstring ChromePwaFilename = L"chrome_proxy.exe";
        const std::wstring PwaCommandLineAddition = L"--profile-directory=Default --app-id=";
    }

    Result<SHELLEXECUTEINFO, std::wstring> LaunchApp(const std::wstring& appPath, const std::wstring& commandLineArgs, bool elevated)
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

        std::wstring appPathFinal;
        std::wstring commandLineArgsFinal;
        appPathFinal = app.path;
        commandLineArgsFinal = app.commandLineArgs;

        if (!launched && !app.pwaAppId.empty())
        {
            std::filesystem::path appPath(app.path);
            if (appPath.filename() == NonLocalizable::EdgeFilename)
            {
                appPathFinal = appPath.parent_path() / NonLocalizable::EdgePwaFilename;
                commandLineArgsFinal = NonLocalizable::PwaCommandLineAddition + app.pwaAppId + L" " + app.commandLineArgs;
            }
            if (appPath.filename() == NonLocalizable::ChromeFilename)
            {
                appPathFinal = appPath.parent_path() / NonLocalizable::ChromePwaFilename;
                commandLineArgsFinal = NonLocalizable::PwaCommandLineAddition + app.pwaAppId + L" " + app.commandLineArgs;
            }
        }

        if (!launched)
        {
            Logger::trace(L"Launching {} at {}", app.name, appPathFinal);

            DWORD dwAttrib = GetFileAttributesW(appPathFinal.c_str());
            if (dwAttrib == INVALID_FILE_ATTRIBUTES)
            {
                Logger::error(L"File not found at {}", appPathFinal);
                launchErrors.push_back({ std::filesystem::path(appPathFinal).filename(), L"File not found" });
                return false;
            }

            auto res = LaunchApp(appPathFinal, commandLineArgsFinal, app.isElevated);
            if (res.isOk())
            {
                launched = true;
            }
            else
            {
                launchErrors.push_back({ std::filesystem::path(appPathFinal).filename(), res.error() });
            }
        }

        Logger::trace(L"{} {} at {}", app.name, (launched ? L"launched" : L"not launched"), appPathFinal);
        return launched;
    }
}