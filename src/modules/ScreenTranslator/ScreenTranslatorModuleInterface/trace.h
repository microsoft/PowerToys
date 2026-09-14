#pragma once
#include <common/Telemetry/TraceBase.h>

class Trace : public telemetry::TraceBase
{
public:
    static void EnableScreenTranslator(const bool enabled) noexcept;
};
