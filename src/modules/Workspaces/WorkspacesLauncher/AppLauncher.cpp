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
        const std::wstring SteamProtocolPrefix = L"steam:";
    }

    Result<SHELLEXECUTEINFO, LaunchError> LaunchApp(const std::wstring& appPath, const std::wstring& commandLineArgs, bool elevated)
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
            const DWORD code = GetLastError();
            std::wstring error = get_last_error_or_default(code);
            Logger::error(L"Failed to launch process. {}", error);
            return Error(LaunchError{ code, std::move(error) });
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

    LaunchResult Launch(const WorkspacesData::WorkspacesProject::Application& app, ErrorList& launchErrors, const ApprovalCallback& requestApproval, const std::function<bool()>& isCanceled)
    {
        bool launched{ false };
        std::optional<LaunchResult> terminalResult;
        const auto stop = [&](LaunchDecision decision) -> Result<SHELLEXECUTEINFO, LaunchError> {
            if (decision == LaunchDecision::Skipped || decision == LaunchDecision::Canceled)
            {
                terminalResult = decision == LaunchDecision::Skipped ? LaunchResult::Skipped : LaunchResult::Canceled;
                return Error(LaunchError{ ERROR_CANCELLED, L"Launch skipped or canceled." });
            }
            terminalResult = LaunchResult::Failed;
            const auto reason = decision == LaunchDecision::TargetChanged ? L"Launch target changed after verification." :
                                decision == LaunchDecision::TimedOut ? L"Elevation confirmation could not be displayed in time." :
                                decision == LaunchDecision::InvalidResponse ? L"Invalid elevation confirmation response." :
                                L"Elevation confirmation UI is unavailable.";
            Logger::error(L"Elevated launch stopped: {}", reason);
            launchErrors.push_back({ std::filesystem::path(app.path).filename(), reason });
            return Error(LaunchError{ ERROR_NOT_READY, reason });
        };
        if (isCanceled())
        {
            return LaunchResult::Canceled;
        }
        const auto execute = [&](const std::wstring& path, const std::wstring& arguments) {
            auto result = LaunchApp(path, arguments, app.isElevated);
            if (result.isError() && result.error().code == ERROR_CANCELLED)
            {
                terminalResult = LaunchResult::Canceled;
            }
            if (result.isOk() && result.value().hProcess)
            {
                auto value = result.value();
                CloseHandle(value.hProcess);
                value.hProcess = nullptr;
                return Result<SHELLEXECUTEINFO, LaunchError>(Ok(value));
            }
            return result;
        };
        const auto launch = [&](const std::wstring& path, const std::wstring& arguments) -> Result<SHELLEXECUTEINFO, LaunchError> {
            if (terminalResult || isCanceled())
            {
                return stop(LaunchDecision::Canceled);
            }
            if (!app.isElevated)
            {
                return execute(path, arguments);
            }

            auto target = SignatureVerification::Verify(path, isCanceled);
            if (isCanceled())
            {
                return stop(LaunchDecision::Canceled);
            }
            if (!target.result.IsVerified())
            {
                const auto decision = requestApproval(target.path, arguments, target.result);
                if (isCanceled())
                {
                    return stop(LaunchDecision::Canceled);
                }
                if (decision != LaunchDecision::Approved)
                {
                    return stop(decision);
                }
            }
            if (!SignatureVerification::IsCurrent(target, isCanceled))
            {
                return stop(isCanceled() ? LaunchDecision::Canceled : LaunchDecision::TargetChanged);
            }
            if (isCanceled())
            {
                return stop(LaunchDecision::Canceled);
            }

            // Direct file targets remain locked until ShellExecuteEx finishes the elevation request.
            return execute(target.path, arguments);
        };

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

                auto res = launch(command, L"");
                if (res.isOk())
                {
                    launched = true;
                }
                else if (!terminalResult)
                {
                    launchErrors.push_back({ std::filesystem::path(app.path).filename(), res.error().message });
                }
            }
            else
            {
                Logger::info(L"Uri protocol names not found for {}", app.packageFullName);
            }
        }

        // packaged apps: try launching first by AppUserModel.ID
        // usage example: elevated Terminal
        if (!launched && !terminalResult && !app.appUserModelId.empty() && !app.packageFullName.empty())
        {
            Logger::trace(L"Launching {} as {} - {app.packageFullName}", app.name, app.appUserModelId, app.packageFullName);
            auto res = launch(L"shell:AppsFolder\\" + app.appUserModelId, app.commandLineArgs);
            if (res.isOk())
            {
                launched = true;
            }
            else if (!terminalResult)
            {
                launchErrors.push_back({ std::filesystem::path(app.path).filename(), res.error().message });
            }
        }

        // protocol launch for steam
        if (!launched && !terminalResult && !app.appUserModelId.empty() && app.appUserModelId.contains(NonLocalizable::SteamProtocolPrefix))
        {
            Logger::trace(L"Launching {} as {}", app.name, app.appUserModelId);
            auto res = launch(app.appUserModelId, app.commandLineArgs);
            if (res.isOk())
            {
                launched = true;
            }
            else if (!terminalResult)
            {
                launchErrors.push_back({ std::filesystem::path(app.path).filename(), res.error().message });
            }
        }

        // packaged apps: try launching by package full name
        // doesn't work for elevated apps or apps with command line args
        if (!launched && !terminalResult && !app.packageFullName.empty() && app.commandLineArgs.empty() && !app.isElevated && !isCanceled())
        {
            Logger::trace(L"Launching packaged app {}", app.name);
            launched = LaunchPackagedApp(app.packageFullName, launchErrors);
        }

        std::wstring appPathFinal;
        std::wstring commandLineArgsFinal;
        appPathFinal = app.path;
        commandLineArgsFinal = app.commandLineArgs;

        if (!launched && !terminalResult && !app.pwaAppId.empty())
        {
            int version = 0;

            if (app.version != L"")
            {
                try
                {
                    version = std::stoi(app.version);
                }
                catch (const std::invalid_argument&)
                {
                    Logger::error(L"Invalid version format: {}", app.version);
                    version = 0;
                }
                catch (const std::out_of_range&)
                {
                    Logger::error(L"Version out of range: {}", app.version);
                    version = 0;
                }
            }

            if (version >= 1)
            {
                auto res = launch(L"shell:AppsFolder\\" + app.appUserModelId, app.commandLineArgs);
                if (res.isOk())
                {
                    launched = true;
                }
                else if (!terminalResult)
                {
                    launchErrors.push_back({ app.appUserModelId, res.error().message });
                }
            }

            if (!launched && !terminalResult)
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
        }

        if (!launched && !terminalResult)
        {
            Logger::trace(L"Launching {} at {}", app.name, appPathFinal);

            DWORD dwAttrib = GetFileAttributesW(appPathFinal.c_str());
            if (dwAttrib == INVALID_FILE_ATTRIBUTES)
            {
                Logger::error(L"File not found at {}", appPathFinal);
                launchErrors.push_back({ std::filesystem::path(appPathFinal).filename(), L"File not found" });
                return LaunchResult::Failed;
            }

            auto res = launch(appPathFinal, commandLineArgsFinal);
            if (res.isOk())
            {
                launched = true;
            }
            else if (!terminalResult)
            {
                launchErrors.push_back({ std::filesystem::path(appPathFinal).filename(), res.error().message });
            }
        }

        Logger::trace(L"{} {} at {}", app.name, (launched ? L"launched" : L"not launched"), appPathFinal);
        return terminalResult.value_or(launched ? LaunchResult::Launched : LaunchResult::Failed);
    }
}