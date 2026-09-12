#include "pch.h"

#include <common/utils/elevation.h>
#include <common/utils/gpo.h>
#include <common/utils/logger_helper.h>
#include <common/utils/process_path.h>
#include <common/utils/UnhandledExceptionHandler.h>
#include <common/utils/resources.h>

#include <common/Telemetry/EtwTrace/EtwTrace.h>

#include <WorkspacesLib/JsonUtils.h>
#include <WorkspacesLib/utils.h>

#include <Launcher.h>

#include <Generated Files/resource.h>
#include <WorkspacesLib/AppUtils.h>
#include <WorkspacesLib/trace.h>
#include <WorkspacesLib/SignatureVerification.h>
#ifdef _DEBUG
#include <WorkspacesLib/LauncherUiMessage.h>
#endif

const std::wstring moduleName = L"Workspaces\\WorkspacesLauncher";
const std::wstring internalPath = L"";
const std::wstring instanceMutexName = L"Local\\PowerToys_WorkspacesLauncher_InstanceMutex";

#ifdef _DEBUG
namespace
{
    int PreviewSignatureWarning(const SignatureVerification::LaunchTarget& target)
    {
        PendingLaunchApproval approval;
        std::atomic<bool> failed{};
        std::unique_ptr<LauncherUIHelper> ui;
        ui = std::make_unique<LauncherUIHelper>([&](const std::wstring& text) {
            const auto message = LauncherUiMessage::Parse(text);
            if (!message)
            {
                failed = true;
                approval.Fail(LaunchDecision::InvalidResponse);
                return;
            }
            switch (message->type)
            {
            case LauncherUiMessage::Type::Ready:
                if (!ui->MarkReady())
                {
                    failed = true;
                    approval.Fail(LaunchDecision::InvalidResponse);
                }
                break;
            case LauncherUiMessage::Type::Cancel:
                approval.Cancel();
                break;
            case LauncherUiMessage::Type::WarningShown:
                approval.Acknowledge(message->requestId);
                break;
            case LauncherUiMessage::Type::Heartbeat:
                approval.Heartbeat(message->requestId);
                break;
            case LauncherUiMessage::Type::Response:
                approval.Complete(message->requestId, message->decision);
                break;
            }
        }, [&](LauncherIpcFailure) {
            failed = true;
            approval.Fail(LaunchDecision::UiUnavailable);
        });
        if (!ui->LaunchUI() || !ui->WaitForReady() || failed)
        {
            Logger::error(L"Unable to start the authenticated signature-warning preview");
            return 1;
        }
        WorkspacesData::WorkspacesProject::Application app{};
        app.id = CreateGuidString();
        app.name = L"Signature warning preview";
        app.path = target.path;
        app.isElevated = true;
        ui->UpdateLaunchStatus({ { app, { app, nullptr, LaunchingState::Waiting } } });
        const auto requestId = CreateGuidString();
        if (!approval.Begin(requestId) || failed ||
            !ui->RequestApproval(requestId, app.name, target.path, L"", target.result))
        {
            Logger::error(L"Unable to send the signature-warning preview");
            return 1;
        }
        std::optional<LaunchDecision> decision;
        while (!(decision = approval.WaitFor(std::chrono::milliseconds(250))))
        {
            if (failed || !ui->IsReady())
            {
                approval.Fail(LaunchDecision::UiUnavailable);
            }
        }
        ui->DismissApproval(requestId);
        const char* choice = decision == LaunchDecision::Approved ? "run" :
                             decision == LaunchDecision::Skipped ? "skip" :
                             decision == LaunchDecision::Canceled ? "cancel" : "failed";
        const std::string output = std::string("{\"previewOnly\":true,\"choice\":\"") + choice + "\"}\n";
        DWORD written = 0;
        if (!WriteFile(GetStdHandle(STD_OUTPUT_HANDLE), output.data(), static_cast<DWORD>(output.size()), &written, nullptr) ||
            written != output.size())
        {
            Logger::error(L"Unable to write the signature-warning preview result");
            return 1;
        }
        return decision == LaunchDecision::Approved || decision == LaunchDecision::Skipped || decision == LaunchDecision::Canceled ? 0 : 1;
    }
}
#endif

