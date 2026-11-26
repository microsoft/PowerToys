// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <Windows.h>
#include <filesystem>

#include <common/SettingsAPI/settings_helpers.h>
#include <common/SettingsAPI/settings_objects.h>
#include <modules/interface/powertoy_module_interface.h>

extern "C" IMAGE_DOS_HEADER __ImageBase;

class PowerScriptsModule final : public PowertoyModuleIface
{
public:
    const wchar_t* get_name() override
    {
        return L"PowerScripts";
    }

    const wchar_t* get_key() override
    {
        return L"PowerScripts";
    }

    bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        PowerToysSettings::Settings settings(reinterpret_cast<HINSTANCE>(&__ImageBase), get_key());
        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    void set_config(const wchar_t* /*config*/) override
    {
    }

    void enable() override
    {
        m_enabled = true;
    }

    void disable() override
    {
        m_enabled = false;
    }

    bool is_enabled() override
    {
        return m_enabled;
    }

    bool is_enabled_by_default() const override
    {
        try
        {
            const auto generalPath = PTSettingsHelper::get_powertoys_general_save_file_location();
            std::error_code error;
            const auto generalExists = std::filesystem::exists(generalPath, error);
            if (error)
            {
                return false;
            }

            if (generalExists)
            {
                const auto general = json::from_file(generalPath);
                if (!general)
                {
                    return false;
                }

                if (general->HasKey(L"enabled"))
                {
                    if (!json::has(*general, L"enabled"))
                    {
                        return false;
                    }

                    if (general->GetNamedObject(L"enabled").HasKey(L"PowerScripts"))
                    {
                        return false;
                    }
                }
            }

            // Only a genuinely absent general preference can inherit a prototype choice.
            const auto path = std::filesystem::path(PTSettingsHelper::get_module_save_folder_location(L"PowerScripts")) / L"config.json";
            const auto legacy = json::from_file(path.wstring());
            return legacy && json::has(*legacy, L"enabled", json::JsonValueType::Boolean) && legacy->GetNamedBoolean(L"enabled");
        }
        catch (...)
        {
            return false;
        }
    }

    powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
    {
        return powertoys_gpo::getConfiguredPowerScriptsEnabledValue();
    }

    void destroy() override
    {
        delete this;
    }

private:
    bool m_enabled = false;
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new PowerScriptsModule();
}
