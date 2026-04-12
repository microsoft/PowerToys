#include "pch.h"

#include <interface/powertoy_module_interface.h>
#include <common/SettingsAPI/settings_objects.h>

#include "ModuleConstants.h"
#include "AltDragMove.h"

BOOL APIENTRY DllMain(HMODULE /*hModule*/, DWORD ul_reason_for_call, LPVOID /*lpReserved*/)
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
    case DLL_PROCESS_DETACH:
        break;
    }
    return TRUE;
}

namespace
{
    const wchar_t JSON_KEY_PROPERTIES[] = L"properties";
    const wchar_t JSON_KEY_ENABLED[] = L"enabled";
    const wchar_t JSON_KEY_MODIFIER[] = L"modifier";
    const wchar_t JSON_KEY_VALUE[] = L"value";
}

class AltDragMoveModule : public PowertoyModuleIface
{
public:
    AltDragMoveModule()
    {
        init_settings();
    }

    virtual const wchar_t* get_name() override
    {
        return NonLocalizable::ModuleKey;
    }

    virtual const wchar_t* get_key() override
    {
        return NonLocalizable::ModuleKey;
    }

    virtual powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
    {
        return powertoys_gpo::gpo_rule_configured_not_configured;
    }

    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);
        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.set_description(NonLocalizable::ModuleDescription);

        settings.add_bool_toggle(JSON_KEY_ENABLED, L"Enable AltDragMove", m_enabled);

        // Modifier selection: 0 = Alt, 1 = Ctrl, 2 = Shift
        settings.add_int_spinner(JSON_KEY_MODIFIER, L"Modifier key (0=Alt, 1=Ctrl, 2=Shift)",
                                 static_cast<int>(m_currentModifier), 0, 2, 1);

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    virtual void set_config(const wchar_t* config) override
    {
        try
        {
            PowerToysSettings::PowerToyValues values =
                PowerToysSettings::PowerToyValues::from_json_string(config, get_key());

            auto settingsObject = values.get_raw_json();
            if (settingsObject.GetView().Size())
            {
                auto props = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES);

                try
                {
                    int mod = static_cast<int>(props.GetNamedObject(JSON_KEY_MODIFIER).GetNamedNumber(JSON_KEY_VALUE));
                    if (mod >= 0 && mod <= 2)
                    {
                        m_currentModifier = static_cast<AltDragMove::Modifier>(mod);
                        AltDragMove::instance().SetModifier(m_currentModifier);
                    }
                }
                catch (...) {}
            }

            values.save_to_settings_file();
        }
        catch (...)
        {
        }
    }

    virtual void enable() override
    {
        m_enabled = true;
        AltDragMove::instance().SetModifier(m_currentModifier);
        AltDragMove::instance().Start();
    }

    virtual void disable() override
    {
        m_enabled = false;
        AltDragMove::instance().Stop();
    }

    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    virtual void destroy() override
    {
        disable();
        delete this;
    }

private:
    void init_settings()
    {
        try
        {
            PowerToysSettings::PowerToyValues settings =
                PowerToysSettings::PowerToyValues::load_from_settings_file(get_key());

            auto settingsObject = settings.get_raw_json();
            if (settingsObject.GetView().Size())
            {
                auto props = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES);
                try
                {
                    int mod = static_cast<int>(props.GetNamedObject(JSON_KEY_MODIFIER).GetNamedNumber(JSON_KEY_VALUE));
                    if (mod >= 0 && mod <= 2)
                    {
                        m_currentModifier = static_cast<AltDragMove::Modifier>(mod);
                    }
                }
                catch (...) {}
            }
        }
        catch (...)
        {
        }
    }

    bool m_enabled = false;
    AltDragMove::Modifier m_currentModifier = AltDragMove::Modifier::Alt;
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new AltDragMoveModule();
}
