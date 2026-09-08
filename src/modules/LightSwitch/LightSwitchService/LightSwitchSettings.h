#pragma once

#include <unordered_set>
#include <string>
#include <vector>
#include <memory>
#include <windows.h>
#include <mutex>
#include <atomic>
#include <thread>
#include <chrono>
#include <common/SettingsAPI/FileWatcher.h>
#include <common/SettingsAPI/settings_objects.h>
#include <SettingsConstants.h>

class SettingsObserver;

enum class ScheduleMode
{
    Off,
    FixedHours,
    SunsetToSunrise,
    FollowNightLight,
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
    case ScheduleMode::FollowNightLight:
        return L"FollowNightLight";
    default:
        return L"Off";
    }
}

inline ScheduleMode FromString(const std::wstring& str)
{
    if (str == L"SunsetToSunrise")
        return ScheduleMode::SunsetToSunrise;
    if (str == L"FixedHours")
        return ScheduleMode::FixedHours;
    if (str == L"FollowNightLight")
        return ScheduleMode::FollowNightLight;
    else
        return ScheduleMode::Off;
}

struct LightSwitchConfig
{
    ScheduleMode scheduleMode = ScheduleMode::FixedHours;

    std::wstring latitude = L"0.0";
    std::wstring longitude = L"0.0";

    // Stored as minutes since midnight
    int lightTime = 8 * 60; // 08:00 default
    int darkTime = 20 * 60; // 20:00 default

    int sunrise_offset = 0;
    int sunset_offset = 0;

    bool changeSystem = false;
    bool changeApps = false;
};

// Parsing and patching preserve the existing settings schema.
bool TryParseLightSwitchConfig(const json::JsonObject& values, LightSwitchConfig& config, std::wstring& error);
bool TryPatchLightSwitchScheduleMode(const std::wstring& path, ScheduleMode mode, LightSwitchConfig& config, std::wstring& error);
bool HasSameEffectiveLightSwitchSettings(const LightSwitchConfig& left, const LightSwitchConfig& right);

class LightSwitchSettings
{
public:
    static LightSwitchSettings& instance();

    static inline LightSwitchConfig settings()
    {
        auto& instanceRef = instance();
        std::lock_guard<std::mutex> guard(instanceRef.m_settingsMutex);
        return instanceRef.m_settings;
    }

    void InitFileWatcher();
    static std::wstring GetSettingsFileName();

    void AddObserver(SettingsObserver& observer);
    void RemoveObserver(SettingsObserver& observer);

    void LoadSettings();
    bool TryLoadSettings(LightSwitchConfig& config, std::wstring& error);
    bool TrySetScheduleMode(ScheduleMode mode, LightSwitchConfig& config, std::wstring& error);

    HANDLE GetSettingsChangedEvent() const;

private:
    LightSwitchSettings();
    ~LightSwitchSettings();

    LightSwitchConfig m_settings;
    std::unique_ptr<FileWatcher> m_settingsFileWatcher;
    std::unordered_set<SettingsObserver*> m_observers;

    void NotifyObservers(SettingId id) const;
    void ApplySettingsLocked(const LightSwitchConfig& config);

    HANDLE m_settingsChangedEvent = nullptr;
    mutable std::mutex m_settingsMutex;

    // Debounce state
    std::atomic_bool m_debouncePending{ false };
    std::mutex m_debounceMutex;
    std::chrono::steady_clock::time_point m_lastChangeTime{};
    std::jthread m_debounceThread;
};