int APIENTRY WinMain(HINSTANCE hInst, HINSTANCE hInstPrev, LPSTR cmdline, int cmdShow)
{
    LoggerHelpers::init_logger(moduleName, internalPath, LogSettings::workspacesLauncherLoggerName);
    InitUnhandledExceptionHandler();

    Trace::Workspaces::RegisterProvider();

    Shared::Trace::ETWTrace trace{};
    trace.UpdateState(true);

    if (powertoys_gpo::getConfiguredWorkspacesEnabledValue() == powertoys_gpo::gpo_rule_configured_disabled)
    {
        Logger::warn(L"Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.");
        return 0;
    }

    int argumentCount = 0;
    auto arguments = CommandLineToArgvW(GetCommandLineW(), &argumentCount);
    auto freeArguments = wil::scope_exit([&] { LocalFree(arguments); });
    if (!arguments)
    {
        Logger::error(L"Unable to parse command line: {}", get_last_error_or_default(GetLastError()));
        return 1;
    }
    if (argumentCount > 1 && std::wstring_view(arguments[1]).starts_with(L"--"))
    {
#ifdef _DEBUG
        const bool preview = std::wstring_view(arguments[1]) == L"--preview-signature-warning";
        if (argumentCount != 3 || (!preview && std::wstring_view(arguments[1]) != L"--verify-signature"))
        {
            Logger::error(L"Unsupported Workspaces diagnostic arguments");
            return 1;
        }
        winrt::init_apartment(winrt::apartment_type::multi_threaded);
        auto uninitialize = wil::scope_exit([] { winrt::uninit_apartment(); });
        const auto executable = SignatureVerification::Verify(arguments[2]);
        if (preview)
        {
            return PreviewSignatureWarning(executable);
        }
        json::JsonObject output;
        output.SetNamedValue(L"path", json::value(executable.path));
        output.SetNamedValue(L"status", json::value(SignatureVerification::StatusName(executable.result.status)));
        output.SetNamedValue(L"reason", json::value(executable.result.Reason()));
        output.SetNamedValue(L"windowsStatus", json::value(fmt::format(L"0x{:08X}", static_cast<uint32_t>(executable.result.error))));
        output.SetNamedValue(L"publisher", json::value(executable.result.publisher));
        output.SetNamedValue(L"source", json::value(executable.result.source));
        output.SetNamedValue(L"packageFullName", json::value(executable.package ? executable.package->fullName : L""));
        output.SetNamedValue(L"applicationUserModelId", json::value(executable.package ? executable.package->applicationUserModelId : L""));
        const std::string text = winrt::to_string(output.Stringify()) + "\n";
        DWORD written = 0;
        if (!WriteFile(GetStdHandle(STD_OUTPUT_HANDLE), text.data(), static_cast<DWORD>(text.size()), &written, nullptr))
        {
            Logger::error(L"Unable to write signature diagnostics: {}", get_last_error_or_default(GetLastError()));
            return 1;
        }
        return 0;
#else
        Logger::error(L"Workspaces diagnostics are only available in Debug builds");
        return 1;
#endif
    }

    std::wstring cmdLineStr{ GetCommandLineW() };
    auto cmdArgs = split(cmdLineStr, L" ");
    if (cmdArgs.workspaceId.empty())
    {
        Logger::warn("Incorrect command line arguments: no workspace id");
        MessageBox(NULL, GET_RESOURCE_STRING(IDS_INCORRECT_ARGS).c_str(), GET_RESOURCE_STRING(IDS_WORKSPACES).c_str(), MB_ICONERROR | MB_OK);
        return 1;
    }

    if (is_process_elevated())
    {
        if (cmdArgs.isRestarted)
        {
            Logger::error(L"Workspaces Launcher is still elevated after restart; refusing to launch workspace applications");
            return 1;
        }
        Logger::warn("Workspaces Launcher is elevated, restart");

        constexpr DWORD exe_path_size = 0xFFFF;
        auto exe_path = std::make_unique<wchar_t[]>(exe_path_size);
        GetModuleFileNameW(nullptr, exe_path.get(), exe_path_size);

        const auto modulePath = get_module_folderpath();

        std::wstring cmd = cmdArgs.workspaceId + L" " + std::to_wstring(cmdArgs.invokePoint) + L" " + NonLocalizable::restartedString;

        RunNonElevatedEx(exe_path.get(), cmd, modulePath);
        return 1;
    }

    auto mutex = CreateMutex(nullptr, true, instanceMutexName.c_str());
    if (mutex == nullptr)
    {
        Logger::error(L"Failed to create mutex. {}", get_last_error_or_default(GetLastError()));
    }

    if (GetLastError() == ERROR_ALREADY_EXISTS)
    {
        Logger::warn(L"WorkspacesLauncher instance is already running");
        return 0;
    }

    // COM should be initialized before ShellExecuteEx is called.
    if (FAILED(CoInitializeEx(NULL, COINIT_MULTITHREADED)))
    {
        Logger::error("CoInitializeEx failed");
        return 1;
    }

    SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

    Logger::trace(L"Invoke point: {}", cmdArgs.invokePoint);

    // read workspaces
    std::vector<WorkspacesData::WorkspacesProject> workspaces;
    WorkspacesData::WorkspacesProject projectToLaunch{};
    if (cmdArgs.invokePoint == InvokePoint::LaunchAndEdit)
    {
        // check the temp file in case the project is just created and not saved to the workspaces.json yet
        auto file = WorkspacesData::TempWorkspacesFile();
        auto res = JsonUtils::ReadSingleWorkspace(file);
        if (res.isOk() && projectToLaunch.id == cmdArgs.workspaceId)
        {
            projectToLaunch = res.getValue();
        }
        else if (res.isError())
        {
            std::wstring formattedMessage{};
            switch (res.error())
            {
            case JsonUtils::WorkspacesFileError::FileReadingError:
                formattedMessage = fmt::format(GET_RESOURCE_STRING(IDS_FILE_READING_ERROR), file);
                break;
            case JsonUtils::WorkspacesFileError::IncorrectFileError:
                formattedMessage = fmt::format(GET_RESOURCE_STRING(IDS_INCORRECT_FILE_ERROR), file);
                break;
            }

            MessageBox(NULL, formattedMessage.c_str(), GET_RESOURCE_STRING(IDS_WORKSPACES).c_str(), MB_ICONERROR | MB_OK);
            return 1;
        }
    }

    if (projectToLaunch.id.empty())
    {
        auto file = WorkspacesData::WorkspacesFile();
        auto res = JsonUtils::ReadWorkspaces(file);
        if (res.isOk())
        {
            workspaces = res.getValue();
        }
        else
        {
            std::wstring formattedMessage{};
            switch (res.error())
            {
            case JsonUtils::WorkspacesFileError::FileReadingError:
                formattedMessage = fmt::format(GET_RESOURCE_STRING(IDS_FILE_READING_ERROR), file);
                break;
            case JsonUtils::WorkspacesFileError::IncorrectFileError:
                formattedMessage = fmt::format(GET_RESOURCE_STRING(IDS_INCORRECT_FILE_ERROR), file);
                break;
            }

            MessageBox(NULL, formattedMessage.c_str(), GET_RESOURCE_STRING(IDS_WORKSPACES).c_str(), MB_ICONERROR | MB_OK);
            return 1;
        }

        if (workspaces.empty())
        {
            Logger::warn("Workspaces file is empty");
            std::wstring formattedMessage = fmt::format(GET_RESOURCE_STRING(IDS_EMPTY_FILE), file);
            MessageBox(NULL, formattedMessage.c_str(), GET_RESOURCE_STRING(IDS_WORKSPACES).c_str(), MB_ICONERROR | MB_OK);
            return 1;
        }

        for (const auto& proj : workspaces)
        {
            if (proj.id == cmdArgs.workspaceId)
            {
                projectToLaunch = proj;
                break;
            }
        }
    }

    if (projectToLaunch.id.empty())
    {
        Logger::critical(L"Workspace {} not found", cmdArgs.workspaceId);
        std::wstring formattedMessage = fmt::format(GET_RESOURCE_STRING(IDS_PROJECT_NOT_FOUND), cmdArgs.workspaceId);
        MessageBox(NULL, formattedMessage.c_str(), GET_RESOURCE_STRING(IDS_WORKSPACES).c_str(), MB_ICONERROR | MB_OK);
        return 1;
    }

    // prepare project in advance
    auto installedApps = Utils::Apps::GetAppsList();
    bool updatedApps = Utils::Apps::UpdateWorkspacesApps(projectToLaunch, installedApps);
    bool updatedIds = false;

    // verify apps have ids
    for (auto& app : projectToLaunch.apps)
    {
        if (app.id.empty())
        {
            app.id = CreateGuidString();
            updatedIds = true;
        }
    }

    // update the file before launching, so WorkspacesWindowArranger and WorkspacesLauncherUI could get updated app paths
    if (updatedApps || updatedIds)
    {
        for (int i = 0; i < workspaces.size(); i++)
        {
            if (workspaces[i].id == projectToLaunch.id)
            {
                workspaces[i] = projectToLaunch;
                break;
            }
        }

        json::to_file(WorkspacesData::WorkspacesFile(), WorkspacesData::WorkspacesListJSON::ToJson(workspaces));
    }

    // launch
    {
        Launcher launcher(projectToLaunch, workspaces, cmdArgs.invokePoint);
    }

    trace.Flush();
    trace.UpdateState(false);

    Trace::Workspaces::UnregisterProvider();

    Logger::trace("Finished");
    CoUninitialize();
    return 0;
}
