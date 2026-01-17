#pragma once

#include <common/Telemetry/TraceBase.h>

struct GeneralSettings;

class Trace : public telemetry::TraceBase
{
public:
    static void EventLaunch(const std::wstring& versionNumber, bool isProcessElevated);
    static void SettingsChanged(const GeneralSettings& settings);

    // Auto-update telemetry
    static void UpdateCheckCompleted(bool success, bool updateAvailable, const std::wstring& fromVersion, const std::wstring& toVersion);
    static void UpdateDownloadCompleted(bool success, const std::wstring& version);
};
