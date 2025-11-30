#pragma once

#include <windows.h>
#include <TraceLoggingActivity.h>
#include <common/telemetry/ProjectTelemetry.h>

TRACELOGGING_DECLARE_PROVIDER(g_hProvider);

class Trace
{
public:
    static void RegisterProvider();
    static void UnregisterProvider();

    // Screencast Mode enabled/disabled state change
    static void ScreencastModeEnabled(bool enabled);
};
