#include "pch.h"
#include "gpo.h"

namespace powertoys_gpo
{
    std::optional<std::wstring> readRegistryStringValue(HKEY hRootKey, const std::wstring& subKey, const std::wstring& value_name, const bool is_multi_line_text)
    {
        // Set value type
        DWORD reg_value_type = REG_SZ;
        DWORD reg_flags = RRF_RT_REG_SZ;
        if (is_multi_line_text)
        {
            reg_value_type = REG_MULTI_SZ;
            reg_flags = RRF_RT_REG_MULTI_SZ;
        }

        DWORD string_buffer_capacity;
        // Request required buffer capacity / string length
        if (RegGetValueW(hRootKey, subKey.c_str(), value_name.c_str(), reg_flags, &reg_value_type, NULL, &string_buffer_capacity) != ERROR_SUCCESS)
        {
            return std::nullopt;
        }
        else if (string_buffer_capacity == 0)
        {
            return std::nullopt;
        }

        // RegGetValueW overshoots sometimes. Use a buffer first to not have characters past the string end.
        wchar_t* temp_buffer = new wchar_t[string_buffer_capacity / sizeof(wchar_t) + 1];
        // Read string
        if (RegGetValueW(hRootKey, subKey.c_str(), value_name.c_str(), reg_flags, &reg_value_type, temp_buffer, &string_buffer_capacity) != ERROR_SUCCESS)
        {
            delete[] temp_buffer;
            return std::nullopt;
        }

        // Convert buffer to std::wstring
        std::wstring string_value = L"";
        if (reg_value_type == REG_MULTI_SZ)
        {
            // If it is REG_MULTI_SZ handle this way
            wchar_t* currentString = temp_buffer;
            while (*currentString != L'\0')
            {
                // If first entry then assign the string, else add to the string
                string_value = (string_value == L"") ? currentString : (string_value + L"\r\n" + currentString);
                currentString += wcslen(currentString) + 1; // Move to the next string
            }
        }
        else
        {
            // If it is REG_SZ handle this way
            string_value = temp_buffer;
        }

        // delete buffer, return string value
        delete[] temp_buffer;
        return string_value;
    }

    gpo_rule_configured_t getConfiguredValue(const std::wstring& registry_value_name)
    {
        HKEY key{};
        DWORD value = 0xFFFFFFFE;
        DWORD valueSize = sizeof(value);

        bool machine_key_found = true;
        if (auto res = RegOpenKeyExW(POLICIES_SCOPE_MACHINE, POLICIES_PATH.c_str(), 0, KEY_READ, &key); res != ERROR_SUCCESS)
        {
            machine_key_found = false;
        }

        if (machine_key_found)
        {
            // If the path was found in the machine, we need to check if the value for the policy exists.
            auto res = RegQueryValueExW(key, registry_value_name.c_str(), nullptr, nullptr, reinterpret_cast<LPBYTE>(&value), &valueSize);

            RegCloseKey(key);

            if (res != ERROR_SUCCESS)
            {
                // Value not found on the path.
                machine_key_found = false;
            }
        }

        if (!machine_key_found)
        {
            // If there's no value found on the machine scope, try to get it from the user scope.
            if (auto res = RegOpenKeyExW(POLICIES_SCOPE_USER, POLICIES_PATH.c_str(), 0, KEY_READ, &key); res != ERROR_SUCCESS)
            {
                if (res == ERROR_FILE_NOT_FOUND)
                {
                    return gpo_rule_configured_not_configured;
                }
                return gpo_rule_configured_unavailable;
            }
            auto res = RegQueryValueExW(key, registry_value_name.c_str(), nullptr, nullptr, reinterpret_cast<LPBYTE>(&value), &valueSize);
            RegCloseKey(key);

            if (res != ERROR_SUCCESS)
            {
                return gpo_rule_configured_not_configured;
            }
        }

        switch (value)
        {
        case 0:
            return gpo_rule_configured_disabled;
        case 1:
            return gpo_rule_configured_enabled;
        default:
            return gpo_rule_configured_wrong_value;
        }
    }

