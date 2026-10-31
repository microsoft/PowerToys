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

    // Tray icon interaction telemetry
    static void TrayIconLeftClick(bool quickAccessEnabled);
    static void TrayIconDoubleClick(bool quickAccessEnabled);
    static void TrayIconRightClick(bool quickAccessEnabled);

    // Startup markers for performance tools. They use the separate Microsoft.PowerToys.Performance
    // provider, which isn't in the telemetry provider group, so diagnostic data collection doesn't
    // pick them up, and they're written whether or not diagnostic data is turned on.
    static void RegisterPerformanceProvider();
    static void UnregisterPerformanceProvider();
    static void StartupStage(const char* stage, uint64_t msSinceProcessStart);
};
