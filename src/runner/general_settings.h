#pragma once

#include <common/utils/json.h>

struct GeneralSettings
{
    bool isStartupEnabled;
    std::wstring startupDisabledReason;
    std::map<std::wstring, bool> isModulesEnabledMap;
    bool isElevated;
    bool isRunElevated;
    bool isAdmin;
    bool enableWarningsElevatedApps;
    bool showNewUpdatesToastNotification;
    bool downloadUpdatesAutomatically;
    bool showWhatsNewAfterUpdates;
    bool enableExperimentation;
    std::wstring theme;
    std::wstring systemTheme;
    std::wstring powerToysVersion;

    json::JsonObject to_json();
};

json::JsonObject load_general_settings();
GeneralSettings get_general_settings();
void apply_general_settings(const json::JsonObject& general_configs, bool save = true);
void start_enabled_powertoys();