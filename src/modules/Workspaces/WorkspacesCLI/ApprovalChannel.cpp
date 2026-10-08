// Copyright (c) Microsoft Corporation. Licensed under the MIT license.
#include <windows.h>
#include <objbase.h>
#include <algorithm>
#include <cstring>
#include <cwchar>
#include <WorkspacesLib/CliCommands.h>
#include "ApprovalChannel.h"
#include "ConsoleApproval.h"

namespace WorkspacesCli
{
    ApprovalPipe ApprovalPipe::Create()
    {
        GUID guid{};
        winrt::check_hresult(CoCreateGuid(&guid));
        wchar_t id[40]{};
        StringFromGUID2(guid, id, ARRAYSIZE(id));
        const auto name = std::wstring(L"\\\\.\\pipe\\PowerToys.Workspaces.Approval.") + id;
        ApprovalPipe result;
        result.write.reset(CreateNamedPipeW(name.c_str(),
                                            PIPE_ACCESS_OUTBOUND | FILE_FLAG_OVERLAPPED | FILE_FLAG_FIRST_PIPE_INSTANCE,
                                            PIPE_TYPE_BYTE | PIPE_READMODE_BYTE | PIPE_WAIT | PIPE_REJECT_REMOTE_CLIENTS,
                                            1,
                                            MaxApprovalBytes + sizeof(DWORD),
                                            0,
                                            0,
                                            nullptr));
        if (!result.write)
            throw Error(1, L"internalError", "Cannot create private approval pipe.");
        wil::unique_handle ready(CreateEventW(nullptr, TRUE, FALSE, nullptr));
        if (!ready)
            throw Error(1, L"internalError", "Cannot create approval connection event.");
        OVERLAPPED connect{};
        connect.hEvent = ready.get();
        const bool connected = ConnectNamedPipe(result.write.get(), &connect) != FALSE;
        const DWORD error = connected ? ERROR_SUCCESS : GetLastError();
        if (!connected && error != ERROR_IO_PENDING && error != ERROR_PIPE_CONNECTED)
            throw Error(1, L"internalError", "Cannot connect approval pipe.");
        auto cancelConnect = wil::scope_exit([&] {
            if (error == ERROR_IO_PENDING)
            {
                CancelIoEx(result.write.get(), &connect);
                DWORD ignored{};
                GetOverlappedResult(result.write.get(), &connect, &ignored, TRUE);
            }
        });
        result.read.reset(CreateFileW(name.c_str(), GENERIC_READ, 0, nullptr, OPEN_EXISTING, SECURITY_SQOS_PRESENT | SECURITY_IDENTIFICATION, nullptr));
        if (!result.read)
            throw Error(1, L"internalError", "Cannot open private approval pipe.");
        if (error == ERROR_IO_PENDING)
        {
            DWORD ignored{};
            if (WaitForSingleObject(ready.get(), 2000) != WAIT_OBJECT_0 ||
                !GetOverlappedResult(result.write.get(), &connect, &ignored, FALSE))
                throw Error(1, L"internalError", "Approval pipe connection failed.");
        }
        ULONG clientPid = 0, serverPid = 0;
        if (!GetNamedPipeClientProcessId(result.write.get(), &clientPid) ||
            !GetNamedPipeServerProcessId(result.read.get(), &serverPid) ||
            clientPid != GetCurrentProcessId() || serverPid != GetCurrentProcessId())
            throw Error(1, L"internalError", "Approval pipe is not connected to its owner.");
        return result;
    }

    ApprovalWriter::ApprovalWriter(HANDLE pipe) :
        m_pipe(pipe), m_event(CreateEventW(nullptr, TRUE, FALSE, nullptr))
    {
        if (!m_event)
            throw Error(1, L"internalError", "Cannot create approval write event.");
    }

    ApprovalWriter::~ApprovalWriter()
    {
        if (m_pending)
        {
            CancelIoEx(m_pipe, &m_overlapped);
            DWORD ignored{};
            GetOverlappedResult(m_pipe, &m_overlapped, &ignored, TRUE);
        }
    }

    void ApprovalWriter::Start(const json::JsonObject& message)
    {
        if (m_pending)
            throw Error(1, L"confirmationFailed", "Approval write is already pending.");
        const auto bytes = winrt::to_string(message.Stringify());
        if (bytes.empty() || bytes.size() > MaxApprovalBytes)
            throw Error(1, L"confirmationFailed", "Approval frame exceeds its size limit.");
        const auto length = static_cast<DWORD>(bytes.size());
        m_frame.resize(sizeof(length) + bytes.size());
        memcpy(m_frame.data(), &length, sizeof(length));
        memcpy(m_frame.data() + sizeof(length), bytes.data(), bytes.size());
        ResetEvent(m_event.get());
        m_overlapped = {};
        m_overlapped.hEvent = m_event.get();
        m_pending = true;
        DWORD written{};
        if (!WriteFile(m_pipe, m_frame.data(), static_cast<DWORD>(m_frame.size()), &written, &m_overlapped) &&
            GetLastError() != ERROR_IO_PENDING)
        {
            m_pending = false;
            throw Error(1, L"confirmationRequired", "Approval channel is unavailable.");
        }
    }

