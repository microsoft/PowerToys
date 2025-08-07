#include "pch.h"

#include <interface/powertoy_module_interface.h>

#include <common/interop/shared_constants.h>
#include <common/logger/logger.h>
#include <common/SettingsAPI/settings_helpers.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/utils/logger_helper.h>
#include <common/utils/resources.h>
#include <common/utils/winapi_error.h>

#include <WorkspacesLib/trace.h>
#include <WorkspacesLib/WorkspacesData.h>

#include <shellapi.h>

#include "resource.h"
#include <common/utils/EventWaiter.h>

// Non-localizable
const std::wstring workspacesLauncherPath = L"PowerToys.WorkspacesLauncher.exe";
const std::wstring workspacesWindowArrangerPath = L"PowerToys.WorkspacesWindowArranger.exe";
const std::wstring workspacesSnapshotToolPath = L"PowerToys.WorkspacesSnapshotTool.exe";
const std::wstring workspacesEditorPath = L"PowerToys.WorkspacesEditor.exe";

namespace
{
    const wchar_t JSON_KEY_PROPERTIES[] = L"properties";
    const wchar_t JSON_KEY_RUN_EDITOR_HOTKEY[] = L"hotkey";
    const wchar_t JSON_KEY_RUN_SNAPSHOT_TOOL_HOTKEY[] = L"run-snapshot-tool-hotkey";
    const wchar_t JSON_KEY_RUN_LAUNCHER_HOTKEY[] = L"run-launcher-hotkey";
    const wchar_t JSON_KEY_VALUE[] = L"value";
}

BOOL APIENTRY DllMain(HMODULE /*hModule*/, DWORD ul_reason_for_call, LPVOID /*lpReserved*/)
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
        Trace::Workspaces::RegisterProvider();
        break;

    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
        break;

    case DLL_PROCESS_DETACH:
        Trace::Workspaces::UnregisterProvider();
        break;
    }
    return TRUE;
}

class WorkspacesModuleInterface : public PowertoyModuleIface
{
public:
    EventWaiter m_toggleEditorEventWaiter;

    // Return the localized display name of the powertoy
    virtual PCWSTR get_name() override
    {
        return app_name.c_str();
    }

    // Return the non localized key of the powertoy, this will be cached by the runner
    virtual const wchar_t* get_key() override
    {
        return app_key.c_str();
    }

    virtual std::optional<HotkeyEx> GetHotkeyEx() override
    {
        return m_hotkey;
    }

    virtual void OnHotkeyEx() override
    {
        if (is_process_running())
        {
            sendHotkeyEvent();
        }
        else
        {
            launch_editor();
        }
    }

    // Return the configured status for the gpo policy for the module
    virtual powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
    {
        return powertoys_gpo::getConfiguredWorkspacesEnabledValue();
    }

