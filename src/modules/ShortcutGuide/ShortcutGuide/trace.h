#pragma once
#include "ShortcutGuideSettings.h"

#include <common/Telemetry/TraceBase.h>

class Trace : public telemetry::TraceBase
{
public:
    static void SendGuideSession(const __int64 duration_ms, const wchar_t* close_type) noexcept;
    static void SendSettings(ShortcutGuideSettings settings) noexcept;
};