    WriteState ApprovalWriter::Poll()
    {
        if (!m_pending)
            return WriteState::Complete;
        DWORD written{};
        if (!GetOverlappedResult(m_pipe, &m_overlapped, &written, FALSE))
        {
            if (GetLastError() == ERROR_IO_INCOMPLETE)
                return WriteState::Pending;
            m_pending = false;
            return WriteState::Closed;
        }
        m_pending = false;
        return written == m_frame.size() ? WriteState::Complete : WriteState::Closed;
    }

    std::optional<json::JsonObject> ApprovalReader::Poll()
    {
        if (m_closed)
            return std::nullopt;
        DWORD available{};
        if (!PeekNamedPipe(m_pipe, nullptr, 0, nullptr, &available, nullptr))
        {
            m_closed = true;
            if (!m_frame.empty())
                throw Error(1, L"confirmationFailed", "Incomplete approval frame.");
            return std::nullopt;
        }
        if (!available)
            return std::nullopt;
        const auto count = static_cast<DWORD>(std::min<size_t>({ available, m_expected - m_frame.size(), 4096 }));
        const auto offset = m_frame.size();
        m_frame.resize(offset + count);
        DWORD received{};
        if (!ReadFile(m_pipe, m_frame.data() + offset, count, &received, nullptr) || !received)
        {
            m_closed = true;
            throw Error(1, L"confirmationFailed", "Cannot read approval frame.");
        }
        m_frame.resize(offset + received);
        if (m_frame.size() != m_expected)
            return std::nullopt;
        if (m_expected == sizeof(DWORD))
        {
            DWORD length{};
            memcpy(&length, m_frame.data(), sizeof(length));
            if (!length || length > MaxApprovalBytes)
                throw Error(1, L"confirmationFailed", "Invalid approval frame size.");
            m_expected += length;
            return std::nullopt;
        }
        const std::string bytes(m_frame.data() + sizeof(DWORD), m_frame.size() - sizeof(DWORD));
        m_frame.clear();
        m_expected = sizeof(DWORD);
        try
        {
            return json::JsonObject::Parse(winrt::to_hstring(bytes));
        }
        catch (const winrt::hresult_error&)
        {
            throw Error(1, L"confirmationFailed", "Invalid approval JSON.");
        }
    }

    json::JsonObject ApprovalMessage(const wchar_t* type, const std::wstring& requestId)
    {
        json::JsonObject message;
        message.SetNamedValue(L"type", json::value(type));
        message.SetNamedValue(L"requestId", json::value(requestId));
        return message;
    }

    bool ValidApprovalId(const std::wstring& id)
    {
        return LauncherUiMessage::Parse(ApprovalMessage(L"warning-shown", id).Stringify().c_str()).has_value();
    }

    WorkerApproval::WorkerApproval(HANDLE requestWrite, HANDLE responseRead, std::function<bool()> canceled) :
        m_writer(requestWrite), m_reader(responseRead), m_canceled(std::move(canceled))
    {
    }

    bool WorkerApproval::Send(const json::JsonObject& message)
    {
        m_writer.Start(message);
        const auto deadline = GetTickCount64() + 2000;
        for (;;)
        {
            const auto state = m_writer.Poll();
            if (state != WriteState::Pending)
                return state == WriteState::Complete;
            if (m_canceled() || GetTickCount64() >= deadline)
                return false;
            Sleep(10);
        }
    }

