#pragma once

#include <unordered_set>

#include <common/SettingsAPI/settings_helpers.h>
#include <common/SettingsAPI/settings_objects.h>

#include <FancyZonesLib/ModuleConstants.h>
#include <FancyZonesLib/SettingsConstants.h>

class SettingsObserver;
class FileWatcher;

enum struct OverlappingZonesAlgorithm : int
{
    Smallest = 0,
    Largest = 1,
    Positional = 2,
    ClosestCenter = 3,
    EnumElements = 4, // number of elements in the enum, not counting this
};

// in reality, this file needs to be kept in sync currently with src/settings-ui/Settings.UI.Library/FZConfigProperties.cs
struct Settings
{
    // The values specified here are the defaults.
    bool shiftDrag = true;
    bool mouseSwitch = false;
    bool displayChange_moveWindows = false;
    bool zoneSetChange_flashZones = false;
    bool zoneSetChange_moveWindows = false;
    bool overrideSnapHotkeys = false;
    bool moveWindowAcrossMonitors = false;
    bool moveWindowsBasedOnPosition = false;
    bool appLastZone_moveWindows = false;
    bool openWindowOnActiveMonitor = false;
    bool restoreSize = false;
    bool quickLayoutSwitch = true;
    bool flashZonesOnQuickSwitch = true;
    bool use_cursorpos_editor_startupscreen = true;
    bool showZonesOnAllMonitors = false;
    bool spanZonesAcrossMonitors = false;
    bool makeDraggedWindowTransparent = true;
    bool systemTheme = true;
    bool showZoneNumber = true;
    bool allowSnapPopupWindows = false;
    bool allowSnapChildWindows = false;
    bool disableRoundCorners = false;
    std::wstring zoneColor = L"#AACDFF";
    std::wstring zoneBorderColor = L"#FFFFFF";
    std::wstring zoneHighlightColor = L"#008CFF";
    std::wstring zoneNumberColor = L"#000000";
    int zoneHighlightOpacity = 50;
    OverlappingZonesAlgorithm overlappingZonesAlgorithm = OverlappingZonesAlgorithm::Smallest;
    PowerToysSettings::HotkeyObject editorHotkey = PowerToysSettings::HotkeyObject::from_settings(true, false, false, true, VK_OEM_3);
    bool windowSwitching = true;
    PowerToysSettings::HotkeyObject nextTabHotkey = PowerToysSettings::HotkeyObject::from_settings(true, false, false, false, VK_NEXT);
    PowerToysSettings::HotkeyObject prevTabHotkey = PowerToysSettings::HotkeyObject::from_settings(true, false, false, false, VK_PRIOR);
    std::wstring excludedApps = L"";
    std::vector<std::wstring> excludedAppsArray;
};

class FancyZonesSettings
{
public:
    static FancyZonesSettings& instance();
    static inline const Settings& settings()
    {
        return instance().m_settings;
    }

    inline static std::wstring GetSettingsFileName()
    {
        std::wstring saveFolderPath = PTSettingsHelper::get_module_save_folder_location(NonLocalizable::ModuleKey);
#if defined(UNIT_TESTS)
        return saveFolderPath + L"\\test-settings.json";
#else
        return saveFolderPath + L"\\settings.json";
#endif
    }

    void AddObserver(SettingsObserver& observer);
    void RemoveObserver(SettingsObserver& observer);

    void LoadSettings();

private:
    FancyZonesSettings();
    ~FancyZonesSettings() = default;

    Settings m_settings;
    std::unique_ptr<FileWatcher> m_settingsFileWatcher;
    std::unordered_set<SettingsObserver*> m_observers;

    void SetBoolFlag(const PowerToysSettings::PowerToyValues& values, const wchar_t* id, SettingId notificationId, bool& out);

    void NotifyObservers(SettingId id) const;
};
