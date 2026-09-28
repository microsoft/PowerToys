#pragma once

#include <common/Telemetry/TraceBase.h>

class Trace : public telemetry::TraceBase
{
public:
    // Log if the user has Context Menu Manager enabled or disabled
    static void EnableContextMenuManager(const bool enabled) noexcept;
};
