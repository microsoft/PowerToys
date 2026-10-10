#pragma once

#include <optional>
#include <unordered_map>
#include <vector>

// Tracks newly created/shown windows until they have settled.
// Some applications show a window for a few milliseconds and hide it again during startup
// (e.g. 3ds Max's MAXScript Debugger). Acting on such a window immediately makes FancyZones
// snap a window that is about to disappear, and the asynchronous placement can show it again.
class PendingNewWindows
{
public:
    explicit PendingNewWindows(unsigned long long settleDelayMillis) noexcept;

    // Adds the window or restarts its settle period if it's already pending.
    void Add(HWND window, unsigned long long now) noexcept;
    void Remove(HWND window) noexcept;

    // Removes and returns the windows whose settle period has elapsed.
    std::vector<HWND> TakeSettled(unsigned long long now) noexcept;
    std::vector<HWND> TakeAll() noexcept;

    // Time until the next pending window settles, or nullopt if nothing is pending.
    std::optional<unsigned long long> NextSettleDelay(unsigned long long now) const noexcept;

    bool Empty() const noexcept;

private:
    const unsigned long long m_settleDelayMillis;
    std::unordered_map<HWND, unsigned long long> m_windows; // window -> time of the last event
};
