// Copyright (c) Microsoft Corporation. Licensed under the MIT license.
#include "pch.h"
#include "LaunchSessionHost.h"
#include "Launcher.h"

#include <common/Telemetry/EtwTrace/EtwTrace.h>
#include <common/utils/gpo.h>
#include <common/utils/logger_helper.h>
#include <WorkspacesLib/CliCommands.h>
#include <WorkspacesLib/LaunchSession.h>
#include <WorkspacesLib/OperationLifetime.h>
#include <WorkspacesLib/SessionHandoff.h>
#include <WorkspacesLib/SessionLogging.h>
#include <WorkspacesLib/trace.h>
#include <wil/resource.h>

namespace WorkspacesSession
{
    namespace
    {
        HANDLE ParseHandle(const std::wstring& value)
        {
            size_t consumed{};
            const auto raw = std::stoull(value, &consumed);
            if (!raw || raw == UINT64_MAX || raw > UINTPTR_MAX || consumed != value.size() || value.front() == L'-')
                throw Error(2, L"invalidArguments", "Invalid private session handle.");
            return reinterpret_cast<HANDLE>(static_cast<uintptr_t>(raw));
        }

        void WriteResponse(HANDLE pipe, const json::JsonObject& result)
        {
            const auto bytes = winrt::to_string(result.Stringify());
            if (bytes.size() > MaxResultPayload)
                throw Error(9, L"outcomeUnknown", "Session result exceeds the private response limit.");
            size_t offset{};
            while (offset < bytes.size())
            {
                DWORD written{};
                if (!WriteFile(pipe, bytes.data() + offset, static_cast<DWORD>(bytes.size() - offset), &written, nullptr) || !written)
                    throw Error(9, L"outcomeUnknown", "Session result channel was closed.");
                offset += written;
            }
        }

        void ReportError(HANDLE pipe, const Error& error, const std::wstring& operationId)
        {
            if (!pipe)
                return;
            try
            {
                WriteResponse(pipe, SerializeError(error, operationId));
            }
            catch (const Error&)
            {
                OutputDebugStringW(L"Workspaces session result delivery failed.\n");
            }
        }

        json::JsonObject ReadRequest(HANDLE mapping)
        {
            wil::unique_mapview_ptr<DWORD> header(static_cast<DWORD*>(MapViewOfFile(mapping, FILE_MAP_READ, 0, 0, sizeof(DWORD))));
            if (!header || !*header || *header > MaxPayload)
                throw Error(8, L"invalidData", "Invalid session snapshot.");
            const auto size = *header;
            wil::unique_mapview_ptr<char> view(static_cast<char*>(MapViewOfFile(mapping, FILE_MAP_READ, 0, 0, sizeof(DWORD) + size)));
            if (!view)
                throw Error(8, L"invalidData", "Cannot read session snapshot.");
            return json::JsonObject::Parse(winrt::to_hstring(std::string(view.get() + sizeof(DWORD), size)));
        }

