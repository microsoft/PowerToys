#include "pch.h"
#include "slow_hotkey_watchdog.h"
#include "trace.h"

#include <common/logger/logger.h>

namespace SlowHotkeyWatchdog
{
    void ReportSlowAction(std::wstring_view dispatchPath, const std::wstring& moduleName, const std::wstring& hotkeyName, Clock::duration elapsed) noexcept
    {
        try
        {
            static ReportLimiter limiter;
            if (!limiter.TryMarkReported(moduleName, hotkeyName))
            {
                return;
            }

            const auto durationMs = std::chrono::duration_cast<std::chrono::milliseconds>(elapsed).count();
            Logger::warn(L"Slow hotkey action: {} ({}) took {} ms via {} (threshold {} ms). Hotkey actions run in order on the runner's main thread and delay later hotkeys while they run.",
                         moduleName,
                         hotkeyName,
                         durationMs,
                         dispatchPath,
                         SlowThreshold.count());
            Trace::SlowHotkeyAction(moduleName, dispatchPath, durationMs);
        }
        catch (...)
        {
        }
    }
}
