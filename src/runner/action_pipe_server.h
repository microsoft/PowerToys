#pragma once

#include <atomic>
#include <thread>

class RunnerActionPipeServer
{
public:
    RunnerActionPipeServer() = default;
    ~RunnerActionPipeServer();

    void Start();
    void Stop();

private:
    std::atomic<bool> _stop_requested{ false };
    std::thread _server_thread;

    void Run();
};
