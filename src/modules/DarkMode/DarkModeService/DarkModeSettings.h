#pragma once

#include <unordered_set>
#include <string>
#include <vector>
#include <memory>
#include <windows.h>

#include <common/SettingsAPI/FileWatcher.h>
#include <common/SettingsAPI/settings_objects.h>
#include <SettingsConstants.h>

class SettingsObserver;

enum class ScheduleMode
{
    FixedHours,
    SunsetToSunrise
    // Add more in the future
};

inline std::wstring ToString(ScheduleMode mode)
{
    switch (mode)
    {
    case ScheduleMode::FixedHours:
        return L"FixedHours";
    case ScheduleMode::SunsetToSunrise:
        return L"SunsetToSunrise";
    default:
        return L"FixedHours";
    }
}

inline ScheduleMode FromString(const std::wstring& str)
{
    if (str == L"SunsetToSunrise")
        return ScheduleMode::SunsetToSunrise;
    else
        return ScheduleMode::FixedHours;
}

struct DarkModeConfig
{
    ScheduleMode scheduleMode = ScheduleMode::FixedHours;

    std::wstring latitude = L"0.0";
    std::wstring longitude = L"0.0";

    // Stored as minutes since midnight
    int lightTime = 8 * 60; // 08:00 default
    int darkTime = 20 * 60; // 20:00 default

    int offset = 0; // offset in minutes to apply to calculated times

    bool changeSystem = false;
    bool changeApps = false;
};

class DarkModeSettings
{
public:
    static DarkModeSettings& instance();

    static inline const DarkModeConfig& settings()
    {
        return instance().m_settings;
    }

    void InitFileWatcher();
    static std::wstring GetSettingsFileName();

    void AddObserver(SettingsObserver& observer);
    void RemoveObserver(SettingsObserver& observer);

    void LoadSettings();

private:
    DarkModeSettings();
    ~DarkModeSettings() = default;

    DarkModeConfig m_settings;
    std::unique_ptr<FileWatcher> m_settingsFileWatcher;
    std::unordered_set<SettingsObserver*> m_observers;

    void NotifyObservers(SettingId id) const;
};
