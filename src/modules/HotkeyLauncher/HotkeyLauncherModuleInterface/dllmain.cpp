#include "pch.h"

#include <interface/powertoy_module_interface.h>

#include <common/logger/logger.h>
#include <common/SettingsAPI/settings_objects.h>

#include "trace.h"

#include <shellapi.h>
#include <vector>
#include <mutex>
#include <string>

extern "C" IMAGE_DOS_HEADER __ImageBase;

namespace NonLocalizable
{
    const wchar_t ModuleKey[] = L"HotkeyLauncher";
}

namespace
{
    const wchar_t JSON_KEY_PROPERTIES[] = L"properties";
    const wchar_t JSON_KEY_HOTKEY_ACTIONS[] = L"hotkey_actions";
    const wchar_t JSON_KEY_VALUE[] = L"value";
    const wchar_t JSON_KEY_WIN[] = L"win";
    const wchar_t JSON_KEY_ALT[] = L"alt";
    const wchar_t JSON_KEY_CTRL[] = L"ctrl";
    const wchar_t JSON_KEY_SHIFT[] = L"shift";
    const wchar_t JSON_KEY_CODE[] = L"code";
    const wchar_t JSON_KEY_HOTKEY[] = L"hotkey";
    const wchar_t JSON_KEY_ACTION_PATH[] = L"action_path";
    const wchar_t JSON_KEY_ARGUMENTS[] = L"arguments";
    const wchar_t JSON_KEY_WORKING_DIR[] = L"working_directory";
    const wchar_t JSON_KEY_ID[] = L"id";
}

struct HotkeyAction
{
    int id = 0;
    PowertoyModuleIface::Hotkey hotkey{};
    std::wstring action_path;
    std::wstring arguments;
    std::wstring working_directory;
};

BOOL APIENTRY DllMain(HMODULE /*hModule*/, DWORD ul_reason_for_call, LPVOID /*lpReserved*/)
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
        Trace::RegisterProvider();
        break;

    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
        break;

    case DLL_PROCESS_DETACH:
        Trace::UnregisterProvider();
        break;
    }
    return TRUE;
}

class HotkeyLauncherModuleInterface : public PowertoyModuleIface
{
public:
    HotkeyLauncherModuleInterface()
    {
        init_settings();
    }

    // Return the localized display name of the powertoy
    virtual const wchar_t* get_name() override
    {
        return NonLocalizable::ModuleKey;
    }

    // Return the non localized key of the powertoy, this will be cached by the runner
    virtual const wchar_t* get_key() override
    {
        return NonLocalizable::ModuleKey;
    }

    // Return the configured status for the gpo policy for the module
    virtual powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
    {
        return powertoys_gpo::getConfiguredHotkeyLauncherEnabledValue();
    }

