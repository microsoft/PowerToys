// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "trace.h"

#include "../../../../common/Telemetry/TraceBase.h"

// Wrapper function for the TraceLoggingWriteWrapper macro which expects a global IsDataDiagnosticsEnabled
inline bool IsDataDiagnosticsEnabled()
{
    return telemetry::TraceBase::IsDataDiagnosticsEnabled();
}

TRACELOGGING_DEFINE_PROVIDER(
    g_hProvider,
    "Microsoft.PowerToys",
    (0x38e8889b, 0x9731, 0x53f5, 0xe9, 0x01, 0xe8, 0xa7, 0xc1, 0x75, 0x30, 0x74),
    TraceLoggingOptionProjectTelemetry());

void Trace::RegisterProvider() noexcept
{
    TraceLoggingRegister(g_hProvider);
}

void Trace::UnregisterProvider() noexcept
{
    TraceLoggingUnregister(g_hProvider);
}

void Trace::EnableCursorFocus(const bool enabled) noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "CursorFocus_EnableCursorFocus",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingBoolean(enabled, "Enabled"));
}
