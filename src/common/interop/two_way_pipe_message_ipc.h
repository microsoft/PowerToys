#pragma once
class TwoWayPipeMessageIPC
{
public:
    typedef void (*callback_function)(const std::wstring&);
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