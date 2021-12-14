#pragma once

#include <common/SettingsAPI/FileWatcher.h>
#include <common/SettingsAPI/settings_objects.h>

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
    AlwaysOnTopSettings(HWND window, const std::wstring& settingsFileName = GetSettingsFileName());
    ~AlwaysOnTopSettings() = default;

    static std::wstring GetSettingsFileName();

    void LoadSettings();

    inline const Settings& GetSettings() const 
    {
        return m_settings;
    }

private:
    Settings m_settings;
    FileWatcher m_settingsFileWatcher;
};