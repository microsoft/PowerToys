#pragma once

#include <common/Telemetry/TraceBase.h>

class Trace : public telemetry::TraceBase
{
public:
    // Log if the user has HostsFileEditor enabled or disabled
    static void EnableHostsFileEditor(const bool enabled) noexcept;

    // Log that the user tried to activate the editor
    static void ActivateEditor() noexcept;
};
