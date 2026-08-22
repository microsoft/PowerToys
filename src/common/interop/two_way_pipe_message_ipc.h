#pragma once
#include <functional>
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
    void end();

private:
    class TwoWayPipeMessageIPCImpl;
    TwoWayPipeMessageIPCImpl* impl;
};