#pragma once

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
        Reload();
        return settings.enabled;
    }

    inline void SetEnabled(bool enabled)
    {
        settings.enabled = enabled;
        Save();
    }

    void Save();
    void Load();

private:
    struct Settings
    {
        bool enabled{ true };
    };

    void Reload();
    void MigrateFromRegistry();
    void ParseJson();

    Settings settings;
    std::wstring jsonFilePath;
    FILETIME lastLoadedTime;
};

CSettings& CSettingsInstance();