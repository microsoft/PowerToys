#pragma once
#include <Windows.h>
#include "async_message_queue.h"
#include <WinSafer.h>
#include <accctrl.h>
#include <aclapi.h>
#include <list>
#include "two_way_pipe_message_ipc.h"

class TwoWayPipeMessageIPC::TwoWayPipeMessageIPCImpl
{
public:
    void send(std::wstring msg);
    TwoWayPipeMessageIPCImpl(std::wstring _input_pipe_name, std::wstring _output_pipe_name, callback_function p_func);
    void start(HANDLE _restricted_pipe_token);
    void end();

private:
    AsyncMessageQueue input_queue;
    AsyncMessageQueue output_queue;
    std::wstring output_pipe_name;
    std::wstring input_pipe_name;
    std::thread input_queue_thread;
    std::thread output_queue_thread;
    std::thread input_pipe_thread;
    std::mutex pipe_connect_handle_mutex; // For manipulating the current_connect_pipe
    std::wstring outgoing_message; // Store the updated json settings.

    HANDLE current_connect_pipe_handle = NULL;
    bool closed = false;
    TwoWayPipeMessageIPC::callback_function dispatch_inc_message_function;

    void send_pipe_message(std::wstring message);
    void consume_output_queue_thread();
    BOOL GetLogonSID(HANDLE hToken, PSID* ppsid);
    VOID FreeLogonSID(PSID* ppsid);
    int change_pipe_security_allow_restricted_token(HANDLE handle, HANDLE token);
    HANDLE create_medium_integrity_token();
    void handle_pipe_connection(HANDLE input_pipe_handle);
    void start_named_pipe_server(HANDLE token);
    void consume_input_queue_thread();
};
