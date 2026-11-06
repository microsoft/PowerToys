// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include "SeamlessCursorEngine.h"

#include <deque>
#include <mutex>
#include <optional>
#include <thread>
#include <vector>

namespace seamless_cursor
{
    // Owns the low-level mouse hook. The hook, the engine and the display-change window all live on one
    // dedicated thread that does nothing else, so the hook is never delayed by other work.
    class MouseHookThread
    {
    public:
        MouseHookThread() = default;
        MouseHookThread(const MouseHookThread&) = delete;
        MouseHookThread& operator=(const MouseHookThread&) = delete;
        ~MouseHookThread();

        bool Start();
        void Stop();

        // The requests below are applied on the hook thread in the order they were made.

        // Re-reads calibration.json, rebuilds the engine and resumes remapping.
        void ReloadCalibration();

        // Remaps with these calibrations instead of calibration.json, until the next reload.
        void Preview(std::vector<BorderCalibration> calibrations);

        // Lets every move pass through unchanged until the next preview or reload.
        void Suspend();

        // Applied right away when the thread is not running; otherwise like the requests above, with a reload.
        void SetOptions(EngineOptions options);

    private:
        struct Request
        {
            enum class Kind
            {
                Reload,
                Preview,
                Suspend,
                Options,
            };

            Kind kind = Kind::Reload;
            std::vector<BorderCalibration> calibrations;
            EngineOptions options;
        };

        void Post(Request request);
        void ProcessRequests();
        void Run(HANDLE ready);
        void Rebuild();
        void UpdateHook();
        void CheckHookAlive();

        static LRESULT CALLBACK MouseProc(int code, WPARAM wParam, LPARAM lParam);
        static LRESULT CALLBACK WindowProc(HWND window, UINT message, WPARAM wParam, LPARAM lParam);

        std::thread m_thread;
        HWND m_window = nullptr;
        std::mutex m_requestMutex;
        std::deque<Request> m_requests;

        // Only touched on the hook thread.
        HHOOK m_hook = nullptr;
        Engine m_engine;
        std::vector<Monitor> m_monitors;
        EngineOptions m_options;
        std::vector<BorderCalibration> m_calibrations; // The last calibration.json read; kept while a new read fails.
        bool m_readPending = true; // calibration.json was not read since the start or the last Reload.
        std::optional<std::vector<BorderCalibration>> m_preview; // Lines being adjusted; survives rebuilds.
        bool m_suspended = false;
        bool m_layoutStale = false; // From a display change until the rebuild after it.

        // Hook watchdog; only touched on the hook thread.
        unsigned int m_hookCalls = 0;
        unsigned int m_hookCallsAtLastCheck = 0;
        POINT m_cursorAtLastCheck{};
        bool m_reinstallLogged = false;
        bool m_installFailureLogged = false;
        bool m_identityFailureLogged = false;
        unsigned int m_retries = 0; // Rebuilds in a row that could not identify the monitors or read the calibration.
    };
}
