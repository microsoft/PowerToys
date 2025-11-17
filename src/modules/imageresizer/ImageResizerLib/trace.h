#pragma once

#include <common/Telemetry/TraceBase.h>

class Trace : public telemetry::TraceBase
{
public:
    static void EnableImageResizer(_In_ bool enabled) noexcept;
    static void Invoked() noexcept;
    static void InvokedRet(_In_ HRESULT hr) noexcept;
    static void QueryContextMenuError(_In_ HRESULT hr) noexcept;
};