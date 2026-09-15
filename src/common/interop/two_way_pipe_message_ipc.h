#pragma once
#include <functional>
#include "pipe_caller_auth.h"

namespace two_way_pipe_message_ipc
{
    // Outbound clients must never grant a server an impersonation-capable token.
    inline constexpr DWORD ClientOpenFlags = FILE_FLAG_OVERLAPPED | SECURITY_SQOS_PRESENT | SECURITY_IDENTIFICATION;
}

#ifdef TWO_WAY_PIPE_MESSAGE_IPC_TESTS
namespace two_way_pipe_message_ipc_test
{
    void FailThreadStartAfter(int successful_starts);
    void SetWaitNamedPipeEnteredEvent(HANDLE event);
    void SetHandlerCompletionEvents(HANDLE completed_event, HANDLE allow_return_event);
    void SetBeforeReplacementListenerEvents(HANDLE reached_event, HANDLE allow_creation_event);
    void SetAfterReplacementListenerEvents(HANDLE reached_event, HANDLE allow_continue_event);
    void FailHandlerThreadStartAfter(int successful_starts);
    void SetHandlerThreadStartAttemptEvent(HANDLE event);
    void SetOutputWritePendingEvent(HANDLE event);
    void ResetFaultInjection();
}
#endif

class TwoWayPipeMessageIPC
{
public:
    typedef std::function<void(const std::wstring&)>callback_function;
    TwoWayPipeMessageIPC(
        std::wstring _input_pipe_name,
        std::wstring _output_pipe_name,
        callback_function p_func);
    ~TwoWayPipeMessageIPC();
    void send(std::wstring msg);
    void start(HANDLE _restricted_pipe_token);
    // Overload that authenticates every connecting client before dispatch (fail-closed). Used by the
    // Runner for its privileged server pipes; the existing start(HANDLE) keeps the gate disabled.
    void start(HANDLE _restricted_pipe_token, const interop_auth::CallerPolicy& caller_policy);
    void end();

private:
    class TwoWayPipeMessageIPCImpl;
    TwoWayPipeMessageIPCImpl* impl;
};