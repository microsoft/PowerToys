#pragma once

#include "pch.h"
#include <common/utils/gpo.h>

class FileLocksmithSettings
{
public:
    FileLocksmithSettings();

    inline bool GetEnabled()
    {
        auto gpoSetting = powertoys_gpo::getConfiguredFileLocksmithEnabledValue();
        if (gpoSetting == powertoys_gpo::gpo_rule_configured_enabled)
            return true;
        if (gpoSetting == powertoys_gpo::gpo_rule_configured_disabled)
            return false;
        Reload();
        RefreshEnabledState();
        return settings.enabled;
    }

    inline bool GetShowInExtendedContextMenu() const
    {
        return settings.showInExtendedContextMenu;
    }

    inline void SetExtendedContextMenuOnly(bool extendedOnly)
    {
        settings.showInExtendedContextMenu = extendedOnly;
    }

    void Save();
    void Load();

private:
    struct Settings
    {
        bool enabled{ true };
        bool showInExtendedContextMenu{ false };
    };

    void RefreshEnabledState();
    void Reload();
    void ParseJson();

    Settings settings;
    std::wstring generalJsonFilePath;
    std::wstring jsonFilePath;
    FILETIME lastLoadedTime{};
    FILETIME lastLoadedGeneralSettingsTime{};
};

FileLocksmithSettings& FileLocksmithSettingsInstance();
