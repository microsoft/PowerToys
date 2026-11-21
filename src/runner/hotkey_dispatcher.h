// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <algorithm>
#include <chrono>
#include <condition_variable>
#include <cstdint>
#include <deque>
#include <exception>
#include <functional>
#include <memory>
#include <mutex>
#include <string>
#include <utility>

// Queue between the low-level keyboard hook thread, which only decides whether a hotkey
// is handled, and the single worker that runs module hotkey actions. Actions run in FIFO
// order, one at a time, on the thread that calls DrainPending() (the runner's main
// thread). An action that throws doesn't stop the actions queued after it.
namespace HotkeyDispatch
{
    using Clock = std::chrono::steady_clock;

    // Identifies a queued action in diagnostics. Built when the hotkey is registered, so
    // the hook thread only copies a shared pointer.
    struct HotkeyLabel
    {
        std::wstring dispatchPath;
        std::wstring moduleName;
        std::wstring hotkeyName;
    };

    struct WorkItem
    {
        std::function<void()> action;
        std::shared_ptr<const HotkeyLabel> label;
    };

    class Dispatcher
    {
    public:
        using Ticket = std::uint64_t;
        static constexpr Ticket InvalidTicket = 0;

        // Asks the worker to call DrainPending(). Called on the enqueuing thread, outside
        // the queue lock. Must be fast and must not block.
        using WakeCallback = std::function<void()>;

        // Called on the worker after each action, with the time the action took and the
        // exception it threw (if any).
        using CompletionObserver = std::function<void(const WorkItem&, Clock::duration, std::exception_ptr)>;

        // Accepts new work. Call before the producers start.
        void Start(WakeCallback wake, CompletionObserver observer)
        {
            std::scoped_lock lock{ m_mutex };
            m_wake = std::move(wake);
            m_observer = std::move(observer);
            m_running = true;
        }

        // Rejects new work, drops the queued actions without running them and releases
        // every waiter. Call it before the objects the actions reference are destroyed.
        void Shutdown() noexcept
        {
            std::deque<Entry> dropped;
            {
                std::scoped_lock lock{ m_mutex };
                m_running = false;
                dropped.swap(m_pending);
                m_completed = m_lastIssued;
            }

            m_cv.notify_all();
        }

        // Thread-safe. Returns InvalidTicket if the dispatcher isn't running or the
        // action couldn't be queued.
        Ticket Enqueue(WorkItem item) noexcept
        {
            Ticket ticket = InvalidTicket;
            WakeCallback wake;
            try
            {
                std::scoped_lock lock{ m_mutex };
                if (!m_running)
                {
                    return InvalidTicket;
                }

                ticket = m_lastIssued + 1;
                m_pending.push_back({ ticket, std::move(item) });
                m_lastIssued = ticket;
                wake = m_wake;
            }
            catch (...)
            {
                return InvalidTicket;
            }

            if (wake)
            {
                try
                {
                    wake();
                }
                catch (...)
                {
                }
            }

            return ticket;
        }

        // Runs the queued actions on the calling thread until the queue is empty and
        // returns how many ran. Call it from the worker thread only. A nested call (an
        // action pumping messages) returns 0 so that actions never overlap.
        size_t DrainPending() noexcept
        {
            if (m_draining)
            {
                return 0;
            }

            m_draining = true;
            size_t ran = 0;
            for (;;)
            {
                Entry entry;
                CompletionObserver observer;
                {
                    std::scoped_lock lock{ m_mutex };
                    if (!m_running || m_pending.empty())
                    {
                        break;
                    }

                    entry = std::move(m_pending.front());
                    m_pending.pop_front();
                    observer = m_observer;
                }

                std::exception_ptr error;
                const auto start = Clock::now();
                try
                {
                    if (entry.item.action)
                    {
                        entry.item.action();
                    }
                }
                catch (...)
                {
                    error = std::current_exception();
                }

                const auto elapsed = Clock::now() - start;

                {
                    std::scoped_lock lock{ m_mutex };
                    m_completed = (std::max)(m_completed, entry.ticket);
                }

                m_cv.notify_all();
                ++ran;

                if (observer)
                {
                    try
                    {
                        observer(entry.item, elapsed, error);
                    }
                    catch (...)
                    {
                    }
                }
            }

            m_draining = false;
            return ran;
        }

        // Waits until the action for the ticket (and every action queued before it) ran,
        // up to the timeout. Returns false on timeout, after Shutdown() or for an invalid
        // ticket. Don't call it on the worker thread.
        bool WaitForCompletion(Ticket ticket, std::chrono::milliseconds timeout) noexcept
        {
            if (ticket == InvalidTicket)
            {
                return false;
            }

            std::unique_lock lock{ m_mutex };
            m_cv.wait_for(lock, timeout, [&] { return m_completed >= ticket || !m_running; });
            return m_running && m_completed >= ticket;
        }

        size_t PendingCount() const
        {
            std::scoped_lock lock{ m_mutex };
            return m_pending.size();
        }

    private:
        struct Entry
        {
            Ticket ticket = InvalidTicket;
            WorkItem item;
        };

        mutable std::mutex m_mutex;
        std::condition_variable m_cv;
        std::deque<Entry> m_pending;
        WakeCallback m_wake;
        CompletionObserver m_observer;
        Ticket m_lastIssued = InvalidTicket;
        Ticket m_completed = InvalidTicket;
        bool m_running = false;

        // Only touched by the worker thread.
        bool m_draining = false;
    };
}
