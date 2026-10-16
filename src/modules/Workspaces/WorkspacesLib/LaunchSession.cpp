// Copyright (c) Microsoft Corporation. Licensed under the MIT license.
#include "pch.h"
#include "LaunchSession.h"
#include "CliCommands.h"
#include "AppUtils.h"
#include "SessionHandoff.h"

#include <algorithm>
#include <limits>
#include <common/utils/elevation.h>
#include <common/utils/gpo.h>
#include <wil/resource.h>
#include <wil/stl.h>
#include <wil/win32_helpers.h>

namespace WorkspacesSession
{
    namespace
    {
        void Check(BOOL success, const char* message)
        {
            if (!success)
                throw Error(1, L"internalError", message);
        }

        std::filesystem::path HostPath()
        {
            std::wstring path;
            winrt::check_hresult(wil::GetModuleFileNameW(nullptr, path));
            return std::filesystem::path(path).parent_path() / L"PowerToys.WorkspacesLauncher.exe";
        }

        void CheckRequest(const Request& request)
        {
            CheckContext(false);
            if (request.presentation != Presentation::Progress && request.presentation != Presentation::ConfirmationOnly)
                throw Error(2, L"invalidArguments", "Invalid session presentation.");
            if (request.presentation == Presentation::ConfirmationOnly)
                WorkspacesCli::CheckEnabled();
            else if (powertoys_gpo::getConfiguredWorkspacesEnabledValue() == powertoys_gpo::gpo_rule_configured_disabled)
                throw Error(6, L"disabledByPolicy", "Workspaces is disabled by policy.");
            if (request.timeoutSeconds > 600 || (request.presentation == Presentation::ConfirmationOnly && !request.timeoutSeconds))
                throw Error(2, L"invalidArguments", "Invalid session deadline.");
        }

        bool AssignMissingIds(WorkspacesData::WorkspacesProject& project)
        {
            bool changed = false;
            for (auto& app : project.apps)
            {
                if (!app.id.empty())
                    continue;
                GUID guid{};
                winrt::check_hresult(CoCreateGuid(&guid));
                wchar_t text[40]{};
                if (!StringFromGUID2(guid, text, ARRAYSIZE(text)))
                    throw Error(1, L"internalError", "Cannot initialize application identity.");
                app.id = text;
                changed = true;
            }
            WorkspacesCli::ValidateLaunch(project);
            return changed;
        }

        void CommitMetadata(const std::filesystem::path& file, const WorkspacesData::WorkspacesProject& original, const WorkspacesData::WorkspacesProject& updated)
        {
            const auto saved = WorkspaceStore::UpdateApplicationMetadata(file, original, updated);
            if (saved == WorkspaceStore::UpdateResult::Conflict)
                throw Error(8, L"workspaceChanged", "Workspace metadata changed before the session could be prepared.");
            if (saved == WorkspaceStore::UpdateResult::Unverified)
                throw Error(11, L"persistenceUnverified", "Workspace metadata persistence could not be verified.");
            if (saved != WorkspaceStore::UpdateResult::Updated)
                throw Error(8, L"applicationIdSaveFailed", "Workspace metadata could not be prepared.");
        }
    }

    void CheckContext(bool requireMedium)
    {
        DWORD session{};
        if (!ProcessIdToSessionId(GetCurrentProcessId(), &session) || !session ||
            (requireMedium && !IsMediumProcess(GetCurrentProcess())))
            throw Error(7, L"unsupportedContext", "Workspaces requires a signed-in session and a Medium execution host.");
    }

    WorkspacesData::WorkspacesProject LoadSnapshot(const Request& request, bool& editorDraft)
    {
        editorDraft = false;
        if (request.invokePoint == InvokePoint::LaunchAndEdit)
        {
            const auto loaded = WorkspacesCli::ReadJson(WorkspacesData::TempWorkspacesFile());
            if (loaded)
            {
                auto draft = WorkspacesData::WorkspacesProjectJSON::FromJson(*loaded);
                if (!draft)
                    throw Error(8, L"invalidData", "The editor workspace draft could not be read.");
                if (WorkspacesCli::NormalizeId(draft->id) == WorkspacesCli::NormalizeId(request.id))
                {
                    editorDraft = true;
                    return *draft;
                }
            }
        }
        WorkspacesCli::Options selection;
        selection.id = request.id;
        selection.name = request.name;
        return WorkspacesCli::Select(WorkspacesCli::LoadWorkspaces(), selection).front();
    }

