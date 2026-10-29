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
            if (argc != 6)
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
            std::mutex mutex;
            std::condition_variable received;
            std::optional<WorkspacesData::WorkspacesProject> project;
            bool invalid = false;
            auto pipe = std::make_unique<IPCHelper>(
                IPCHelperStrings::WindowArrangerPipeName + operationId,
                IPCHelperStrings::LauncherArrangerPipeName + operationId,
                [&](const std::wstring& message) {
                    std::lock_guard lock(mutex);
                    try
                    {
                        if (message.size() > WorkspacesCli::MaxPayload)
                            throw std::runtime_error("Oversized snapshot");
                        const auto snapshot = json::JsonObject::Parse(message);
                        if (snapshot.GetNamedNumber(L"protocolVersion") != 1 || snapshot.GetNamedString(L"operationId") != operationId)
                            throw std::runtime_error("Invalid snapshot protocol");
                        project = WorkspacesData::WorkspacesProjectJSON::FromJson(snapshot.GetNamedObject(L"workspace"));
                        invalid = !project || WorkspacesCli::NormalizeId(project->id) != WorkspacesCli::NormalizeId(argv[1]);
                    }
                    catch (const std::exception&)
                    {
                        invalid = true;
                    }
                    catch (const winrt::hresult_error&)
                    {
                        invalid = true;
                    }
                    received.notify_one();
                },
                IPCHelper::SessionPeer(parentPid));
            const auto deadline = std::chrono::steady_clock::now() + std::chrono::seconds(10);
            if (!pipe->WaitForReady(10000))
                throw WorkspacesCli::Error(9, L"outcomeUnknown", "Operation snapshot pipes did not become ready.");
            pipe->send(L"snapshot-request");
            {
                std::unique_lock lock(mutex);
                if (!received.wait_until(lock, deadline, [&] { return project.has_value() || invalid; }) || invalid)
                    throw WorkspacesCli::Error(9, L"outcomeUnknown", "No valid operation snapshot received.");
            }
            WindowArranger arranger(*project, std::move(pipe), operationId);
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
