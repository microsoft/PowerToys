#pragma once

#include <common/SettingsAPI/FileWatcher.h>
#include <common/SettingsAPI/settings_objects.h>

using NotificationCallback = std::function<void()>;

struct Settings
{
    PowerToysSettings::HotkeyObject hotkey = PowerToysSettings::HotkeyObject::from_settings(true, true, false, false, 84); // win + ctrl + T
    bool enableFrame = true;
    bool enableSound = true;
    bool blockInGameMode = true;
    float frameThickness = 15.0f;
    COLORREF frameColor = RGB(0, 173, 239);
};

class AlwaysOnTopSettings
{
public:
    static AlwaysOnTopSettings& instance();
    static inline const Settings& settings()
    {
        return instance().m_settings;
    }

    void InitFileWatcher();
    static std::wstring GetSettingsFileName();

    void AddObserver(const NotificationCallback& callback);

    void LoadSettings();

private:
    AlwaysOnTopSettings();
    ~AlwaysOnTopSettings() = default;

    Settings m_settings;
    std::unique_ptr<FileWatcher> m_settingsFileWatcher;
    std::vector<NotificationCallback> m_observerCallbacks;

    void NotifyObservers() const;
};