    // Return JSON with the configuration options.
    // These are the settings shown on the settings page along with their current values.
    virtual bool get_config(_Out_ PWSTR buffer, _Out_ int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        // Create a Settings object.
        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.set_description(GET_RESOURCE_STRING(IDS_WORKSPACES_SETTINGS_DESC));
        settings.set_overview_link(L"https://aka.ms/PowerToysOverview_Workspaces");

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    // Passes JSON with the configuration settings for the powertoy.
    // This is called when the user hits Save on the settings page.
    virtual void set_config(PCWSTR config) override
    {
        try
        {
            // Parse the input JSON string.
            PowerToysSettings::PowerToyValues values = PowerToysSettings::PowerToyValues::from_json_string(config, get_key());

            parse_hotkeys(values);

            auto settingsObject = values.get_raw_json();
            values.save_to_settings_file();
        }
        catch (std::exception&)
        {
            // Improper JSON.
        }
    }

    // Signal from the Settings editor to call a custom action.
    // This can be used to spawn more complex editors.
    virtual void call_custom_action(const wchar_t* /*action*/) override
    {
        SetEvent(m_toggleEditorEvent);
    }

    // Enable the powertoy
    virtual void enable()
    {
        Logger::info("Workspaces enabling");
        Enable();
    }

    // Disable the powertoy
    virtual void disable()
    {
        Logger::info("Workspaces disabling");
        Disable(true);
    }

    // Returns if the powertoy is enabled
    virtual bool is_enabled() override
    {
        Logger::info("enabled = {}", m_enabled);
        return m_enabled;
    }

    // Destroy the powertoy and free memory
    virtual void destroy() override
    {
        Disable(false);

        if (m_toggleEditorEvent)
        {
            CloseHandle(m_toggleEditorEvent);
            m_toggleEditorEvent = nullptr;
        }

        if (m_hotkeyEvent)
        {
            CloseHandle(m_hotkeyEvent);
            m_hotkeyEvent = nullptr;
        }

        delete this;
    }

    virtual void send_settings_telemetry() override
    {
    }

    WorkspacesModuleInterface()
    {
        app_name = GET_RESOURCE_STRING(IDS_WORKSPACES_NAME);
        app_key = L"Workspaces";
        LoggerHelpers::init_logger(app_key, L"ModuleInterface", "Workspaces");
        init_settings();

        m_hotkeyEvent = CreateEventW(nullptr, false, false, CommonSharedConstants::WORKSPACES_HOTKEY_EVENT);
        if (!m_hotkeyEvent)
        {
            Logger::warn(L"Failed to create hotkey event. {}", get_last_error_or_default(GetLastError()));
        }

        m_toggleEditorEvent = CreateDefaultEvent(CommonSharedConstants::WORKSPACES_LAUNCH_EDITOR_EVENT);
        if (!m_toggleEditorEvent)
        {
            Logger::error(L"Failed to create launch editor event");
            auto message = get_last_error_message(GetLastError());
            if (message.has_value())
            {
                Logger::error(message.value());
            }
        }
        m_toggleEditorEventWaiter = EventWaiter(CommonSharedConstants::WORKSPACES_LAUNCH_EDITOR_EVENT, [&](int err) {
            if (err == ERROR_SUCCESS)
            {
                Logger::trace(L"{} event was signaled", CommonSharedConstants::WORKSPACES_LAUNCH_EDITOR_EVENT);
                launch_editor();
            }
        });
    }

private:
    void Enable()
    {
        Logger::info("Enable");
        m_enabled = true;

        Trace::Workspaces::Enable(true);

        unsigned long powertoys_pid = GetCurrentProcessId();
        std::wstring executable_args = L"";
        executable_args.append(std::to_wstring(powertoys_pid));
    }

    void sendHotkeyEvent()
    {
        Logger::trace(L"Signaled hotkey event");
        if (!SetEvent(m_hotkeyEvent))
        {
            Logger::warn(L"Failed to signal hotkey event. {}", get_last_error_or_default(GetLastError()));
        }
    }

    void Disable(bool const traceEvent)
    {
        Logger::info("Disable");
        m_enabled = false;
        if (traceEvent)
        {
            Trace::Workspaces::Enable(false);
        }

        if (m_toggleEditorEvent)
        {
            ResetEvent(m_toggleEditorEvent);
        }

        if (m_hotkeyEvent)
        {
            ResetEvent(m_hotkeyEvent);
        }

        if (m_hProcess)
        {
            TerminateProcess(m_hProcess, 0);
            m_hProcess = nullptr;
        }
    }

    // Load the settings file.
    void init_settings()
    {
        try
        {
            Logger::trace(L"Read settings {}", get_key());
            // Load and parse the settings file for this PowerToy.
            PowerToysSettings::PowerToyValues settings = PowerToysSettings::PowerToyValues::load_from_settings_file(get_key());
            parse_hotkeys(settings);
        }
        catch (std::exception&)
        {
            Logger::warn(L"An exception occurred while loading the settings file");
            // Error while loading from the settings file. Let default values stay as they are.
        }
    }

    void parse_hotkeys(PowerToysSettings::PowerToyValues& settings)
    {
        auto settingsObject = settings.get_raw_json();

        if (settingsObject.GetView().Size())
        {
            if (settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).HasKey(JSON_KEY_RUN_EDITOR_HOTKEY))
            {
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_RUN_EDITOR_HOTKEY).GetNamedObject(JSON_KEY_VALUE);
                auto hotkey = PowerToysSettings::HotkeyObject::from_json(jsonPropertiesObject);
                m_hotkey = HotkeyEx();
                if (hotkey.win_pressed())
                {
                    m_hotkey.modifiersMask |= MOD_WIN;
                }

                if (hotkey.ctrl_pressed())
                {
                    m_hotkey.modifiersMask |= MOD_CONTROL;
                }

                if (hotkey.shift_pressed())
                {
                    m_hotkey.modifiersMask |= MOD_SHIFT;
                }

                if (hotkey.alt_pressed())
                {
                    m_hotkey.modifiersMask |= MOD_ALT;
                }

                m_hotkey.vkCode = static_cast<WORD>(hotkey.get_code());
            }
        }
    }

    void launch_editor()
    {
        Logger::trace(L"Starting Workspaces Editor");

        unsigned long powertoys_pid = GetCurrentProcessId();
        std::wstring executable_args = L"";
        executable_args.append(std::to_wstring(powertoys_pid));

        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = SEE_MASK_NOCLOSEPROCESS;
        sei.lpFile = L"PowerToys.WorkspacesEditor.exe";
        sei.nShow = SW_SHOWNORMAL;
        sei.lpParameters = executable_args.data();
        if (ShellExecuteExW(&sei))
        {
            Logger::trace("Successfully started the Workspaces Editor");
        }
        else
        {
            Logger::error(L"Workspaces Editor failed to start. {}", get_last_error_or_default(GetLastError()));
        }

        m_hProcess = sei.hProcess;
    }

    bool is_process_running() const
    {
        return WaitForSingleObject(m_hProcess, 0) == WAIT_TIMEOUT;
    }

    std::wstring app_name;
    //contains the non localized key of the powertoy
    std::wstring app_key;

    bool m_enabled = false;
    HANDLE m_hProcess = nullptr;

    // Handle to event used to invoke Workspaces Editor
    HANDLE m_toggleEditorEvent;

    // Handle to event used when hotkey is invoked
    HANDLE m_hotkeyEvent;

    // Hotkey to invoke the module
    HotkeyEx m_hotkey{
        .modifiersMask = MOD_CONTROL | MOD_WIN,
        .vkCode = 0xC0, // VK_OEM_3 key; usually `~
    };
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new WorkspacesModuleInterface();
}
