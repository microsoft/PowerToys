// Copyright (c) Microsoft Corporation. Licensed under the MIT license.
#pragma once

#include <thread>
#include <wil/resource.h>
#include "CliCommands.h"

namespace WorkspacesCli
{
    // Only used by operation-owned workers, never by launched applications.
    class OperationLifetime
    {
    public:
        OperationLifetime(DWORD parentPid, DWORD timeoutMs, HANDLE ownerLifetime = nullptr) :
            m_done(CreateEventW(nullptr, TRUE, FALSE, nullptr))
        {
            if (ownerLifetime)
            {
                if (!DuplicateHandle(GetCurrentProcess(), ownerLifetime, GetCurrentProcess(), m_parent.put(), SYNCHRONIZE, FALSE, 0))
                    throw Error(9, L"outcomeUnknown", "Cannot duplicate the operation owner's lifetime handle.");
            }
            else
                m_parent.reset(OpenProcess(SYNCHRONIZE, FALSE, parentPid));
            if (!m_done || !m_parent)
                throw Error(9, L"outcomeUnknown", "Cannot monitor the operation owner.");
            m_thread = std::thread([this, timeoutMs] {
                HANDLE handles[] = { m_done.get(), m_parent.get() };
                const auto wait = WaitForMultipleObjects(2, handles, FALSE, timeoutMs);
                if (wait != WAIT_OBJECT_0)
                    TerminateProcess(GetCurrentProcess(), 9);
            });
        }

        ~OperationLifetime()
        {
            SetEvent(m_done.get());
            if (m_thread.joinable())
                m_thread.join();
        }

        OperationLifetime(const OperationLifetime&) = delete;
        OperationLifetime& operator=(const OperationLifetime&) = delete;

    private:
        wil::unique_handle m_done;
        wil::unique_handle m_parent;
        std::thread m_thread;
    };
}
