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

struct DarkModeConfig
{
    bool useLocation = false;
    std::wstring latitude = L"0.0";
    std::wstring longitude = L"0.0";

    // Stored as minutes since midnight
    int lightTime = 8 * 60; // 08:00 default
    int darkTime = 20 * 60; // 20:00 default
    bool changeSystem = false;
    bool changeApps = false;

    // Overrides
    bool forceLight = false;
    bool forceDark = false;
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
