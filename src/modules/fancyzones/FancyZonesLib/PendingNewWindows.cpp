#include "pch.h"
#include "PendingNewWindows.h"

PendingNewWindows::PendingNewWindows(unsigned long long settleDelayMillis) noexcept :
    m_settleDelayMillis(settleDelayMillis)
{
}

void PendingNewWindows::Add(HWND window, unsigned long long now) noexcept
{
    m_windows[window] = now;
}

void PendingNewWindows::Remove(HWND window) noexcept
{
    m_windows.erase(window);
}

std::vector<HWND> PendingNewWindows::TakeSettled(unsigned long long now) noexcept
{
    std::vector<HWND> settled;
    for (auto iter = m_windows.begin(); iter != m_windows.end();)
    {
        if (now - iter->second >= m_settleDelayMillis)
        {
            settled.push_back(iter->first);
            iter = m_windows.erase(iter);
        }
        else
        {
            ++iter;
        }
    }

    return settled;
}

std::vector<HWND> PendingNewWindows::TakeAll() noexcept
{
    std::vector<HWND> windows;
    windows.reserve(m_windows.size());
    for (const auto& [window, _] : m_windows)
    {
        windows.push_back(window);
    }

    m_windows.clear();
    return windows;
}

std::optional<unsigned long long> PendingNewWindows::NextSettleDelay(unsigned long long now) const noexcept
{
    std::optional<unsigned long long> result;
    for (const auto& [_, lastEventTime] : m_windows)
    {
        const auto elapsed = now - lastEventTime;
        const unsigned long long remaining = elapsed >= m_settleDelayMillis ? 0ULL : m_settleDelayMillis - elapsed;
        if (!result.has_value() || remaining < *result)
        {
            result = remaining;
        }
    }

    return result;
}

bool PendingNewWindows::Empty() const noexcept
{
    return m_windows.empty();
}