    void PrepareProject(WorkspacesData::WorkspacesProject& project, bool editorDraft, Presentation presentation)
    {
        WorkspacesCli::ValidateLaunch(project, true);
        try
        {
            WorkspacesCli::NormalizeId(project.id);
        }
        catch (const Error&)
        {
            throw Error(8, L"invalidData", "Workspace identity is invalid.");
        }
        auto prepared = project;
        const bool missingIds = AssignMissingIds(prepared);
        const auto identitySnapshot = prepared;
        const bool resolved = Utils::Apps::UpdateWorkspacesApps(prepared, Utils::Apps::GetAppsList());
        WorkspacesCli::ValidateLaunch(prepared);
        if (!editorDraft && (missingIds || (resolved && presentation == Presentation::Progress)))
        {
            // CLI resolves paths within its snapshot; the existing GUI also persists refreshed metadata.
            const auto& update = presentation == Presentation::Progress ? prepared : identitySnapshot;
            CommitMetadata(WorkspacesData::WorkspacesFile(), project, update);
        }
        project = std::move(prepared);
    }

    void InitializeApplicationIds(const std::filesystem::path& fileName, WorkspacesData::WorkspacesProject& project)
    {
        WorkspacesCli::ValidateLaunch(project, true);
        auto prepared = project;
        if (!AssignMissingIds(prepared))
            return;
        CommitMetadata(fileName, project, prepared);
        project = std::move(prepared);
    }

    json::JsonObject SerializeResult(const Result& result)
    {
        json::JsonObject message;
        message.SetNamedValue(L"protocolVersion", json::value(1));
        message.SetNamedValue(L"kind", json::value(L"session-result"));
        message.SetNamedValue(L"operationId", json::value(result.operationId));
        message.SetNamedValue(L"workspace", WorkspacesData::WorkspacesProjectJSON::ToJson(result.project));
        json::JsonArray applications;
        for (const auto& [app, state] : result.applications)
        {
            auto item = WorkspacesData::AppLaunchInfoJSON::ToJson(state);
            if (const auto error = result.applicationErrors.find(app.id); error != result.applicationErrors.end())
                item.SetNamedValue(L"nativeError", json::value(error->second));
            if (const auto approval = result.approvals.find(app.id); approval != result.approvals.end())
                item.SetNamedValue(L"approval", json::value(static_cast<int>(approval->second)));
            applications.Append(item);
        }
        message.SetNamedValue(L"applications", applications);
        message.SetNamedValue(L"loggingAvailable", json::value(result.loggingAvailable));
        if (result.persistence)
            message.SetNamedValue(L"persistence", json::value(static_cast<int>(*result.persistence)));
        return message;
    }

    json::JsonObject SerializeError(const Error& error, const std::wstring& operationId)
    {
        json::JsonObject message;
        message.SetNamedValue(L"protocolVersion", json::value(1));
        message.SetNamedValue(L"kind", json::value(L"session-error"));
        message.SetNamedValue(L"operationId", json::value(operationId));
        message.SetNamedValue(L"code", json::value(error.code));
        message.SetNamedValue(L"exitCode", json::value(error.exitCode));
        message.SetNamedValue(L"message", json::value(winrt::to_hstring(error.what())));
        return message;
    }

    Result DeserializeResult(const json::JsonObject& message, const std::wstring& operationId)
    {
        if (message.GetNamedNumber(L"protocolVersion") != 1 || message.GetNamedString(L"operationId") != operationId)
            throw Error(9, L"outcomeUnknown", "Session result identity mismatch.");
        if (message.GetNamedString(L"kind") == L"session-error")
        {
            const auto code = message.GetNamedNumber(L"exitCode");
            if (code < 1 || code > 12 || code != static_cast<int>(code))
                throw Error(9, L"outcomeUnknown", "Invalid session failure status.");
            throw Error(static_cast<int>(code), message.GetNamedString(L"code").c_str(), winrt::to_string(message.GetNamedString(L"message")).c_str());
        }
        if (message.GetNamedString(L"kind") != L"session-result")
            throw Error(9, L"outcomeUnknown", "Invalid session response.");
        Result result;
        result.operationId = operationId;
        const auto project = WorkspacesData::WorkspacesProjectJSON::FromJson(message.GetNamedObject(L"workspace"));
        if (!project)
            throw Error(9, L"outcomeUnknown", "Invalid session workspace.");
        try
        {
            WorkspacesCli::ValidateLaunch(*project);
        }
        catch (const Error&)
        {
            throw Error(9, L"outcomeUnknown", "Invalid session workspace.");
        }
        result.project = *project;
        WorkspacesData::LaunchingAppStateMap expected;
        for (const auto& app : project->apps)
            expected.emplace(app, WorkspacesData::LaunchingAppState{ app, nullptr, LaunchingState::Waiting });
        json::JsonObject outcome;
        outcome.SetNamedValue(L"kind", json::value(L"arranger-result"));
        outcome.SetNamedValue(L"protocolVersion", json::value(1));
        outcome.SetNamedValue(L"operationId", json::value(operationId));
        outcome.SetNamedValue(L"applications", message.GetNamedArray(L"applications"));
        result.applications = WorkspacesCli::ParseArrangerResult(outcome, expected, operationId);
        for (const auto& value : message.GetNamedArray(L"applications"))
        {
            const auto item = value.GetObjectW();
            const auto app = WorkspacesData::AppLaunchInfoJSON::FromJson(item);
            if (item.HasKey(L"nativeError"))
            {
                const auto error = item.GetNamedNumber(L"nativeError");
                if (error < 0 || error > MAXDWORD || error != static_cast<DWORD>(error))
                    throw Error(9, L"outcomeUnknown", "Invalid native application error.");
                result.applicationErrors.emplace(app->application.id, static_cast<DWORD>(error));
            }
            if (item.HasKey(L"approval"))
            {
                const auto approval = item.GetNamedNumber(L"approval");
                if (approval < static_cast<int>(LaunchDecision::Approved) ||
                    approval > static_cast<int>(LaunchDecision::InvalidResponse) || approval != static_cast<int>(approval))
                    throw Error(9, L"outcomeUnknown", "Invalid session approval result.");
                result.approvals.emplace(app->application.id, static_cast<LaunchDecision>(approval));
            }
        }
        result.loggingAvailable = message.GetNamedBoolean(L"loggingAvailable");
        if (message.HasKey(L"persistence"))
        {
            const auto value = message.GetNamedNumber(L"persistence");
            if (value < static_cast<int>(WorkspaceStore::UpdateResult::Updated) ||
                value > static_cast<int>(WorkspaceStore::UpdateResult::Unverified) || value != static_cast<int>(value))
                throw Error(9, L"outcomeUnknown", "Invalid session persistence result.");
            result.persistence = static_cast<WorkspaceStore::UpdateResult>(static_cast<int>(value));
        }
        return result;
    }

