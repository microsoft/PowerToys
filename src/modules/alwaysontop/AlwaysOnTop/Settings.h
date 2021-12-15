#pragma once

#include <common/SettingsAPI/FileWatcher.h>
#include <common/SettingsAPI/settings_objects.h>

using NotificationCallback = std::function<void()>;

struct Settings
{
    PowerToysSettings::HotkeyObject hotkey = PowerToysSettings::HotkeyObject::from_settings(true, true, false, false, 84); // win + ctrl + T
    bool enableSound = false;
    bool enableFrame = false;
    bool blockInGameMode = true;
};

class AlwaysOnTopSettings
{
public: 
    AlwaysOnTopSettings();
    ~AlwaysOnTopSettings() = default;

    void InitFileWatcher();
    static std::wstring GetSettingsFileName();

    void AddObserver(const NotificationCallback& callback);

    void LoadSettings();
    inline const Settings& GetSettings() const 
    {
        return m_settings;
    }

private:
    Settings m_settings;
    std::unique_ptr<FileWatcher> m_settingsFileWatcher;
    std::vector<NotificationCallback> m_observerCallbacks;

    void NotifyObservers() const;
};