#include "pch.h"

#include <WorkspacesLib/JsonUtils.h>
#include <WorkspacesLib/IPCHelper.h>
#include <WorkspacesLib/utils.h>

#include <common/utils/gpo.h>
#include <common/utils/logger_helper.h>
#include <common/utils/UnhandledExceptionHandler.h>
#include <common/utils/window.h>

#include <WindowArranger.h>
#include <WorkspacesLib/CliCommands.h>
#include <WorkspacesLib/OperationLifetime.h>
#include <WorkspacesLib/WorkspaceLaunchSession.h>
#include <condition_variable>
#include <shellapi.h>
#include <spdlog/sinks/null_sink.h>

namespace
{
    int RunSessionArranger(int argc, wchar_t** argv)
    {
        Logger::init(std::vector<spdlog::sink_ptr>{ std::make_shared<spdlog::sinks::null_sink_mt>() });
        try
        {
            winrt::init_apartment();
            if (argc != 7 || !std::wstring_view(argv[6]).starts_with(L"--plan="))
                throw WorkspacesCli::Error(2, L"invalidArguments", "Invalid session arranger arguments.");
            const auto operationId = WorkspacesCli::NormalizeId(argv[3]);
            const auto parentPid = std::stoul(argv[4]);
            const auto seconds = std::stoul(argv[5]);
            if (!parentPid || seconds > 600)
                throw WorkspacesCli::Error(2, L"invalidArguments", "Invalid operation lifetime.");
            WorkspacesSession::OperationLifetime lifetime(parentPid, seconds ? seconds * 1000 + 5000 : INFINITE);
            if (powertoys_gpo::getConfiguredWorkspacesEnabledValue() == powertoys_gpo::gpo_rule_configured_disabled)
                throw WorkspacesCli::Error(6, L"disabledByPolicy", "Workspaces is disabled by policy.");
            SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
            auto project = Workspaces::LaunchSession::Receive(std::wstring(argv[6]).substr(7));
            if (WorkspacesCli::NormalizeId(project.id) != WorkspacesCli::NormalizeId(argv[1]))
                throw WorkspacesCli::Error(8, L"invalidData", "Protected plan workspace mismatch.");
            auto pipe = std::make_unique<IPCHelper>(
                IPCHelperStrings::WindowArrangerPipeName + operationId,
                IPCHelperStrings::LauncherArrangerPipeName + operationId,
                [](const std::wstring&) {},
                IPCHelper::SessionPeer(parentPid));
            if (!pipe->WaitForReady(10000))
                throw WorkspacesCli::Error(9, L"outcomeUnknown", "Operation snapshot pipes did not become ready.");
            WindowArranger arranger(project, std::move(pipe), operationId);
            return 0;
        }
        catch (const WorkspacesCli::Error& error)
        {
            return error.exitCode;
        }
        catch (const winrt::hresult_error&)
        {
            OutputDebugStringW(L"Workspaces CLI: arranger Windows operation failed.\n");
            return 1;
        }
        catch (const std::exception&)
        {
            OutputDebugStringW(L"Workspaces CLI: invalid arranger operation.\n");
            return 1;
        }
    }
}

int APIENTRY WinMain(HINSTANCE, HINSTANCE, LPSTR, int)
{
    int argc = 0;
    auto argv = CommandLineToArgvW(GetCommandLineW(), &argc);
    auto freeArguments = wil::scope_exit([&] { LocalFree(argv); });
    if (argv && argc > 2 && std::wstring_view(argv[2]) == L"--session-worker")
        return RunSessionArranger(argc, argv);
    return 1;
}
