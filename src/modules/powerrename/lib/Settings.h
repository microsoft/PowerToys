#pragma once

#include <common/utils/json.h>
#include <common/utils/gpo.h>

class CSettings
{
public:
    CSettings();

    inline bool GetEnabled()
    {
        auto gpoSetting = powertoys_gpo::getConfiguredPowerRenameEnabledValue();
        if (gpoSetting == powertoys_gpo::gpo_rule_configured_enabled)
            return true;
        if (gpoSetting == powertoys_gpo::gpo_rule_configured_disabled)
            return false;
        Reload();
        RefreshEnabledState();
        return settings.enabled;
    }

    inline bool GetShowIconOnMenu() const
    {
        return settings.showIconOnMenu;
    }

    inline void SetShowIconOnMenu(bool show)
    {
        settings.showIconOnMenu = show;
    }

    inline bool GetExtendedContextMenuOnly() const
    {
        return settings.extendedContextMenuOnly;
    }

    inline void SetExtendedContextMenuOnly(bool extendedOnly)
    {
        settings.extendedContextMenuOnly = extendedOnly;
    }

    inline bool GetPersistState() const
    {
        return settings.persistState;
    }

    inline void SetPersistState(bool persistState)
    {
        settings.persistState = persistState;
    }

    inline bool GetUseBoostLib() const
    {
        return settings.useBoostLib;
    }

    inline void SetUseBoostLib(bool useBoostLib)
    {
        settings.useBoostLib = useBoostLib;
    }

    inline bool GetMRUEnabled() const
    {
        return settings.MRUEnabled;
    }

    inline void SetMRUEnabled(bool MRUEnabled)
    {
        settings.MRUEnabled = MRUEnabled;
    }

    inline unsigned int GetMaxMRUSize() const
    {
        return settings.maxMRUSize;
    }

    inline void SetMaxMRUSize(unsigned int maxMRUSize)
    {
        settings.maxMRUSize = maxMRUSize;
    }

    inline unsigned int GetFlags() const
    {
        return settings.flags;
    }

    inline void SetFlags(unsigned int flags)
    {
        settings.flags = flags;
        WriteFlags();
    }

    void Save();
    void Load();

private:
    struct Settings
    {
        bool enabled{ true };
        bool showIconOnMenu{ true };
        bool extendedContextMenuOnly{ false }; // Disabled by default.
        bool persistState{ true };
        bool useBoostLib{ false }; // Disabled by default.
        bool MRUEnabled{ true };
        unsigned int maxMRUSize{ 10 };
        unsigned int flags{ 0 };
    };

    void Reload();
    void RefreshEnabledState();
    void MigrateFromRegistry();
    void ParseJson();

    void ReadFlags();
    void WriteFlags();

    Settings settings;
    std::wstring generalJsonFilePath;
    std::wstring moduleJsonFilePath;
    std::wstring UIFlagsFilePath;
    FILETIME lastLoadedTime{};
    FILETIME lastLoadedGeneralSettingsTime{};
};

CSettings& CSettingsInstance();

class LastRunSettings
{
    static constexpr inline int DEFAULT_WINDOW_WIDTH = 1400;
    static constexpr inline int DEFAULT_WINDOW_HEIGHT = 800;

    int lastWindowWidth{ DEFAULT_WINDOW_WIDTH };
    int lastWindowHeight{ DEFAULT_WINDOW_HEIGHT };

    std::wstring searchText{};
    std::wstring replaceText{};

public:
    inline LastRunSettings()
    {
        Load();
    }

    inline std::tuple<int, int> GetLastWindowSize() const
    {
        return std::make_tuple(lastWindowWidth, lastWindowHeight);
    }

    inline void UpdateLastWindowSize(const int width, const int height)
    {
        lastWindowWidth = std::max(width, DEFAULT_WINDOW_WIDTH);
        lastWindowHeight = std::max(height, DEFAULT_WINDOW_HEIGHT);
        Save();
    }

    inline const std::wstring& GetSearchText() const
    {
        return searchText;
    }

    inline void SetSearchText(const std::wstring& text)
    {
        searchText = text;
        Save();
    }

    inline const std::wstring& GetReplaceText() const
    {
        return replaceText;
    }

    inline void SetReplaceText(const std::wstring& text)
    {
        replaceText = text;
        Save();
    }

    void Load();
    void Save();
};

LastRunSettings& LastRunSettingsInstance();
