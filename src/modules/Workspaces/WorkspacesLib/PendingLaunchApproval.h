// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <chrono>
#include <condition_variable>
#include <mutex>
#include <optional>
#include <string>
#include "LaunchDecision.h"

class PendingLaunchApproval
{
public:
    using Clock = std::chrono::steady_clock;
    static constexpr auto DisplayTimeout = std::chrono::seconds(10);
    static constexpr auto HeartbeatTimeout = std::chrono::seconds(10);

    bool Begin(const std::wstring& requestId, Clock::time_point now = Clock::now())
    {
        std::lock_guard lock(m_mutex);
        if (m_canceled || !m_requestId.empty() || requestId.empty())
        {
            return false;
        }
        m_requestId = requestId;
        m_decision.reset();
        m_shown = false;
        m_started = now;
        m_lastHeartbeat = now;
        return true;
    }

    bool Acknowledge(const std::wstring& requestId, Clock::time_point now = Clock::now())
    {
        std::lock_guard lock(m_mutex);
        EvaluateLocked(now);
        if (!MatchesPending(requestId) || m_shown)
        {
            return false;
        }
        m_shown = true;
        m_lastHeartbeat = now;
        m_changed.notify_all();
        return true;
    }

    bool Heartbeat(const std::wstring& requestId, Clock::time_point now = Clock::now())
    {
        std::lock_guard lock(m_mutex);
        EvaluateLocked(now);
        if (!MatchesPending(requestId) || !m_shown)
        {
            return false;
        }
        m_lastHeartbeat = now;
        return true;
    }

    bool Complete(const std::wstring& requestId, LaunchDecision decision, Clock::time_point now = Clock::now())
    {
        std::lock_guard lock(m_mutex);
        EvaluateLocked(now);
        if (!MatchesPending(requestId) || !m_shown ||
            (decision != LaunchDecision::Approved && decision != LaunchDecision::Skipped))
        {
            return false;
        }
        m_decision = decision;
        m_changed.notify_all();
        return true;
    }

    std::optional<LaunchDecision> Evaluate(Clock::time_point now = Clock::now())
    {
        std::lock_guard lock(m_mutex);
        return EvaluateLocked(now);
    }

    std::optional<LaunchDecision> WaitFor(std::chrono::milliseconds duration)
    {
        std::unique_lock lock(m_mutex);
        if (const auto result = EvaluateLocked(Clock::now()))
        {
            return result;
        }
        m_changed.wait_for(lock, duration, [&] { return m_canceled || m_decision.has_value(); });
        return EvaluateLocked(Clock::now());
    }

    void Fail(LaunchDecision failure)
    {
        std::lock_guard lock(m_mutex);
        if (!m_requestId.empty() && !m_decision &&
            (failure == LaunchDecision::UiUnavailable || failure == LaunchDecision::TimedOut || failure == LaunchDecision::InvalidResponse))
        {
            m_decision = failure;
            m_changed.notify_all();
        }
    }

    void End()
    {
        std::lock_guard lock(m_mutex);
        m_requestId.clear();
        m_decision.reset();
    }

    void Cancel()
    {
        std::lock_guard lock(m_mutex);
        m_canceled = true;
        m_changed.notify_all();
    }

    bool IsCanceled() const
    {
        std::lock_guard lock(m_mutex);
        return m_canceled;
    }

private:
    bool MatchesPending(const std::wstring& requestId) const
    {
        return !m_canceled && !m_requestId.empty() && m_requestId == requestId && !m_decision;
    }

    std::optional<LaunchDecision> EvaluateLocked(Clock::time_point now)
    {
        if (m_canceled)
        {
            return LaunchDecision::Canceled;
        }
        if (!m_requestId.empty() && !m_decision)
        {
            if (!m_shown && now - m_started >= DisplayTimeout)
            {
                m_decision = LaunchDecision::TimedOut;
            }
            else if (m_shown && now - m_lastHeartbeat >= HeartbeatTimeout)
            {
                m_decision = LaunchDecision::UiUnavailable;
            }
        }
        return m_decision;
    }

    mutable std::mutex m_mutex;
    std::condition_variable m_changed;
    std::wstring m_requestId;
    std::optional<LaunchDecision> m_decision;
    Clock::time_point m_started;
    Clock::time_point m_lastHeartbeat;
    bool m_shown{};
    bool m_canceled{};
};
