// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <cstdint>
#include <optional>
#include <string_view>
#include <common/logger/logger.h>
#include <common/utils/gpo.h>
#include <common/utils/json.h>

// Mirrors each module interface's is_enabled_by_default().
enum class DefaultEnabled : uint8_t
{
    No,
    Yes,
    BeforeWindows11,
};

// Literal type so the catalog in main.cpp is constant-initialized, with no startup work.
struct ModuleRegistration
{
    std::wstring_view key;
    std::wstring_view dllPath;
    DefaultEnabled defaultEnabled;
    powertoys_gpo::gpo_rule_configured_t (*gpoPolicy)() = nullptr;

    bool enabled_by_default() const;

    bool apply_policy(bool preferredState) const
    {
        const auto rule = gpoPolicy ? gpoPolicy() : powertoys_gpo::gpo_rule_configured_not_configured;
        if (rule == powertoys_gpo::gpo_rule_configured_enabled || rule == powertoys_gpo::gpo_rule_configured_disabled)
        {
            return rule == powertoys_gpo::gpo_rule_configured_enabled;
        }
        return preferredState;
    }

    // An unloaded module is not running. Keep a saved true only to preserve the preference of a module that failed to load.
    bool unloaded_state(std::optional<bool> savedState) const
    {
        return savedState.value_or(false) && apply_policy(true);
    }

    bool is_enabled(const json::JsonObject& settings) const
    {
        if (gpoPolicy)
        {
            const auto rule = gpoPolicy();
            if (rule == powertoys_gpo::gpo_rule_configured_unavailable || rule == powertoys_gpo::gpo_rule_configured_wrong_value)
            {
                Logger::warn(L"Module {} has unavailable or invalid GPO configuration; using settings/defaults", key);
            }
        }

        if (settings.HasKey(L"enabled"))
        {
            if (json::has(settings, L"enabled", json::JsonValueType::Object))
            {
                const auto enabled = settings.GetNamedObject(L"enabled");
                if (json::has(enabled, key, json::JsonValueType::Boolean))
                {
                    return apply_policy(enabled.GetNamedBoolean(key));
                }
                if (!enabled.HasKey(key))
                {
                    return apply_policy(enabled_by_default());
                }
            }
            Logger::warn(L"Invalid enabled setting for {}; using module default", key);
        }
        return apply_policy(enabled_by_default());
    }
};