        Result Execute(const json::JsonObject& request, const ReceivedWorkerHandles& handles, const std::wstring& operationId)
        {
            CheckContext(true);
            if (request.GetNamedNumber(L"protocolVersion") != 1 ||
                WorkspacesCli::NormalizeId(request.GetNamedString(L"operationId").c_str()) != operationId)
                throw Error(8, L"invalidData", "Invalid session request identity.");
            ExecutionOptions options;
            options.operationId = operationId;
            options.cancelEvent = handles.cancel.get();
            const auto timeout = request.GetNamedNumber(L"timeout");
            const auto presentation = request.GetNamedNumber(L"presentation");
            const auto invokeValue = request.GetNamedNumber(L"invokePoint");
            if (timeout < 0 || timeout > 600 || timeout != static_cast<DWORD>(timeout) ||
                (presentation != 0 && presentation != 1) || invokeValue < 0 || invokeValue > static_cast<int>(InvokePoint::LaunchAndEdit) ||
                invokeValue != static_cast<int>(invokeValue))
                throw Error(8, L"invalidData", "Invalid session options.");
            const auto invoke = static_cast<InvokePoint>(static_cast<int>(invokeValue));
            options.timeoutSeconds = static_cast<DWORD>(timeout);
            options.presentation = static_cast<Presentation>(presentation);
            const auto deadline = request.GetNamedNumber(L"deadline");
            if (deadline < 0 || deadline > 9007199254740991.0 ||
                deadline != static_cast<ULONGLONG>(deadline) || (!options.timeoutSeconds && deadline != 0))
                throw Error(8, L"invalidData", "Invalid session deadline.");
            options.deadline = static_cast<ULONGLONG>(deadline);
            if ((options.presentation == Presentation::ConfirmationOnly && !options.timeoutSeconds) ||
                (options.timeoutSeconds != 0 && (!options.deadline || options.deadline <= GetTickCount64())))
                throw Error(9, L"timeout", "Session deadline has expired.");
            wil::unique_handle owner(OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, handles.ownerPid));
            if (!owner || !SameUserSession(GetCurrentProcess(), owner.get()))
                throw Error(7, L"unsupportedContext", "Session owner is not in the same user session.");
            OperationLifetime lifetime(handles.ownerPid,
                                       options.timeoutSeconds ? options.timeoutSeconds * 1000 + 5000 : INFINITE,
                                       handles.ownerLifetime.get());
            if (options.presentation == Presentation::ConfirmationOnly)
                WorkspacesCli::CheckEnabled();
            else if (powertoys_gpo::getConfiguredWorkspacesEnabledValue() == powertoys_gpo::gpo_rule_configured_disabled)
                throw Error(6, L"disabledByPolicy", "Workspaces is disabled by policy.");
            auto project = WorkspacesData::WorkspacesProjectJSON::FromJson(request.GetNamedObject(L"workspace"));
            if (!project)
                throw Error(8, L"invalidData", "Invalid session workspace.");
            const bool draft = request.GetNamedBoolean(L"editorDraft");
            if (draft && (options.presentation != Presentation::Progress || invoke != InvokePoint::LaunchAndEdit))
                throw Error(8, L"invalidData", "Invalid editor draft session.");
            wil::unique_mutex_nothrow mutex(CreateMutexW(nullptr, TRUE, L"Local\\PowerToys_WorkspacesLauncher_InstanceMutex"));
            const auto mutexError = GetLastError();
            if (!mutex || mutexError == ERROR_ALREADY_EXISTS)
                throw Error(5, L"busy", "Another workspace launch is running.");
            auto release = wil::scope_exit([&] { ReleaseMutex(mutex.get()); });
            auto checkLifetime = [&] {
                if (WaitForSingleObject(handles.cancel.get(), 0) == WAIT_OBJECT_0)
                    throw Error(12, L"canceled", "Further application launches were canceled.");
                if (options.deadline && GetTickCount64() >= options.deadline)
                    throw Error(9, L"timeout", "Session deadline has expired. Already launched applications remain open.");
            };
            checkLifetime();
            PrepareProject(*project, draft, options.presentation);
            checkLifetime();
            Result result;
            result.operationId = operationId;
            result.project = *project;
            const bool gui = options.presentation == Presentation::Progress;
            if (gui)
                LoggerHelpers::init_logger(L"Workspaces\\WorkspacesLauncher", L"", LogSettings::workspacesLauncherLoggerName);
            else
                result.loggingAvailable = Logging::Initialize();
            Logger::info("Workspaces CLI: shared session host verified Medium/non-elevated.");
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
            SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
            {
                Launcher launcher(*project, invoke, options);
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
            if (!draft && invoke != InvokePoint::LaunchAndEdit)
            {
                result.persistence = WorkspaceStore::UpdateLastLaunched(WorkspacesData::WorkspacesFile(), project->id, std::chrono::system_clock::to_time_t(std::chrono::system_clock::now()));
                if (*result.persistence != WorkspaceStore::UpdateResult::Updated)
                    Logger::warn("Workspace launch history could not be saved.");
            }
            Logger::info("Workspaces CLI: shared session host finished.");
            Logger::flush();
            return result;
        }
    }

    std::optional<int> TryRunHost(const std::vector<std::wstring>& arguments)
    {
        if (arguments.empty() || (arguments[0] != L"--session-worker" && arguments[0] != L"--session-connect"))
            return std::nullopt;
        ReceivedWorkerHandles handles;
        std::wstring operationId;
        try
        {
            const bool connected = arguments[0] == L"--session-connect";
            if (arguments.size() != (connected ? 3u : 7u))
                throw Error(2, L"invalidArguments", "Invalid private session arguments.");
            operationId = WorkspacesCli::NormalizeId(arguments[1]);
            if (connected)
                handles = ReceiveWorkerHandles(operationId, static_cast<DWORD>(std::stoul(arguments[2])), { L"PowerToys.WorkspacesCLI.exe", L"PowerToys.WorkspacesLauncher.exe" });
            else
            {
                handles.snapshot.reset(ParseHandle(arguments[2]));
                handles.cancel.reset(ParseHandle(arguments[3]));
                handles.result.reset(ParseHandle(arguments[4]));
                handles.ownerLifetime.reset(ParseHandle(arguments[5]));
                handles.ownerPid = static_cast<DWORD>(std::stoul(arguments[6]));
                for (const auto handle : { handles.snapshot.get(), handles.cancel.get(), handles.result.get(), handles.ownerLifetime.get() })
                {
                    if (!SetHandleInformation(handle, HANDLE_FLAG_INHERIT, 0))
                        throw Error(8, L"invalidData", "Invalid inherited session capability.");
                }
            }
            WriteResponse(handles.result.get(), SerializeResult(Execute(ReadRequest(handles.snapshot.get()), handles, operationId)));
            return 0;
        }
        catch (const Error& error)
        {
            ReportError(handles.result.get(), error, operationId);
            return error.exitCode;
        }
        catch (const winrt::hresult_error&)
        {
            ReportError(handles.result.get(), { 8, L"invalidData", "Malformed session data." }, operationId);
            return 8;
        }
        catch (const std::exception&)
        {
            ReportError(handles.result.get(), { 1, L"internalError", "Workspace session failed." }, operationId);
            return 1;
        }
    }
}
