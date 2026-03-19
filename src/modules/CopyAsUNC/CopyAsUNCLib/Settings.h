#pragma once

#include "pch.h"

class CopyAsUNCSettings
{
public:
    CopyAsUNCSettings();

    inline bool GetEnabled()
    {
        // TODO: Add GPO entry to src/common/utils/gpo.h and uncomment:
        // auto gpoSetting = powertoys_gpo::getConfiguredCopyAsUNCEnabledValue();
        // if (gpoSetting == powertoys_gpo::gpo_rule_configured_enabled)  return true;
        // if (gpoSetting == powertoys_gpo::gpo_rule_configured_disabled) return false;
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

CopyAsUNCSettings& CopyAsUNCSettingsInstance();
