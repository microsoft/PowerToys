// Copyright (c) Microsoft Corporation. Licensed under the MIT license.
#pragma once

#include <map>
#include <optional>
#include <stdexcept>
#include <string>
#include <WorkspacesLib/LaunchDecision.h>
#include <WorkspacesLib/WorkspaceStore.h>
#include <workspaces-common/InvokePoint.h>

namespace WorkspacesSession
{
    inline constexpr DWORD MaxPayload = 1024 * 1024;
    inline constexpr DWORD MaxResultPayload = 3 * MaxPayload;

    struct Error : std::runtime_error
    {
        int exitCode;
        std::wstring code;
        bool executionStillRunning;

        Error(int exitCode, std::wstring code, const char* message, bool executionStillRunning = false) :
            std::runtime_error(message), exitCode(exitCode), code(std::move(code)), executionStillRunning(executionStillRunning)
        {
        }
    };

    enum class Presentation
    {
        Progress,
        ConfirmationOnly,
    };

    struct Request
    {
        std::wstring id;
        std::wstring name;
        InvokePoint invokePoint = InvokePoint::EditorButton;
        Presentation presentation = Presentation::Progress;
        DWORD timeoutSeconds = 0;
    };

    struct Result
    {
        std::wstring operationId;
        WorkspacesData::WorkspacesProject project;
        WorkspacesData::LaunchingAppStateMap applications;
        std::map<std::wstring, DWORD> applicationErrors;
        std::map<std::wstring, LaunchDecision> approvals;
        std::optional<WorkspaceStore::UpdateResult> persistence;
        bool loggingAvailable = true;
    };

    struct ExecutionOptions
    {
        std::wstring operationId;
        HANDLE cancelEvent = nullptr;
        ULONGLONG deadline = 0;
        DWORD timeoutSeconds = 0;
        Presentation presentation = Presentation::Progress;
    };

    Result Start(const Request& request, HANDLE cancelEvent = nullptr);
    Result StartSnapshot(const Request& request, const WorkspacesData::WorkspacesProject& snapshot, bool editorDraft, HANDLE cancelEvent = nullptr);
    Result Execute(WorkspacesData::WorkspacesProject project, InvokePoint invokePoint, bool editorDraft, const ExecutionOptions& options);
    // One-shot adapters call this only after flushing the error response.
    void ExitIfExecutionUnresponsive(const Error& error);

    // Internal operation protocol shared by the entry adapter and the Medium host.
    json::JsonObject SerializeResult(const Result& result);
    Result DeserializeResult(const json::JsonObject& message, const std::wstring& operationId);
    json::JsonObject SerializeError(const Error& error, const std::wstring& operationId);
    Result ReadResult(HANDLE process, HANDLE pipe, HANDLE cancel, ULONGLONG deadline, const std::wstring& operationId);
    void CheckContext(bool requireMedium);
    WorkspacesData::WorkspacesProject LoadSnapshot(const Request& request, bool& editorDraft);
    void PrepareProject(WorkspacesData::WorkspacesProject& project, bool editorDraft, Presentation presentation);
    void InitializeApplicationIds(const std::filesystem::path& fileName, WorkspacesData::WorkspacesProject& project);
}
