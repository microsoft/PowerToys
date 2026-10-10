// Copyright (c) Microsoft Corporation. Licensed under the MIT license.
#pragma once

#include <deque>
#include <functional>
#include <optional>
#include <string>
#include <vector>
#include <wil/resource.h>
#include <WorkspacesLib/LauncherUiMessage.h>
#include <WorkspacesLib/PendingLaunchApproval.h>
#include <WorkspacesLib/SignatureVerification.h>

namespace WorkspacesCli
{
    inline constexpr DWORD MaxApprovalBytes = 64 * 1024;

    struct ApprovalPipe
    {
        wil::unique_handle read;
        wil::unique_handle write;
        static ApprovalPipe Create();
    };

    enum class WriteState
    {
        Pending,
        Complete,
        Closed,
    };

    // Only the connected handles are inherited; no child discovers a public pipe by name.
    class ApprovalWriter
    {
    public:
        explicit ApprovalWriter(HANDLE pipe);
        ~ApprovalWriter();
        ApprovalWriter(const ApprovalWriter&) = delete;
        ApprovalWriter& operator=(const ApprovalWriter&) = delete;
        void Start(const json::JsonObject& message);
        WriteState Poll();
        bool Idle() const { return !m_pending; }

    private:
        HANDLE m_pipe;
        wil::unique_handle m_event;
        OVERLAPPED m_overlapped{};
        std::vector<char> m_frame;
        bool m_pending = false;
    };

    class ApprovalReader
    {
    public:
        explicit ApprovalReader(HANDLE pipe) : m_pipe(pipe) {}
        std::optional<json::JsonObject> Poll();
        bool Closed() const { return m_closed; }

    private:
        HANDLE m_pipe;
        std::vector<char> m_frame;
        size_t m_expected = sizeof(DWORD);
        bool m_closed = false;
    };

    json::JsonObject ApprovalMessage(const wchar_t* type, const std::wstring& requestId);
    bool ValidApprovalId(const std::wstring& id);

    class WorkerApproval
    {
    public:
        WorkerApproval(HANDLE requestWrite, HANDLE responseRead, std::function<bool()> canceled);
        LaunchDecision Request(const std::wstring& name, const std::wstring& path, const std::wstring& arguments, const SignatureVerification::Result& result);

    private:
        bool Send(const json::JsonObject& message);
        ApprovalWriter m_writer;
        ApprovalReader m_reader;
        std::function<bool()> m_canceled;
        PendingLaunchApproval m_pending;
        bool m_available = true;
    };

    class ConsoleApproval;

    class FrontendApproval
    {
    public:
        FrontendApproval(HANDLE requestRead, HANDLE responseWrite, ConsoleApproval& console, HANDLE cancelEvent);
        void Poll();
        void Stop();

    private:
        void Enqueue(const json::JsonObject& message);
        ApprovalReader m_reader;
        ApprovalWriter m_writer;
        ConsoleApproval& m_console;
        HANDLE m_cancelEvent;
        std::deque<json::JsonObject> m_outgoing;
        std::wstring m_requestId;
        bool m_answered = false;
        bool m_stopped = false;
    };
}
