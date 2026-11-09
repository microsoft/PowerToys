// Copyright (c) Microsoft Corporation. Licensed under the MIT license.
#include "pch.h"
#include "LaunchSessionHost.h"
#include <WorkspacesLib/CliCommands.h>
#include <WorkspacesLib/LaunchSession.h>
#include <WorkspacesLib/OperationLifetime.h>
#include <WorkspacesLib/SessionHandoff.h>
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

        Result ExecuteRequest(const json::JsonObject& request, const ReceivedWorkerHandles& handles, const std::wstring& operationId)
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
            auto project = WorkspacesData::WorkspacesProjectJSON::FromJson(request.GetNamedObject(L"workspace"));
            if (!project)
                throw Error(8, L"invalidData", "Invalid session workspace.");
            const bool draft = request.GetNamedBoolean(L"editorDraft");
            return Execute(std::move(*project), invoke, draft, options);
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
            WriteResponse(handles.result.get(), SerializeResult(ExecuteRequest(ReadRequest(handles.snapshot.get()), handles, operationId)));
            return 0;
        }
        catch (const Error& error)
        {
            ReportError(handles.result.get(), error, operationId);
            ExitIfExecutionUnresponsive(error);
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
