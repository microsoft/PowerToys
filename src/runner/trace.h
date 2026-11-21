#pragma once

#include <common/Telemetry/TraceBase.h>
#include <string_view>

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

    // A module hotkey action blocked the keyboard hook thread for too long
    static void SlowHotkeyAction(const std::wstring& moduleName, std::wstring_view dispatchPath, long long durationMs);
};
