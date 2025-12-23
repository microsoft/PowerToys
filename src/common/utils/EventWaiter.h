#pragma once

#include <functional>
#include <thread>
#include <string>
#include <atomic>
#include <windows.h>

/// <summary>
/// A reusable utility class that listens for a named Windows event and invokes a callback when triggered.
/// Provides RAII-based resource management for event handles and the listener thread.
/// The thread is properly joined on destruction to ensure clean shutdown.
/// </summary>
class EventWaiter
{
public:
    EventWaiter() = default;

    EventWaiter(const EventWaiter&) = delete;
    EventWaiter& operator=(const EventWaiter&) = delete;
    EventWaiter(EventWaiter&&) = delete;
    EventWaiter& operator=(EventWaiter&&) = delete;

    ~EventWaiter()
    {
        stop();
    }

    /// <summary>
    /// Starts listening for the specified named event. When the event is signaled, the callback is invoked.
    /// </summary>
    /// <param name="name">The name of the Windows event to listen for.</param>
    /// <param name="callback">The callback function to invoke when the event is triggered. Receives ERROR_SUCCESS on success.</param>
    /// <returns>true if listening started successfully, false otherwise.</returns>
    bool start(const std::wstring& name, std::function<void(DWORD)> callback)
    {
        if (m_listening)
        {
            return false;
        }

        m_exitThreadEvent = CreateEventW(nullptr, false, false, nullptr);
        m_waitingEvent = CreateEventW(nullptr, false, false, name.c_str());

        if (!m_exitThreadEvent || !m_waitingEvent)
        {
            cleanup();
            return false;
        }

        m_listening = true;
        m_eventThread = std::thread([this, cb = std::move(callback)]() {
            HANDLE events[2] = { m_waitingEvent, m_exitThreadEvent };
            while (m_listening)
            {
                auto waitResult = WaitForMultipleObjects(2, events, false, INFINITE);
                if (!m_listening)
                {
                    break;
                }

                if (waitResult == WAIT_OBJECT_0 + 1)
                {
                    // Exit event signaled
                    break;
                }

                if (waitResult == WAIT_FAILED)
                {
                    cb(GetLastError());
                    continue;
                }

                if (waitResult == WAIT_OBJECT_0)
                {
                    cb(ERROR_SUCCESS);
                }
            }
        });

        return true;
    }

    /// <summary>
    /// Stops listening for the event and cleans up resources.
    /// Waits for the listener thread to finish before returning.
    /// Safe to call multiple times.
    /// </summary>
    void stop()
    {
        m_listening = false;
        if (m_exitThreadEvent)
        {
            SetEvent(m_exitThreadEvent);
        }
        if (m_eventThread.joinable())
        {
            m_eventThread.join();
        }
        cleanup();
    }

    /// <summary>
    /// Returns whether the listener is currently active.
    /// </summary>
    bool is_listening() const
    {
        return m_listening;
    }

private:
    void cleanup()
    {
        if (m_exitThreadEvent)
        {
            CloseHandle(m_exitThreadEvent);
            m_exitThreadEvent = nullptr;
        }
        if (m_waitingEvent)
        {
            CloseHandle(m_waitingEvent);
            m_waitingEvent = nullptr;
        }
    }

    HANDLE m_exitThreadEvent = nullptr;
    HANDLE m_waitingEvent = nullptr;
    std::thread m_eventThread;
    std::atomic_bool m_listening{ false };
};