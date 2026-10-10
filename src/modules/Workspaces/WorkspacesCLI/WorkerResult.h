// Copyright (c) Microsoft Corporation. Licensed under the MIT license.
#pragma once

#include <algorithm>
#include <WorkspacesLib/CliCommands.h>
#include "ApprovalChannel.h"

namespace WorkspacesCli
{
    inline json::JsonObject ReadWorkerResult(HANDLE process, HANDLE pipe, HANDLE cancel, ULONGLONG deadline,
                                             FrontendApproval& presenter, const std::wstring& operationId, int& exitCode)
    {
        std::string response;
        const auto acknowledgementDeadline = deadline + 2000;
        ULONGLONG cancelDeadline = 0;
        for (;;)
        {
            const auto now = GetTickCount64();
            if (now >= deadline)
                presenter.Stop();
            if (WaitForSingleObject(cancel, 0) == WAIT_OBJECT_0)
            {
                presenter.Stop();
                if (!cancelDeadline)
                    cancelDeadline = (std::min)(now + 2000, acknowledgementDeadline);
            }
            // Stop interaction at the deadline, but let the worker report its acknowledged outcome.
            if (now >= acknowledgementDeadline || (cancelDeadline && now >= cancelDeadline))
                throw Error(9, L"outcomeUnknown", "Worker did not acknowledge completion before the wait ended. Already launched applications remain open; do not retry automatically.");
            presenter.Poll();
            DWORD available = 0;
            if (PeekNamedPipe(pipe, nullptr, 0, nullptr, &available, nullptr) && available)
            {
                char buffer[4096];
                DWORD received = 0;
                if (!ReadFile(pipe, buffer, (std::min)(available, static_cast<DWORD>(sizeof(buffer))), &received, nullptr))
                    throw Error(9, L"outcomeUnknown", "Cannot read worker result.");
                response.append(buffer, received);
                if (response.size() > MaxPayload)
                    throw Error(9, L"outcomeUnknown", "Worker returned an oversized result.");
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
        presenter.Stop();
        DWORD processExit = 1;
        if (!GetExitCodeProcess(process, &processExit))
            throw Error(9, L"outcomeUnknown", "Cannot read worker exit status.");
        if (response.empty())
            throw Error(9, L"outcomeUnknown", "Worker exited without a final result.");
        json::JsonObject output;
        try
        {
            output = json::JsonObject::Parse(winrt::to_hstring(response));
            if (output.GetNamedNumber(L"schemaVersion") != 1 || output.GetNamedString(L"command") != L"launch")
                throw Error(9, L"outcomeUnknown", "Worker result contract mismatch.");
            if (output.HasKey(L"result") &&
                output.GetNamedObject(L"result").GetNamedString(L"operationId") != operationId)
                throw Error(9, L"outcomeUnknown", "Worker returned a different operation result.");
        }
        catch (const winrt::hresult_error&)
        {
            throw Error(9, L"outcomeUnknown", "Worker returned an invalid result.");
        }
        exitCode = static_cast<int>(processExit);
        return output;
    }
}