    Result ReadResult(HANDLE process, HANDLE pipe, HANDLE cancel, ULONGLONG deadline, const std::wstring& operationId)
    {
        std::string response;
        const auto acknowledgementDeadline = deadline ? deadline + 2000 : (std::numeric_limits<ULONGLONG>::max)();
        ULONGLONG cancelDeadline{};
        for (;;)
        {
            const auto now = GetTickCount64();
            if (WaitForSingleObject(cancel, 0) == WAIT_OBJECT_0 && !cancelDeadline)
                cancelDeadline = (std::min)(now + 2000, acknowledgementDeadline);
            if (now >= acknowledgementDeadline || (cancelDeadline && now >= cancelDeadline))
                throw Error(9, L"outcomeUnknown", "The session did not acknowledge completion. Do not retry automatically.");
            DWORD available{};
            if (PeekNamedPipe(pipe, nullptr, 0, nullptr, &available, nullptr) && available)
            {
                char buffer[4096]{};
                DWORD received{};
                Check(ReadFile(pipe, buffer, (std::min)(available, static_cast<DWORD>(sizeof(buffer))), &received, nullptr),
                      "Cannot read the session result.");
                response.append(buffer, received);
                if (response.size() > MaxResultPayload)
                    throw Error(9, L"outcomeUnknown", "Session result exceeds the payload limit.");
                continue;
            }
            if (WaitForSingleObject(process, 0) == WAIT_OBJECT_0)
            {
                if (PeekNamedPipe(pipe, nullptr, 0, nullptr, &available, nullptr) && available)
                    continue;
                break;
            }
            Sleep(20);
        }
        if (response.empty())
            throw Error(9, L"outcomeUnknown", "Session host exited without its result.");
        try
        {
            DWORD processExit{};
            if (!GetExitCodeProcess(process, &processExit))
                throw Error(9, L"outcomeUnknown", "Cannot read session host completion.");
            const auto message = json::JsonObject::Parse(winrt::to_hstring(response));
            const bool error = message.GetNamedString(L"kind") == L"session-error";
            if ((!error && processExit != 0) ||
                (error && message.GetNamedNumber(L"exitCode") != processExit))
                throw Error(9, L"outcomeUnknown", "Session exit status and response disagree.");
            return DeserializeResult(message, operationId);
        }
        catch (const winrt::hresult_error&)
        {
            throw Error(9, L"outcomeUnknown", "Session host returned an invalid result.");
        }
    }

    Result Start(const Request& request, HANDLE cancelEvent)
    {
        CheckRequest(request);
        if (request.id.empty() == request.name.empty())
            throw Error(2, L"invalidArguments", "A session requires exactly one workspace selector.");
        bool draft{};
        const auto project = LoadSnapshot(request, draft);
        return StartSnapshot(request, project, draft, cancelEvent);
    }

