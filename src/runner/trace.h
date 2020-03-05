#pragma once

struct GeneralSettings;

class Trace
{
public:
    static void RegisterProvider();
    static void UnregisterProvider();
    static void EventLaunch(const std::wstring& versionNumber, bool isProcessElevated);
    static void SettingsChanged(const GeneralSettings& settings);
};
