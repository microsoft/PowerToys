// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <chrono>
#include <cstdio>
#include <mutex>
#include <set>
#include <string>
#include <string_view>
#include <utility>

// Module hotkey actions run in FIFO order on the runner's main thread (the hotkey worker).
// The low-level keyboard hook waits up to SlowThreshold for an action before it releases
// the key press, so a slow action no longer stalls input, but it still delays later
// hotkeys, the tray icon and Settings. This watchdog times each action and reports slow
// ones.
namespace SlowHotkeyWatchdog
{
    using Clock = std::chrono::steady_clock;

    inline constexpr std::chrono::milliseconds SlowThreshold{ 50 };

    constexpr bool IsSlow(Clock::duration elapsed) noexcept
    {
        return elapsed > SlowThreshold;
    }

    // Layout-independent description, e.g. "win+ctrl+shift+alt+VK 0x56".
    inline std::wstring DescribeHotkey(bool win, bool ctrl, bool shift, bool alt, unsigned int vkCode)
    {
        std::wstring result;
        if (win)
        {
            result += L"win+";
        }

        if (ctrl)
        {
            result += L"ctrl+";
        }

        if (shift)
        {
            result += L"shift+";
        }

        if (alt)
        {
            result += L"alt+";
        }

        wchar_t key[16]{};
        swprintf_s(key, L"VK 0x%02X", vkCode);
        result += key;
        return result;
    }

    // Tracks which (module, hotkey) pairs were already reported, so each one is reported
    // at most once per runner session. Only used on the slow path.
    class ReportLimiter
    {
    public:
        bool TryMarkReported(const std::wstring& moduleName, const std::wstring& hotkeyName)
        {
            std::scoped_lock lock{ m_mutex };
            return m_reported.emplace(moduleName, hotkeyName).second;
        }

    private:
        std::mutex m_mutex;
        std::set<std::pair<std::wstring, std::wstring>> m_reported;
    };

    // Logs a warning and emits telemetry for a slow hotkey action, at most once per
    // (module, hotkey) per runner session. Call only when IsSlow(elapsed) is true.
    void ReportSlowAction(std::wstring_view dispatchPath, const std::wstring& moduleName, const std::wstring& hotkeyName, Clock::duration elapsed) noexcept;
}