    Result StartSnapshot(const Request& request, const WorkspacesData::WorkspacesProject& project, bool draft, HANDLE cancelEvent)
    {
        CheckRequest(request);
        if (draft && (request.presentation != Presentation::Progress || request.invokePoint != InvokePoint::LaunchAndEdit))
            throw Error(2, L"invalidArguments", "Invalid editor draft session.");
        const auto deadline = request.timeoutSeconds ? GetTickCount64() + request.timeoutSeconds * 1000ULL : 0;
        WorkspacesCli::ValidateLaunch(project, true);
        const auto executable = HostPath();
        if (!std::filesystem::is_regular_file(executable.parent_path() / L"PowerToys.WorkspacesWindowArranger.exe"))
            throw Error(6, L"unavailable", "The matching Workspaces arranger is unavailable.");
        GUID guid{};
        winrt::check_hresult(CoCreateGuid(&guid));
        wchar_t text[40]{};
        StringFromGUID2(guid, text, ARRAYSIZE(text));
        const auto operationId = WorkspacesCli::NormalizeId(text);
        json::JsonObject payload;
        payload.SetNamedValue(L"protocolVersion", json::value(1));
        payload.SetNamedValue(L"operationId", json::value(operationId));
        payload.SetNamedValue(L"timeout", json::value(request.timeoutSeconds));
        payload.SetNamedValue(L"deadline", json::value(deadline));
        payload.SetNamedValue(L"presentation", json::value(static_cast<int>(request.presentation)));
        payload.SetNamedValue(L"invokePoint", json::value(static_cast<int>(request.invokePoint)));
        payload.SetNamedValue(L"editorDraft", json::value(draft));
        payload.SetNamedValue(L"workspace", WorkspacesData::WorkspacesProjectJSON::ToJson(project));
        const auto bytes = winrt::to_string(payload.Stringify());
        if (bytes.size() > MaxPayload)
            throw Error(8, L"invalidData", "Workspace exceeds the session payload limit.");
        if (!is_process_elevated())
        {
            ExecutionOptions options;
            options.operationId = operationId;
            options.cancelEvent = cancelEvent;
            options.deadline = deadline;
            options.timeoutSeconds = request.timeoutSeconds;
            options.presentation = request.presentation;
            return Execute(project, request.invokePoint, draft, options);
        }
        if (!std::filesystem::is_regular_file(executable))
            throw Error(6, L"unavailable", "The matching Medium execution host is unavailable.");
        wil::unique_handle mapping(CreateFileMappingW(INVALID_HANDLE_VALUE, nullptr, PAGE_READWRITE, 0, static_cast<DWORD>(sizeof(DWORD) + bytes.size()), nullptr));
        wil::unique_handle cancel;
        if (cancelEvent)
            Check(DuplicateHandle(GetCurrentProcess(), cancelEvent, GetCurrentProcess(), cancel.put(), EVENT_MODIFY_STATE | SYNCHRONIZE, FALSE, 0),
                  "Cannot attach session cancellation.");
        else
            cancel.reset(CreateEventW(nullptr, TRUE, FALSE, nullptr));
        Check(mapping && cancel, "Cannot create session handles.");
        {
            wil::unique_mapview_ptr<char> view(static_cast<char*>(MapViewOfFile(mapping.get(), FILE_MAP_WRITE, 0, 0, 0)));
            Check(static_cast<bool>(view), "Cannot create session snapshot.");
            *reinterpret_cast<DWORD*>(view.get()) = static_cast<DWORD>(bytes.size());
            memcpy(view.get() + sizeof(DWORD), bytes.data(), bytes.size());
        }
        SECURITY_ATTRIBUTES attributes{ sizeof(attributes), nullptr, TRUE };
        wil::unique_handle read, write;
        Check(CreatePipe(read.put(), write.put(), &attributes, 0), "Cannot create session result pipe.");
        Check(SetHandleInformation(read.get(), HANDLE_FLAG_INHERIT, 0), "Cannot restrict session result inheritance.");
        wil::unique_process_information process;
        auto cleanup = wil::scope_exit([&] {
            if (process.hProcess && WaitForSingleObject(process.hProcess, 0) == WAIT_TIMEOUT)
            {
                SetEvent(cancel.get());
                if (WaitForSingleObject(process.hProcess, 2000) == WAIT_TIMEOUT)
                {
                    TerminateProcess(process.hProcess, 9);
                    WaitForSingleObject(process.hProcess, 2000);
                }
            }
        });
        process = StartMediumWorker(executable.wstring(), operationId, { mapping.get(), cancel.get(), write.get() }, deadline);
        write.reset();
        auto result = ReadResult(process.hProcess, read.get(), cancel.get(), deadline, operationId);
        if (WorkspacesCli::NormalizeId(result.project.id) != WorkspacesCli::NormalizeId(project.id))
            throw Error(9, L"outcomeUnknown", "Session returned a different workspace.");
        return result;
    }
}
