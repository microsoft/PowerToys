#pragma once
#include <atomic>
#include <condition_variable>
#include <memory>
#include <Windows.h>
#include "async_message_queue.h"
#include <WinSafer.h>
#include <accctrl.h>
#include <aclapi.h>
#include <vector>
#include "two_way_pipe_message_ipc.h"

class TwoWayPipeMessageIPC::TwoWayPipeMessageIPCImpl
{
public:
    void send(std::wstring msg);
    TwoWayPipeMessageIPCImpl(std::wstring _input_pipe_name, std::wstring _output_pipe_name, callback_function p_func);
    void start(HANDLE _restricted_pipe_token);
    void start(HANDLE _restricted_pipe_token, const interop_auth::CallerPolicy& _caller_policy);
    void end();

private:
    struct ConnectionHandler
    {
        explicit ConnectionHandler(HANDLE pipe) :
            pipe_handle(pipe)
        {
        }

        HANDLE pipe_handle = INVALID_HANDLE_VALUE;
        std::thread thread;
        bool completed = false;
    };

    enum class LifecycleState
    {
        NotStarted,
        Starting,
        Running,
        Stopping,
        Stopped,
    };

    struct PipeSecurityAttributes
    {
        SECURITY_DESCRIPTOR security_descriptor{};
        SECURITY_ATTRIBUTES attributes{};
        PACL dacl = nullptr;
        PSID logon_sid = nullptr;
        BYTE administrators_sid[SECURITY_MAX_SID_SIZE]{};
        BYTE local_system_sid[SECURITY_MAX_SID_SIZE]{};
        BYTE server_sid[SECURITY_MAX_SID_SIZE]{};

        ~PipeSecurityAttributes()
        {
            if (dacl)
            {
                LocalFree(dacl);
            }
            if (logon_sid)
            {
                HeapFree(GetProcessHeap(), 0, logon_sid);
            }
        }
    };

    AsyncMessageQueue input_queue;
    AsyncMessageQueue output_queue;
    std::wstring output_pipe_name;
    std::wstring input_pipe_name;
    std::thread input_queue_thread;
    std::thread output_queue_thread;
    std::thread input_pipe_thread;
    std::mutex lifecycle_mutex;
    std::condition_variable lifecycle_stopped;
    LifecycleState lifecycle_state = LifecycleState::NotStarted;
    std::mutex pipe_connect_handle_mutex; // For manipulating the current_connect_pipe
    std::mutex connection_handlers_mutex;
    std::vector<std::shared_ptr<ConnectionHandler>> connection_handlers;
    std::wstring outgoing_message; // Store the updated json settings.

    HANDLE current_connect_pipe_handle = NULL;
    std::atomic_bool closed = false;
    TwoWayPipeMessageIPC::callback_function dispatch_inc_message_function;
    interop_auth::CallerPolicy caller_policy;
    interop_auth::VerificationCache caller_cache;

    void send_pipe_message(std::wstring message);
    void consume_output_queue_thread();
    BOOL GetLogonSID(HANDLE hToken, PSID* ppsid);
    VOID FreeLogonSID(PSID* ppsid);
    bool create_pipe_security_attributes(HANDLE token, PipeSecurityAttributes& security_attributes);
    void start_threads(HANDLE token);
    void stop_started_threads();
    HANDLE create_medium_integrity_token();
    void handle_pipe_connection(const std::shared_ptr<ConnectionHandler>& handler);
    void finish_connection_handler(const std::shared_ptr<ConnectionHandler>& handler);
    bool start_connection_handler(HANDLE pipe_handle);
    void reap_finished_connection_handlers();
    void cancel_and_wait_for_connection_handlers();
    void start_named_pipe_server(HANDLE token);
    void consume_input_queue_thread();
};
