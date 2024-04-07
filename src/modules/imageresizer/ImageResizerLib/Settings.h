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

    void Save();
    void Load();

private:
    struct Settings
    {
        bool enabled{ true };
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