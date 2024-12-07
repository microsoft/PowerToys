#pragma once

#include <common/Telemetry/TraceBase.h>

struct GeneralSettings;

class Trace : public telemetry::TraceBase
{
public:
    static void EventLaunch(const std::wstring& versionNumber, bool isProcessElevated);
    static void SettingsChanged(const GeneralSettings& settings);
};
