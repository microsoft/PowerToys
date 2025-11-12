#pragma once

#include <common/utils/json.h>

enum class DashboardSortOrder
{
    Alphabetical = 0,
    ByStatus = 1,
};

struct GeneralSettings
{
    bool isStartupEnabled;
    bool showSystemTrayIcon;
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
    DashboardSortOrder dashboardSortOrder;
    std::wstring theme;
    std::wstring systemTheme;
    std::wstring powerToysVersion;
    json::JsonObject ignoredConflictProperties;

    json::JsonObject to_json();
};

json::JsonObject load_general_settings();
GeneralSettings get_general_settings();
void apply_general_settings(const json::JsonObject& general_configs, bool save = true);
void start_enabled_powertoys();