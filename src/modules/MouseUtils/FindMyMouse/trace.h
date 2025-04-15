#pragma once

#include <common/Telemetry/TraceBase.h>

class Trace : public telemetry::TraceBase
{
public:
    // Log if the user has FindMyMouse enabled or disabled
    static void EnableFindMyMouse(const bool enabled) noexcept;

    // Log that the user activated the module by focusing the mouse pointer
    static void MousePointerFocused() noexcept;
};
