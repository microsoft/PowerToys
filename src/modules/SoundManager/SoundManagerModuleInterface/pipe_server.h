#pragma once

#include <windows.h>

#include <atomic>
#include <mutex>
#include <string>
#include <thread>
#include <vector>

#include "audio_engine.h"

namespace SoundManager
{
    // Serves the mixer state. Every message on the pipe is a single JSON document on its own line.
    class PipeServer
    {
    public:
        explicit PipeServer(std::wstring pipeName);
        ~PipeServer();

        PipeServer(const PipeServer&) = delete;
        PipeServer& operator=(const PipeServer&) = delete;

        void Start();
        void Stop();

    private:
        void Serve();
        void HandleClient(HANDLE pipe, AudioEngine& engine);
        void HandleCommand(HANDLE pipe, AudioEngine& engine, const std::string& command);
        void ServeClient(HANDLE pipe, AudioEngine& engine);
        void ReapFinishedClients();
        void WakeUp();
        std::string BuildSnapshot(AudioEngine& engine);
        std::string BuildPeaks(AudioEngine& engine);

        std::wstring m_pipeName;
        std::atomic<bool> m_running{ false };
        std::thread m_thread;

        // The clients that are connected. Every one of them is served on its own thread.
        std::mutex m_clientMutex;
        std::vector<std::thread> m_clientThreads;
    };
}
