#pragma once

#include <unordered_set>

#include <common/SettingsAPI/FileWatcher.h>
#include <common/SettingsAPI/settings_objects.h>

#include <SettingsConstants.h>

#include <winrt/Windows.UI.ViewManagement.h>

class SettingsObserver;

// Needs to be kept in sync with src\settings-ui\Settings.UI.Library\AlwaysOnTopProperties.cs
struct Settings
{
    PowerToysSettings::HotkeyObject hotkey = PowerToysSettings::HotkeyObject::from_settings(true, true, false, false, 84); // win + ctrl + T
    bool enableFrame = true;
    bool enableSound = true;
    bool roundCornersEnabled = true;
    bool blockInGameMode = true;
    bool frameAccentColor = true;
    int frameThickness = 15;
    int frameOpacity = 100;
    COLORREF frameColor = RGB(0, 173, 239);
    std::vector<std::wstring> excludedApps{};
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

    void AddObserver(SettingsObserver& observer);
    void RemoveObserver(SettingsObserver& observer);

    void LoadSettings();

private:
    AlwaysOnTopSettings();
    ~AlwaysOnTopSettings() = default;

    winrt::Windows::UI::ViewManagement::UISettings m_uiSettings;
    Settings m_settings;
    std::unique_ptr<FileWatcher> m_settingsFileWatcher;
    std::unordered_set<SettingsObserver*> m_observers;

    void NotifyObservers(SettingId id) const;
};