    LaunchDecision WorkerApproval::Request(const std::wstring& name, const std::wstring& path, const std::wstring& arguments, const SignatureVerification::Result& result)
    {
        if (m_canceled())
            return LaunchDecision::Canceled;
        if (!m_available)
            return LaunchDecision::UiUnavailable;
        GUID guid{};
        winrt::check_hresult(CoCreateGuid(&guid));
        wchar_t id[40]{};
        StringFromGUID2(guid, id, ARRAYSIZE(id));
        const std::wstring requestId(id);
        if (!m_pending.Begin(requestId))
            return LaunchDecision::InvalidResponse;
        auto end = wil::scope_exit([&] { m_pending.End(); });
        try
        {
            auto request = ApprovalMessage(L"elevation-warning", requestId);
            request.SetNamedValue(L"appName", json::value(name));
            request.SetNamedValue(L"path", json::value(path));
            request.SetNamedValue(L"arguments", json::value(arguments));
            request.SetNamedValue(L"reason", json::value(result.Reason()));
            wchar_t status[11]{};
            swprintf_s(status, L"0x%08X", static_cast<unsigned int>(result.error));
            request.SetNamedValue(L"status", json::value(status));
            if (!Send(request))
            {
                m_available = false;
                return m_canceled() ? LaunchDecision::Canceled : LaunchDecision::UiUnavailable;
            }
            for (;;)
            {
                if (m_canceled())
                    m_pending.Cancel();
                if (const auto decision = m_pending.Evaluate())
                {
                    if (!Send(ApprovalMessage(L"dismiss-warning", requestId)))
                        m_available = false;
                    return *decision;
                }
                if (const auto message = m_reader.Poll())
                {
                    if (message->GetNamedString(L"requestId", L"") != requestId)
                    {
                        m_pending.Fail(LaunchDecision::InvalidResponse);
                        continue;
                    }
                    if (message->GetNamedString(L"type", L"") == L"confirmation-unavailable")
                    {
                        m_pending.Fail(LaunchDecision::UiUnavailable);
                        continue;
                    }
                    const auto reply = LauncherUiMessage::Parse(message->Stringify().c_str());
                    if (!reply || reply->type == LauncherUiMessage::Type::Cancel)
                        m_pending.Fail(LaunchDecision::InvalidResponse);
                    else if (reply->type == LauncherUiMessage::Type::WarningShown)
                    {
                        if (!m_pending.Acknowledge(reply->requestId))
                            m_pending.Fail(LaunchDecision::InvalidResponse);
                    }
                    else if (!m_pending.Complete(reply->requestId, reply->decision))
                        m_pending.Fail(LaunchDecision::InvalidResponse);
                }
                else if (m_reader.Closed())
                {
                    m_available = false;
                    m_pending.Fail(LaunchDecision::UiUnavailable);
                }
                Sleep(10);
            }
        }
        catch (const Error& error)
        {
            m_available = false;
            return m_canceled()                        ? LaunchDecision::Canceled :
                   error.code == L"confirmationFailed" ? LaunchDecision::InvalidResponse :
                                                         LaunchDecision::UiUnavailable;
        }
        catch (const winrt::hresult_error&)
        {
            m_available = false;
            return LaunchDecision::InvalidResponse;
        }
    }

    FrontendApproval::FrontendApproval(HANDLE requestRead, HANDLE responseWrite, ConsoleApproval& console, HANDLE cancelEvent) :
        m_reader(requestRead), m_writer(responseWrite), m_console(console), m_cancelEvent(cancelEvent)
    {
    }

    void FrontendApproval::Enqueue(const json::JsonObject& message)
    {
        if (m_outgoing.size() >= 2)
            throw Error(1, L"confirmationFailed", "Too many pending approval replies.");
        m_outgoing.push_back(message);
    }

    void FrontendApproval::Stop()
    {
        m_stopped = true;
        m_outgoing.clear();
        m_console.End();
    }

    void FrontendApproval::Poll()
    {
        if (m_stopped)
            return;
        if (WaitForSingleObject(m_cancelEvent, 0) == WAIT_OBJECT_0)
        {
            Stop();
            return;
        }
        if (m_writer.Poll() == WriteState::Closed)
            throw Error(1, L"confirmationRequired", "Worker confirmation channel closed.");
        if (m_writer.Idle() && !m_outgoing.empty())
        {
            m_writer.Start(m_outgoing.front());
            m_outgoing.pop_front();
        }
        if (const auto message = m_reader.Poll())
        {
            const auto id = std::wstring(message->GetNamedString(L"requestId", L""));
            const auto type = message->GetNamedString(L"type", L"");
            if (!ValidApprovalId(id))
                throw Error(1, L"confirmationFailed", "Invalid confirmation request ID.");
            if (type == L"dismiss-warning" && id == m_requestId)
            {
                m_console.End();
                m_requestId.clear();
                m_answered = false;
            }
            else if (type == L"elevation-warning" && m_requestId.empty())
            {
                m_requestId = id;
                m_answered = !m_console.Begin(*message);
                Enqueue(ApprovalMessage(m_answered ? L"confirmation-unavailable" : L"warning-shown", id));
            }
            else
                throw Error(1, L"confirmationFailed", "Unexpected confirmation request.");
        }
        else if (m_reader.Closed())
        {
            Stop();
            return;
        }
        if (!m_requestId.empty() && !m_answered)
        {
            if (const auto decision = m_console.Poll())
            {
                m_answered = true;
                m_console.End();
                if (*decision == LaunchDecision::Canceled)
                {
                    SetEvent(m_cancelEvent);
                    Stop();
                    return;
                }
                auto reply = ApprovalMessage(*decision == LaunchDecision::UiUnavailable ?
                                                 L"confirmation-unavailable" :
                                                 L"elevation-response",
                                             m_requestId);
                if (*decision != LaunchDecision::UiUnavailable)
                    reply.SetNamedValue(L"choice", json::value(*decision == LaunchDecision::Approved ? L"run" : L"skip"));
                Enqueue(reply);
            }
        }
    }
}
