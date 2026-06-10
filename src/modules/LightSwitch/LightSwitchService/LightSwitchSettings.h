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

#include "../LightSwitchTypes.h"

class SettingsObserver;

class LightSwitchSettings
{
public:
    static LightSwitchSettings& instance();

    static inline const LightSwitchConfig& settings()
    {
        return instance().m_settings;
    }

    void InitFileWatcher();
    static std::wstring GetSettingsFileName();

    void AddObserver(SettingsObserver& observer);
    void RemoveObserver(SettingsObserver& observer);

    void LoadSettings();

    HANDLE GetSettingsChangedEvent() const;

private:
    LightSwitchSettings();
    ~LightSwitchSettings();

    LightSwitchConfig m_settings;
    std::unique_ptr<FileWatcher> m_settingsFileWatcher;
    std::unordered_set<SettingsObserver*> m_observers;

    void NotifyObservers(SettingId id) const;

    HANDLE m_settingsChangedEvent = nullptr;
    mutable std::mutex m_settingsMutex;

    // Debounce state
    std::atomic_bool m_debouncePending{ false };
    std::mutex m_debounceMutex;
    std::chrono::steady_clock::time_point m_lastChangeTime{};
    std::jthread m_debounceThread;
};
