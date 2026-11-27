#pragma once

#include <cstdint>

// Counts events and says when the count may be written to the log: at most once per interval.
// It carries a count only, so a failure that repeats on every keystroke can be made visible without
// logging anything about the keystrokes themselves. Not thread safe; use it from one thread.
class RateLimitedCount
{
public:
    explicit RateLimitedCount(uint64_t intervalMs) noexcept :
        m_intervalMs(intervalMs)
    {
    }

    // Records one event at time nowMs (any monotonic millisecond clock). Returns the number of events
    // to report now, or 0 to stay quiet. The first event is reported at once; later ones are added
    // up and reported together with the first event that arrives after the interval has passed.
    uint64_t Add(uint64_t nowMs) noexcept
    {
        ++m_count;
        if (m_reportedOnce && nowMs - m_lastReportMs < m_intervalMs)
        {
            return 0;
        }

        const uint64_t toReport = m_count;
        m_count = 0;
        m_lastReportMs = nowMs;
        m_reportedOnce = true;
        return toReport;
    }

private:
    uint64_t m_intervalMs;
    uint64_t m_count = 0;
    uint64_t m_lastReportMs = 0;
    bool m_reportedOnce = false;
};
