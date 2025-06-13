#pragma once

#include "pch.h"
#include <common/utils/gpo.h>

class CSettings
{
public:
    CSettings();

    inline bool GetEnabled()
    {
        auto gpoSetting = powertoys_gpo::getConfiguredImageResizerEnabledValue();
        if (gpoSetting == powertoys_gpo::gpo_rule_configured_enabled)
            return true;
        if (gpoSetting == powertoys_gpo::gpo_rule_configured_disabled)
            return false;
        RefreshEnabledState();
        return settings.enabled;
    }

    inline bool GetExtendedContextMenuOnly() const
    {
        return settings.extendedContextMenuOnly;
    }

    inline void SetExtendedContextMenuOnly(bool extendedOnly)
    {
        settings.extendedContextMenuOnly = extendedOnly;
    }

    void Save();
    void Load();

private:
    struct Settings
    {
        bool enabled{ true };
        bool extendedContextMenuOnly{ false }; // Disabled by default.
    };

    void RefreshEnabledState();
    void Reload();
    void MigrateFromRegistry();
    void ParseJson();

    Settings settings;
    std::wstring jsonFilePath;
    std::wstring generalJsonFilePath;
    FILETIME lastLoadedTime;
    FILETIME lastLoadedGeneralSettingsTime{};
};

CSettings& CSettingsInstance();