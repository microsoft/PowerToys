#pragma once

#include <chrono>
#include <condition_variable>
#include <cstddef>
#include <functional>
#include <mutex>
#include <optional>
#include <stop_token>
#include <thread>
#include <utility>

namespace advanced_paste
{
    // One active request and at most one pending request, with repeats coalesced.
    class HotkeyWork
    {
    public:
        ~HotkeyWork()
        {
            stop();
        }

        void start()
        {
            std::lock_guard lock{ m_mutex };
            if (m_accepting)
            {
                return;
            }

            m_accepting = true;
            m_worker = std::jthread([this](std::stop_token token) {
                std::unique_lock lock{ m_mutex };
                while (m_ready.wait(lock, token, [this] { return m_pending.has_value(); }))
                {
                    if (token.stop_requested())
                    {
                        break;
                    }

                    auto [id, work] = std::move(*m_pending);
                    m_pending.reset();
                    m_active = id;
                    lock.unlock();
                    work(token);
                    lock.lock();
                    m_active.reset();
                }
            });
        }

        bool submit(size_t id, std::function<void(std::stop_token)> work)
        {
            std::lock_guard lock{ m_mutex };
            if (!m_accepting || m_pending || m_active == id)
            {
                return false;
            }

            m_pending.emplace(id, std::move(work));
            m_ready.notify_one();
            return true;
        }

        void stop()
        {
            {
                std::lock_guard lock{ m_mutex };
                m_accepting = false;
                m_pending.reset();
                m_worker.request_stop();
            }

            m_ready.notify_all();
            if (m_worker.joinable())
            {
                m_worker.join();
            }

            std::lock_guard lock{ m_mutex };
            m_active.reset();
        }

    private:
        std::mutex m_mutex;
        std::condition_variable_any m_ready;
        std::optional<std::pair<size_t, std::function<void(std::stop_token)>>> m_pending;
        std::optional<size_t> m_active;
        bool m_accepting = false;
        std::jthread m_worker;
    };

    template<typename IsCurrent, typename ModifiersDown>
    bool wait_for_modifiers_release(std::stop_token token, IsCurrent is_current, ModifiersDown modifiers_down, std::chrono::milliseconds timeout = std::chrono::milliseconds(1000))
    {
        const auto deadline = std::chrono::steady_clock::now() + timeout;
        while (!token.stop_requested() && is_current())
        {
            if (!modifiers_down())
            {
                return true;
            }
            if (std::chrono::steady_clock::now() >= deadline)
            {
                return false;
            }
            std::this_thread::sleep_for(std::chrono::milliseconds(10));
        }
        return false;
    }
}
