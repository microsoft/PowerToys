#pragma once

#include <common/Telemetry/TraceBase.h>

class Trace : public telemetry::TraceBase
{
public:
    static void EnableHotkeyLauncher(bool enabled) noexcept;
    static void HotkeyLauncherLaunchAction(int hotkeyId) noexcept;
};
