#include "pch.h"

#include <common/utils/elevation.h>
#include <common/utils/owner_process.h>
#include <common/utils/gpo.h>
#include <common/utils/logger_helper.h>
#include <common/utils/process_path.h>
#include <common/utils/UnhandledExceptionHandler.h>
#include <common/utils/resources.h>
#include <common/Telemetry/EtwTrace/EtwTrace.h>
#include <WorkspacesLib/WorkspacesRepository.h>
#include <WorkspacesLib/utils.h>
#include <WorkspacesLib/AppUtils.h>
#include <WorkspacesLib/trace.h>
#include <Launcher.h>
#include <Generated Files/resource.h>

const std::wstring moduleName = L"Workspaces\\WorkspacesLauncher";
const std::wstring internalPath = L"";
const std::wstring instanceMutexName = L"Local\\PowerToys_WorkspacesLauncher_InstanceMutex";

namespace
{
    bool RetryStorage()
    {
        return MessageBoxW(nullptr, GET_RESOURCE_STRING(IDS_PROTECTED_STORAGE_ERROR).c_str(),
            GET_RESOURCE_STRING(IDS_WORKSPACES).c_str(), MB_ICONERROR | MB_RETRYCANCEL) == IDRETRY;
    }
}

int APIENTRY WinMain(HINSTANCE, HINSTANCE, LPSTR, int)
{
    LoggerHelpers::init_logger(moduleName, internalPath, LogSettings::workspacesLauncherLoggerName);
    InitUnhandledExceptionHandler();
    if (powertoys_gpo::getConfiguredWorkspacesEnabledValue() == powertoys_gpo::gpo_rule_configured_disabled) return 0;
    auto args = split(GetCommandLineW(), L" ");
    if (args.workspaceId.empty())
    {
        MessageBoxW(nullptr, GET_RESOURCE_STRING(IDS_INCORRECT_ARGS).c_str(), GET_RESOURCE_STRING(IDS_WORKSPACES).c_str(), MB_ICONERROR | MB_OK);
        return 1;
    }
    if (is_process_elevated())
    {
        const auto command = args.workspaceId + L" " + std::to_wstring(args.invokePoint) + L" " + NonLocalizable::restartedString +
            (args.previewId.empty() ? L"" : L" --preview=" + PowerToys::ProtectedStorage::Wide(args.previewId));
        wil::unique_handle child;
        const auto result = owner_process::Start(PowerToys::ProtectedStorage::ModulePath(), command, get_module_folderpath(), child);
        if (result != ERROR_SUCCESS)
        {
            Logger::error("Workspaces launcher could not obtain its bound ordinary owner context: {}", result);
            return static_cast<int>(result);
        }
        DWORD exitCode = ERROR_GEN_FAILURE;
        if (WaitForSingleObject(child.get(), INFINITE) != WAIT_OBJECT_0 || !GetExitCodeProcess(child.get(), &exitCode))
        {
            Logger::error("Workspaces owner launcher outcome could not be observed: {}", GetLastError());
            return 1;
        }
        return static_cast<int>(exitCode);
    }
    PowerToys::ProtectedStorage::Handle mutex(CreateMutexW(nullptr, TRUE, instanceMutexName.c_str()));
    if (!mutex || GetLastError() == ERROR_ALREADY_EXISTS) return 1;
    if (FAILED(CoInitializeEx(nullptr, COINIT_MULTITHREADED))) return 1;
    SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
    Workspaces::Repository repository;
    WorkspacesData::WorkspacesProject project;
    for (;;)
    {
        try
        {
            if (args.invokePoint == InvokePoint::LaunchAndEdit)
            {
                PowerToys::ProtectedStorage::Check(!args.previewId.empty(), PowerToys::ProtectedStorage::ErrorCode::InvalidPayload);
                project = repository.ReadPreview(args.previewId);
            }
            else
            {
                auto snapshot = repository.Load();
                const auto found = std::find_if(snapshot.projects.begin(), snapshot.projects.end(),
                    [&](const auto& value) { return Workspaces::Repository::SameIdentity(value.id, args.workspaceId); });
                PowerToys::ProtectedStorage::Check(found != snapshot.projects.end(), PowerToys::ProtectedStorage::ErrorCode::NotFound);
                project = *found;
            }
            PowerToys::ProtectedStorage::Check(Workspaces::Repository::SameIdentity(project.id, args.workspaceId), PowerToys::ProtectedStorage::ErrorCode::InvalidPayload);
            auto original = project;
            if (Utils::Apps::UpdateWorkspacesApps(project, Utils::Apps::GetAppsList()) && args.invokePoint != InvokePoint::LaunchAndEdit)
            {
                repository.UpdateLaunchMetadata(original, project);
            }
            Workspaces::Repository::Validate({project}, args.invokePoint == InvokePoint::LaunchAndEdit);
            break;
        }
        catch (...)
        {
            Logger::error("Protected Workspaces read or metadata update failed");
            if (!RetryStorage()) { CoUninitialize(); return 1; }
        }
    }
    Trace::Workspaces::RegisterProvider();
    Shared::Trace::ETWTrace trace{};
    trace.UpdateState(true);
    try
    {
        Launcher launcher(project, args.invokePoint);
    }
    catch (...)
    {
        Logger::error("Owner-bound Workspaces launch session failed");
        MessageBoxW(nullptr, GET_RESOURCE_STRING(IDS_PROTECTED_STORAGE_ERROR).c_str(), GET_RESOURCE_STRING(IDS_WORKSPACES).c_str(), MB_ICONERROR | MB_OK);
        CoUninitialize();
        return 1;
    }
    if (args.invokePoint != InvokePoint::LaunchAndEdit)
    {
        const auto launched = std::chrono::system_clock::to_time_t(std::chrono::system_clock::now());
        for (;;)
        {
            try
            {
                repository.UpdateLaunchMetadata(project, project, launched);
                break;
            }
            catch (...)
            {
                Logger::error("Recording the completed Workspaces launch failed");
                // Retry only the metadata operation; never launch the applications again.
                if (!RetryStorage()) { CoUninitialize(); return 1; }
            }
        }
    }
    trace.Flush();
    trace.UpdateState(false);
    Trace::Workspaces::UnregisterProvider();
    CoUninitialize();
    return 0;
}