    // Return JSON with the configuration options.
    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);
        PowerToysSettings::Settings settings(hinstance, get_name());
        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    // Called when the user saves settings from the Settings UI.
    virtual void set_config(const wchar_t* config) override
    {
        try
        {
            PowerToysSettings::PowerToyValues values =
                PowerToysSettings::PowerToyValues::from_json_string(config, get_key());

            parse_hotkey_actions(values);
            values.save_to_settings_file();
        }
        catch (std::exception&)
        {
            Logger::error("HotkeyLauncher: failed to parse config");
        }
    }

    // get_hotkeys is called even when the module is disabled.
    virtual size_t get_hotkeys(Hotkey* hotkeys, size_t buffer_size) override
    {
        std::lock_guard<std::mutex> lock(m_mutex);
        size_t count = m_actions.size();
        if (hotkeys)
        {
            for (size_t i = 0; i < count && i < buffer_size; i++)
            {
                hotkeys[i] = m_actions[i].hotkey;
            }
        }
        return count;
    }

    // on_hotkey is called even when the module is disabled, so we must guard.
    virtual bool on_hotkey(size_t hotkeyId) override
    {
        if (!m_enabled)
        {
            return false;
        }

        std::lock_guard<std::mutex> lock(m_mutex);
        if (hotkeyId >= m_actions.size())
        {
            return false;
        }

        const auto& action = m_actions[hotkeyId];
        Logger::trace(L"HotkeyLauncher: launching action id={}, path={}", action.id, action.action_path);
        Trace::HotkeyLauncherLaunchAction(action.id);

        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = SEE_MASK_FLAG_NO_UI;
        sei.lpFile = action.action_path.c_str();
        sei.lpParameters = action.arguments.empty() ? nullptr : action.arguments.c_str();
        sei.lpDirectory = action.working_directory.empty() ? nullptr : action.working_directory.c_str();
        sei.nShow = SW_SHOWNORMAL;

        if (!ShellExecuteExW(&sei))
        {
            Logger::error(L"HotkeyLauncher: failed to launch '{}'", action.action_path);
        }

        return true;
    }

    // Enable the powertoy
    virtual void enable() override
    {
        Logger::info("HotkeyLauncher enabling");
        m_enabled = true;
        Trace::EnableHotkeyLauncher(true);
    }

    // Disable the powertoy
    virtual void disable() override
    {
        Logger::info("HotkeyLauncher disabling");
        m_enabled = false;
        Trace::EnableHotkeyLauncher(false);
    }

    // Returns if the powertoy is enabled
    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    // Destroy the powertoy and free memory
    virtual void destroy() override
    {
        delete this;
    }

    // Disabled by default -- user must opt in
    virtual bool is_enabled_by_default() const override
    {
        return false;
    }

private:
    void parse_hotkey_actions(PowerToysSettings::PowerToyValues& settings)
    {
        auto settingsObject = settings.get_raw_json();
        if (settingsObject.GetView().Size() == 0)
        {
            return;
        }

        try
        {
            auto props = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES);
            auto actionsContainer = props.GetNamedObject(JSON_KEY_HOTKEY_ACTIONS);
            auto actionsArray = actionsContainer.GetNamedArray(JSON_KEY_VALUE);

            std::vector<HotkeyAction> newActions;
            newActions.reserve(actionsArray.Size());

            for (uint32_t i = 0; i < actionsArray.Size(); i++)
            {
                auto actionObj = actionsArray.GetObjectAt(i);
                auto hotkeyObj = actionObj.GetNamedObject(JSON_KEY_HOTKEY);

                HotkeyAction action;
                action.id = static_cast<int>(actionObj.GetNamedNumber(JSON_KEY_ID));
                action.hotkey.win = hotkeyObj.GetNamedBoolean(JSON_KEY_WIN);
                action.hotkey.ctrl = hotkeyObj.GetNamedBoolean(JSON_KEY_CTRL);
                action.hotkey.shift = hotkeyObj.GetNamedBoolean(JSON_KEY_SHIFT);
                action.hotkey.alt = hotkeyObj.GetNamedBoolean(JSON_KEY_ALT);
                action.hotkey.key = static_cast<unsigned char>(hotkeyObj.GetNamedNumber(JSON_KEY_CODE));
                action.action_path = std::wstring(actionObj.GetNamedString(JSON_KEY_ACTION_PATH));
                action.arguments = std::wstring(actionObj.GetNamedString(JSON_KEY_ARGUMENTS, L""));
                action.working_directory = std::wstring(actionObj.GetNamedString(JSON_KEY_WORKING_DIR, L""));
                newActions.push_back(std::move(action));
            }

            std::lock_guard<std::mutex> lock(m_mutex);
            m_actions = std::move(newActions);
            Logger::info("HotkeyLauncher: loaded {} hotkey actions", m_actions.size());
        }
        catch (...)
        {
            Logger::error("HotkeyLauncher: failed to parse hotkey_actions from settings JSON");
        }
    }

    void init_settings()
    {
        try
        {
            PowerToysSettings::PowerToyValues settings =
                PowerToysSettings::PowerToyValues::load_from_settings_file(get_key());

            parse_hotkey_actions(settings);
        }
        catch (std::exception&)
        {
            Logger::warn(L"HotkeyLauncher: no existing settings found, using defaults");
        }
    }

    bool m_enabled = false;
    std::mutex m_mutex;
    std::vector<HotkeyAction> m_actions;
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new HotkeyLauncherModuleInterface();
}
