// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once
#include <windows.h>
#include <atomic>
#include <thread>

namespace WindowTabs
{
    class Service
    {
    public:
        ~Service();
        bool Start(HINSTANCE instance);
        void Stop();
        bool Cycle(bool forward);
        void SetRestore(bool enabled) { m_restore = enabled; }

    private:
        std::thread m_thread;
        std::atomic<HWND> m_dispatcher = nullptr;
        std::atomic<bool> m_restore = true;
    };
}
