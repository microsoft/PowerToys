#pragma once

#include <algorithm>
#include <cmath>
#include <cstdint>

namespace ScrollAcceleration
{
    struct MomentumSettings
    {
        bool enabled = false;
        // Total time (ms) the scroll keeps coasting after the wheel stops.
        uint32_t durationMs = 600;
        // Quiet time (ms) after the last tick before coasting begins. Kept equal to the acceleration time threshold.
        uint32_t startDelayMs = 100;
    };

    // Pure logic (no Win32 dependencies) so it can be unit tested and fuzzed.
    // Feed it the wheel ticks that were delivered (OnTick) and poll Step() from a timer: after a fast, continuous
    // spin it returns decaying deltas that make the scroll glide to a stop (inertia).
    class MomentumCalculator
    {
    public:
        struct StepResult
        {
            int delta = 0; // wheel delta to inject now (may be 0)
            bool active = false; // true while the timer should keep running
        };

        explicit MomentumCalculator(MomentumSettings settings = {}) :
            m_settings(Sanitize(settings))
        {
        }

        void SetSettings(const MomentumSettings& settings)
        {
            m_settings = Sanitize(settings);
            Reset();
        }

        void Reset()
        {
            m_hasLast = false;
            m_lastSign = 0;
            m_lastTimeMs = 0;
            m_fastCount = 0;
            m_velocity = 0.0;
            m_coasting = false;
            m_remainder = 0.0;
        }

        // Stops any coasting in progress (a new physical tick, key press...). Keeps no velocity.
        void Cancel()
        {
            m_fastCount = 0;
            m_velocity = 0.0;
            m_coasting = false;
            m_remainder = 0.0;
        }

        // Records a wheel tick that was delivered to the application (after acceleration).
        void OnTick(int delta, uint32_t timeMs)
        {
            if (!m_settings.enabled || delta == 0)
            {
                return;
            }

            const int sign = delta > 0 ? 1 : -1;
            const uint32_t elapsed = timeMs - m_lastTimeMs;

            if (m_hasLast && sign == m_lastSign && elapsed <= m_settings.startDelayMs)
            {
                ++m_fastCount;
                const double magnitude = std::abs(static_cast<double>(delta));
                const double instant = magnitude / static_cast<double>((std::max)(elapsed, 1u));
                m_velocity = m_fastCount == 1 ? instant : 0.5 * m_velocity + 0.5 * instant;
                m_velocity = (std::min)(m_velocity, kMaxVelocity);
            }
            else
            {
                m_fastCount = 0;
                m_velocity = 0.0;
            }

            m_coasting = false;
            m_remainder = 0.0;
            m_hasLast = true;
            m_lastSign = sign;
            m_lastTimeMs = timeMs;
        }

        // Call periodically with the current monotonic millisecond tick.
        StepResult Step(uint32_t nowMs)
        {
            StepResult result;
            if (!m_settings.enabled || !m_hasLast || m_fastCount < kMinFastTicks)
            {
                return result;
            }

            if (!m_coasting)
            {
                if (SignedElapsed(nowMs, m_lastTimeMs) < m_settings.startDelayMs)
                {
                    result.active = true; // still waiting to see whether the wheel keeps spinning
                    return result;
                }

                m_coasting = true;
                m_coastStartMs = nowMs;
                m_lastStepMs = nowMs;
                m_startVelocity = m_velocity;
                m_remainder = 0.0;
                result.active = true;
                return result;
            }

            const int64_t t = SignedElapsed(nowMs, m_coastStartMs);
            const double tau = static_cast<double>(m_settings.durationMs) / 4.0;
            const double velocity = m_startVelocity * std::exp(-static_cast<double>(t) / tau);
            if (t >= static_cast<int64_t>(m_settings.durationMs) || velocity < kMinVelocity)
            {
                Cancel();
                return result;
            }

            const int64_t dt = SignedElapsed(nowMs, m_lastStepMs);
            m_lastStepMs = nowMs;

            const double amount = velocity * static_cast<double>(dt) * static_cast<double>(m_lastSign) + m_remainder;
            const double whole = std::trunc((std::clamp)(amount, -32767.0, 32767.0));
            m_remainder = amount - whole;
            result.delta = static_cast<int>(whole);
            result.active = true;
            return result;
        }

        bool IsCoasting() const { return m_coasting; }

    private:
        static constexpr int kMinFastTicks = 2; // three quick ticks in a row before inertia kicks in
        static constexpr double kMinVelocity = 0.02; // wheel units per ms below which coasting stops
        static constexpr double kMaxVelocity = 100.0;

        static constexpr MomentumSettings Sanitize(MomentumSettings s)
        {
            s.durationMs = (std::clamp)(s.durationMs, 50u, 10000u);
            s.startDelayMs = (std::clamp)(s.startDelayMs, 1u, 1000u);
            return s;
        }

        // Signed difference that tolerates the two time sources being a few ms apart and 32-bit wrap-around.
        static constexpr int64_t SignedElapsed(uint32_t later, uint32_t earlier)
        {
            const int32_t diff = static_cast<int32_t>(later - earlier);
            return diff < 0 ? 0 : diff;
        }

        MomentumSettings m_settings;
        bool m_hasLast = false;
        int m_lastSign = 0;
        uint32_t m_lastTimeMs = 0;
        int m_fastCount = 0;
        double m_velocity = 0.0;

        bool m_coasting = false;
        uint32_t m_coastStartMs = 0;
        uint32_t m_lastStepMs = 0;
        double m_startVelocity = 0.0;
        double m_remainder = 0.0;
    };
}
