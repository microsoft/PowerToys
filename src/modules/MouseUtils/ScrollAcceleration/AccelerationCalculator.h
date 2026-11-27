#pragma once

#include <algorithm>
#include <cmath>
#include <cstdint>

namespace ScrollAcceleration
{
    struct AccelerationSettings
    {
        // Max gap between two wheel ticks (ms) for them to count as one continuous scroll.
        uint32_t timeThresholdMs = 100;
        // Multiplier added per consecutive fast tick (0 disables acceleration).
        double acceleration = 0.5;
        // Upper bound for the resulting multiplier.
        double maxMultiplier = 8.0;
        // Multiplier of a slow, isolated tick (streak 0). Below 1 makes single ticks very precise (fine scroll steps);
        // the acceleration then ramps up from this value as the wheel is spun faster.
        double baseMultiplier = 1.0;
    };

    // Pure logic (no Win32 dependencies) so it can be unit tested and fuzzed.
    class AccelerationCalculator
    {
    public:
        explicit AccelerationCalculator(AccelerationSettings settings = {}) :
            m_settings(settings)
        {
        }

        void SetSettings(const AccelerationSettings& settings)
        {
            m_settings = settings;
            Reset();
        }

        void Reset()
        {
            m_hasLast = false;
            m_streak = 0;
        }

        // Returns the multiplier for a wheel tick. `direction` is the sign of the delta,
        // `timeMs` a monotonic millisecond tick (wrap-around safe, e.g. MSLLHOOKSTRUCT::time).
        double NextMultiplier(int direction, uint32_t timeMs)
        {
            if (direction == 0)
            {
                return 1.0;
            }

            const int sign = direction > 0 ? 1 : -1;
            const uint32_t elapsed = timeMs - m_lastTimeMs; // unsigned subtraction handles wrap

            if (m_hasLast && sign == m_lastSign && elapsed <= m_settings.timeThresholdMs)
            {
                ++m_streak;
            }
            else
            {
                m_streak = 0;
            }

            m_hasLast = true;
            m_lastSign = sign;
            m_lastTimeMs = timeMs;

            // Sanitize hand-edited values: NaN / negative become the neutral value, huge or infinite values
            // are capped so that accel * streak can never turn into NaN (inf * 0) or overflow.
            constexpr double kLimit = 1.0e6;
            constexpr double kMinBase = 0.01;
            const double accel = m_settings.acceleration >= 0.0 ? (std::min)(m_settings.acceleration, kLimit) : 0.0;
            const double maxMul = m_settings.maxMultiplier >= 1.0 ? (std::min)(m_settings.maxMultiplier, kLimit) : 1.0;
            const double baseMul = m_settings.baseMultiplier >= kMinBase ? (std::min)(m_settings.baseMultiplier, kLimit) : 1.0;
            return (std::min)(baseMul + accel * static_cast<double>(m_streak), (std::max)(maxMul, baseMul));
        }

        // Convenience: scales a raw wheel delta (multiples of WHEEL_DELTA), clamped to int16 range
        // as required by MOUSEINPUT::mouseData for wheel events.
        int Scale(int delta, uint32_t timeMs)
        {
            const double multiplier = NextMultiplier(delta, timeMs);
            if (multiplier == 1.0)
            {
                return delta;
            }

            const double scaled = static_cast<double>(delta) * multiplier;
            const int result = static_cast<int>((std::clamp)(std::round(scaled), -32767.0, 32767.0));

            // A tick never disappears: keep at least one unit in the direction of the wheel.
            if (result == 0 && delta != 0)
            {
                return delta > 0 ? 1 : -1;
            }

            return result;
        }

    private:
        AccelerationSettings m_settings;
        bool m_hasLast = false;
        int m_lastSign = 0;
        uint32_t m_lastTimeMs = 0;
        uint32_t m_streak = 0;
    };
}
