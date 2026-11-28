// Copyright (c) Microsoft Corporation. Licensed under the MIT license.
#include "pch.h"
#include "LaunchSession.h"
#include "CliCommands.h"
#include "SessionLogging.h"
#include "trace.h"
#include <WorkspacesLauncher/Launcher.h>

#include <future>
#include <thread>
#include <common/Telemetry/EtwTrace/EtwTrace.h>
#include <common/utils/gpo.h>
#include <common/utils/logger_helper.h>
#include <wil/resource.h>

#pragma comment(lib, "Shcore.lib")

namespace WorkspacesSession
{
    namespace
    {
        Result ExecuteCore(WorkspacesData::WorkspacesProject project, InvokePoint invokePoint, bool draft, const ExecutionOptions& options)
        {
            if (options.presentation == Presentation::ConfirmationOnly)
                WorkspacesCli::CheckEnabled();
            else if (powertoys_gpo::getConfiguredWorkspacesEnabledValue() == powertoys_gpo::gpo_rule_configured_disabled)
                throw Error(6, L"disabledByPolicy", "Workspaces is disabled by policy.");
            if (draft && (options.presentation != Presentation::Progress || invokePoint != InvokePoint::LaunchAndEdit))
                throw Error(8, L"invalidData", "Invalid editor draft session.");
            wil::unique_mutex_nothrow mutex(CreateMutexW(nullptr, TRUE, L"Local\\PowerToys_WorkspacesLauncher_InstanceMutex"));
            const auto mutexError = GetLastError();
            if (!mutex || mutexError == ERROR_ALREADY_EXISTS)
                throw Error(5, L"busy", "Another workspace launch is running.");
            auto release = wil::scope_exit([&] { ReleaseMutex(mutex.get()); });
            const auto checkLifetime = [&] {
                if (WaitForSingleObject(options.cancelEvent, 0) == WAIT_OBJECT_0)
                    throw Error(12, L"canceled", "Further application launches were canceled.");
                if (options.deadline && GetTickCount64() >= options.deadline)
                    throw Error(9, L"timeout", "Session deadline has expired. Already launched applications remain open.");
            };
            checkLifetime();
            PrepareProject(project, draft, options.presentation);
            checkLifetime();
            Result result;
            result.operationId = options.operationId;
            result.project = project;
            const bool gui = options.presentation == Presentation::Progress;
            if (gui)
                LoggerHelpers::init_logger(L"Workspaces\\WorkspacesLauncher", L"", LogSettings::workspacesLauncherLoggerName);
            else
                result.loggingAvailable = Logging::Initialize();
            Logger::info("Workspaces: executing shared session in Medium process {}", GetCurrentProcessId());
            std::optional<Shared::Trace::ETWTrace> trace;
            if (gui)
            {
                Trace::Workspaces::RegisterProvider();
                trace.emplace();
                trace->UpdateState(true);
            }
            auto finishTrace = wil::scope_exit([&] {
                if (trace)
                {
                    trace->Flush();
                    trace->UpdateState(false);
                    Trace::Workspaces::UnregisterProvider();
                }
            });
            const auto dpi = SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
            winrt::check_bool(dpi != nullptr);
            auto restoreDpi = wil::scope_exit([&] { SetThreadDpiAwarenessContext(dpi); });
            {
                Launcher launcher(project, invokePoint, options);
                if (launcher.IsCanceled())
                    throw Error(12, L"canceled", "Further application launches were canceled.");
                if (launcher.GetArrangerError() != ERROR_SUCCESS)
                    throw Error(7, launcher.GetArrangerError() == ERROR_CANCELLED ? L"consentDenied" : L"arrangerFailed", "Window arranger could not start or elevation was declined.");
                checkLifetime();
                if (!launcher.HasArrangerResult())
                    throw Error(9, L"outcomeUnknown", "Arranger exited without a conclusive result.");
                result.applications = launcher.GetResult();
                result.applicationErrors = launcher.GetApplicationErrors();
                result.approvals = launcher.GetApprovalDecisions();
            }
            if (!draft && invokePoint != InvokePoint::LaunchAndEdit)
            {
                result.persistence = WorkspaceStore::UpdateLastLaunched(WorkspacesData::WorkspacesFile(), project.id, std::chrono::system_clock::to_time_t(std::chrono::system_clock::now()));
                if (*result.persistence != WorkspaceStore::UpdateResult::Updated)
                    Logger::warn("Workspace launch history could not be saved.");
            }
            Logger::info("Workspaces: shared session finished.");
            Logger::flush();
            return result;
        }
    }

    Result Execute(WorkspacesData::WorkspacesProject project, InvokePoint invokePoint, bool editorDraft, const ExecutionOptions& options)
    {
        CheckContext(true);
        auto cancellation = std::make_shared<wil::unique_handle>(CreateEventW(nullptr, TRUE, FALSE, nullptr));
        winrt::check_bool(static_cast<bool>(*cancellation));
        ExecutionOptions execution = options;
        execution.cancelEvent = cancellation->get();
        if (options.cancelEvent && WaitForSingleObject(options.cancelEvent, 0) == WAIT_OBJECT_0)
            winrt::check_bool(SetEvent(cancellation->get()));
        std::packaged_task<Result()> task([project = std::move(project), invokePoint, editorDraft, execution, cancellation]() mutable {
            winrt::init_apartment();
            auto uninitialize = wil::scope_exit([] { winrt::uninit_apartment(); });
            return ExecuteCore(std::move(project), invokePoint, editorDraft, execution);
        });
        auto result = task.get_future();
        std::thread worker(std::move(task));
        auto cleanup = wil::scope_exit([&] {
            if (worker.joinable())
            {
                if (!SetEvent(cancellation->get()))
                    OutputDebugStringW(L"Workspaces: could not signal unresponsive execution cleanup.\n");
                // A blocked Shell call cannot be safely killed as a thread. Its state stays owned until
                // it returns or the one-shot GUI/CLI adapter exits after reporting outcomeUnknown.
                worker.detach();
            }
        });
        ULONGLONG cancellationDeadline{};
        for (;;)
        {
            if (options.cancelEvent && WaitForSingleObject(options.cancelEvent, 0) == WAIT_OBJECT_0)
                winrt::check_bool(SetEvent(cancellation->get()));
            if (result.wait_for(std::chrono::milliseconds(20)) == std::future_status::ready)
            {
                worker.join();
                return result.get();
            }
            const auto now = GetTickCount64();
            if (!cancellationDeadline && WaitForSingleObject(cancellation->get(), 0) == WAIT_OBJECT_0)
                cancellationDeadline = now + 2000;
            if ((options.deadline && now >= options.deadline + 2000) ||
                (cancellationDeadline && now >= cancellationDeadline))
                throw Error(9, L"outcomeUnknown", "The session did not acknowledge completion. Do not retry automatically.", true);
        }
    }

    void ExitIfExecutionUnresponsive(const Error& error)
    {
        if (error.executionStillRunning)
        {
            // Do not run process-wide destructors concurrently with a blocked Shell/COM operation.
            TerminateProcess(GetCurrentProcess(), error.exitCode);
            ExitProcess(error.exitCode);
        }
    }
}
