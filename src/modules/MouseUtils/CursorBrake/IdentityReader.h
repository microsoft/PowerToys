// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include "CursorBrakeEdges.h"

#include <windows.h>

#include <cstdint>
#include <mutex>
#include <optional>
#include <string>
#include <vector>

namespace cursorbrake
{
    // Reads monitor identities, names and display numbers on a thread-pool work item, because
    // DisplayUtils::GetDisplays and QueryDisplayConfig can stall during a display change and the
    // low-level hook thread must keep answering mouse input. A read takes the newest request when
    // it starts, reads run one at a time, and each finished read, a failed one included, leaves
    // its result for Take and posts the request's message to its window.
    class IdentityReader
    {
    public:
        IdentityReader();
        ~IdentityReader();
        IdentityReader(const IdentityReader&) = delete;
        IdentityReader& operator=(const IdentityReader&) = delete;

        // Queues a read for the monitors of one enumeration, by index: their handles and GDI
        // devices. Without a work item or a window to tell, the read runs on the calling thread
        // and nothing is posted. Returns whether it did, in which case any result is ready for
        // Take on return.
        bool Request(uint64_t generation, std::vector<HMONITOR> handles, std::vector<std::wstring> devices, HWND window, UINT message);

        // The newest finished read, if one is waiting.
        std::optional<Identities> Take();

        // Drops any queued read and waits for a running one, so no message is posted after it
        // returns.
        void Cancel();

        // Reads the identities of the monitors on the calling thread.
        static Identities Read(uint64_t generation, const std::vector<HMONITOR>& handles, const std::vector<std::wstring>& devices);

    private:
        struct Pending
        {
            uint64_t generation = 0;
            std::vector<HMONITOR> handles;
            std::vector<std::wstring> devices;
            HWND window = nullptr;
            UINT message = 0;
        };

        static void CALLBACK Run(PTP_CALLBACK_INSTANCE instance, PVOID context, PTP_WORK work);
        void ReadPending();

        // Read, or an incomplete result with no monitors when it throws, logged.
        static Identities TryRead(uint64_t generation, const std::vector<HMONITOR>& handles, const std::vector<std::wstring>& devices);

        std::mutex m_mutex;
        std::optional<Pending> m_pending;
        std::optional<Identities> m_result;
        std::mutex m_readMutex;
        PTP_WORK m_work = nullptr;
    };
}