    std::optional<std::wstring> getPolicyListValue(const std::wstring& registry_list_path, const std::wstring& registry_list_value_name)
    {
        // This function returns the value of an entry of a policy list. The user scope is only checked, if the list is not enabled for the machine to not mix the lists.

        HKEY key{};

        // Try to read from the machine list.
        bool machine_list_found = false;
        if (RegOpenKeyExW(POLICIES_SCOPE_MACHINE, registry_list_path.c_str(), 0, KEY_READ, &key) == ERROR_SUCCESS)
        {
            machine_list_found = true;
            RegCloseKey(key);

            // If the path exists in the machine registry, we try to read the value.
            auto regValueData = readRegistryStringValue(POLICIES_SCOPE_MACHINE, registry_list_path, registry_list_value_name);

            if (regValueData.has_value())
            {
                // Return the value from the machine list.
                return *regValueData;
            }
        }

        // If no list exists for machine, we try to read from the user list.
        if (!machine_list_found)
        {
            if (RegOpenKeyExW(POLICIES_SCOPE_USER, registry_list_path.c_str(), 0, KEY_READ, &key) == ERROR_SUCCESS)
            {
                RegCloseKey(key);

                // If the path exists in the user registry, we try to read the value.
                auto regValueData = readRegistryStringValue(POLICIES_SCOPE_USER, registry_list_path, registry_list_value_name);

                if (regValueData.has_value())
                {
                    // Return the value from the user list.
                    return *regValueData;
                }
            }
        }

        // No list exists for machine and user, or no value was found in the list, or an error ocurred while reading the value.
        return std::nullopt;
    }

    gpo_rule_configured_t getUtilityEnabledValue(const std::wstring& utility_name)
    {
        auto individual_value = getConfiguredValue(utility_name);

        if (individual_value == gpo_rule_configured_disabled || individual_value == gpo_rule_configured_enabled)
        {
            return individual_value;
        }
        else
        {
            return getConfiguredValue(POLICY_CONFIGURE_ENABLED_GLOBAL_ALL_UTILITIES);
        }
    }

    gpo_rule_configured_t getRunPluginEnabledValue(std::string pluginID)
    {
        if (pluginID == "" || pluginID == " ")
        {
            // this plugin id can't exist in the registry
            return gpo_rule_configured_not_configured;
        }

        std::wstring plugin_id(pluginID.begin(), pluginID.end());
        auto individual_plugin_setting = getPolicyListValue(POWER_LAUNCHER_INDIVIDUAL_PLUGIN_ENABLED_LIST_PATH, plugin_id);

        if (individual_plugin_setting.has_value())
        {
            if (*individual_plugin_setting == L"0")
            {
                // force disabled
                return gpo_rule_configured_disabled;
            }
            else if (*individual_plugin_setting == L"1")
            {
                // force enabled
                return gpo_rule_configured_enabled;
            }
            else if (*individual_plugin_setting == L"2")
            {
                // user takes control
                return gpo_rule_configured_not_configured;
            }
            else
            {
                return gpo_rule_configured_wrong_value;
            }
        }
        else
        {
            // If no individual plugin policy exists, we check the policy with the setting for all plugins.
            return getConfiguredValue(POLICY_CONFIGURE_ENABLED_POWER_LAUNCHER_ALL_PLUGINS);
        }
    }

    std::wstring getConfiguredMwbPolicyDefinedIpMappingRules()
    {
        // Important: HKLM has priority over HKCU
        auto mapping_rules = readRegistryStringValue(HKEY_LOCAL_MACHINE, POLICIES_PATH, POLICY_MWB_POLICY_DEFINED_IP_MAPPING_RULES, true);
        if (!mapping_rules.has_value())
        {
            mapping_rules = readRegistryStringValue(HKEY_CURRENT_USER, POLICIES_PATH, POLICY_MWB_POLICY_DEFINED_IP_MAPPING_RULES, true);
        }

        // return value
        if (mapping_rules.has_value())
        {
            return mapping_rules.value();
        }
        else
        {
            return std::wstring();
        }
    }
}
