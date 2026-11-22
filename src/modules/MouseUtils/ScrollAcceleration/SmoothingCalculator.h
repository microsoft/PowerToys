#pragma once

#include <algorithm>
#include <cmath>
#include <cstdint>

namespace ScrollAcceleration
{
    struct SmoothingSettings
    {
        bool enabled = true;
        // Time (ms) a wheel tick takes to be fully delivered. Longer is smoother but less immediate.
        uint32_t durationMs = 120;
    };

    // Pure logic (no Win32 dependencies) so it can be unit tested and fuzzed.
    // Instead of delivering a (possibly large) wheel delta in one jump, the delta is queued and handed out
    // in many small steps from a timer (ease-out), so the content glides instead of jumping.
    class SmoothingCalculator
    {
    public:
        struct StepResult
        {
            int delta = 0; // wheel delta to inject now (may be 0)
            bool active = false; // true while the timer should keep running
        };

        explicit SmoothingCalculator(SmoothingSettings settings = {}) :
            m_settings(Sanitize(settings))
        {
        }

        void SetSettings(const SmoothingSettings& settings)
        {
            m_settings = Sanitize(settings);
            Reset();
        }

        void Reset()
        {
            m_pending = 0.0;
            m_carry = 0.0;
            m_lastStepMs = 0;
            m_active = false;
        }

        bool Enabled() const { return m_settings.enabled; }

        // True while queued delta has not been fully delivered yet.
        bool HasPending() const { return m_active; }

        // Queues a wheel delta. A tick in the opposite direction drops what is still queued, so reversing the
        // wheel reacts immediately instead of first finishing the previous glide.
        void Add(int delta, uint32_t nowMs)
        {
            if (!m_settings.enabled || delta == 0)
            {
                return;
            }

            const double queued = m_pending + m_carry;
            if (m_active && queued != 0.0 && ((delta > 0) != (queued > 0.0)))
            {
                m_pending = 0.0;
                m_carry = 0.0;
            }

            if (!m_active)
            {
                m_active = true;
                m_lastStepMs = nowMs;
            }

            m_pending += static_cast<double>(delta);
        }

        // Call periodically with the current monotonic millisecond tick.
        StepResult Step(uint32_t nowMs)
        {
            StepResult result;
            if (!m_active)
            {
                return result;
            }

            const double dt = static_cast<double>((std::min)(SignedElapsed(nowMs, m_lastStepMs), static_cast<int64_t>(250)));
            m_lastStepMs = nowMs;

            const double tau = static_cast<double>(m_settings.durationMs) / 4.0;
            const double fraction = 1.0 - std::exp(-dt / tau);
            const double emit = m_pending * fraction;
            m_pending -= emit;

            const double total = m_carry + emit;
            double whole = std::trunc((std::clamp)(total, -32767.0, 32767.0));
            m_carry = total - whole;

            if (std::abs(m_pending) < 1.0)
            {
                // Flush what is left so every queued unit is delivered exactly once.
                whole = std::round((std::clamp)(total + m_pending, -32767.0, 32767.0));
                m_pending = 0.0;
                m_carry = 0.0;
                m_active = false;
            }

            result.delta = static_cast<int>(whole);
            result.active = m_active;
            return result;
        }

    private:
        static constexpr SmoothingSettings Sanitize(SmoothingSettings s)
        {
            s.durationMs = (std::clamp)(s.durationMs, 20u, 1000u);
            return s;
        }

        static constexpr int64_t SignedElapsed(uint32_t later, uint32_t earlier)
        {
            const int32_t diff = static_cast<int32_t>(later - earlier);
            return diff < 0 ? 0 : diff;
        }

        SmoothingSettings m_settings;
        double m_pending = 0.0;
        double m_carry = 0.0;
        uint32_t m_lastStepMs = 0;
        bool m_active = false;
    };
}
