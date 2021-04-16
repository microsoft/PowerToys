#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/interop/shared_constants.h>
#include <common/utils/resources.h>
#include "Generated Files/resource.h"
#include <keyboardmanager/common/KeyboardManagerState.h>
#include <keyboardmanager/common/Shortcut.h>
#include <keyboardmanager/common/RemapShortcut.h>
#include <keyboardmanager/common/KeyboardManagerConstants.h>
#include <common/debug_control.h>
#include <common/utils/winapi_error.h>
#include <common/logger/logger_settings.h>
#include <keyboardmanager/common/trace.h>
#include <keyboardmanager/common/Helpers.h>
#include <shellapi.h>

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
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

// Implement the PowerToy Module Interface and all the required methods.
class KeyboardManager : public PowertoyModuleIface
{
private:
    // The PowerToy state.
    bool m_enabled = false;

    // The PowerToy name that will be shown in the settings.
    const std::wstring app_name = GET_RESOURCE_STRING(IDS_KEYBOARDMANAGER);

    //contains the non localized key of the powertoy
    std::wstring app_key = KeyboardManagerConstants::ModuleName;

    // Low level hook handles
    static HHOOK hook_handle;

    // Required for Unhook in old versions of Windows
    static HHOOK hook_handle_copy;

    // Static pointer to the current keyboardmanager object required for accessing the HandleKeyboardHookEvent function in the hook procedure (Only global or static variables can be accessed in a hook procedure CALLBACK)
    static KeyboardManager* keyboardmanager_object_ptr;

    // Variable which stores all the state information to be shared between the UI and back-end
    KeyboardManagerState keyboardManagerState;

    HANDLE m_hProcess = nullptr;
public:
    // Constructor
    KeyboardManager()
    {
        std::filesystem::path logFilePath(PTSettingsHelper::get_module_save_folder_location(app_key));
        logFilePath.append(LogSettings::keyboardManagerLogPath);
        Logger::init(LogSettings::keyboardManagerLoggerName, logFilePath.wstring(), PTSettingsHelper::get_log_settings_file_location());

        // Load the initial configuration.
        load_config();

        // Set the static pointer to the newest object of the class
        keyboardmanager_object_ptr = this;
    };

    // Load config from the saved settings.
    void load_config()
    {
        try
        {
            PowerToysSettings::PowerToyValues settings =
                PowerToysSettings::PowerToyValues::load_from_settings_file(get_key());
            auto current_config = settings.get_string_value(KeyboardManagerConstants::ActiveConfigurationSettingName);

            if (current_config)
            {
                keyboardManagerState.SetCurrentConfigName(*current_config);
                // Read the config file and load the remaps.
                auto configFile = json::from_file(PTSettingsHelper::get_module_save_folder_location(KeyboardManagerConstants::ModuleName) + L"\\" + *current_config + L".json");
                if (configFile)
                {
                    auto jsonData = *configFile;

                    // Load single key remaps
                    try
                    {
                        auto remapKeysData = jsonData.GetNamedObject(KeyboardManagerConstants::RemapKeysSettingName);
                        keyboardManagerState.ClearSingleKeyRemaps();

                        if (remapKeysData)
                        {
                            auto inProcessRemapKeys = remapKeysData.GetNamedArray(KeyboardManagerConstants::InProcessRemapKeysSettingName);
                            for (const auto& it : inProcessRemapKeys)
                            {
                                try
                                {
                                    auto originalKey = it.GetObjectW().GetNamedString(KeyboardManagerConstants::OriginalKeysSettingName);
                                    auto newRemapKey = it.GetObjectW().GetNamedString(KeyboardManagerConstants::NewRemapKeysSettingName);

                                    // If remapped to a shortcut
                                    if (std::wstring(newRemapKey).find(L";") != std::string::npos)
                                    {
                                        keyboardManagerState.AddSingleKeyRemap(std::stoul(originalKey.c_str()), Shortcut(newRemapKey.c_str()));
                                    }

                                    // If remapped to a key
                                    else
                                    {
                                        keyboardManagerState.AddSingleKeyRemap(std::stoul(originalKey.c_str()), std::stoul(newRemapKey.c_str()));
                                    }
                                }
                                catch (...)
                                {
                                    // Improper Key Data JSON. Try the next remap.
                                }
                            }
                        }
                    }
                    catch (...)
                    {
                        // Improper JSON format for single key remaps. Skip to next remap type
                    }

                    // Load shortcut remaps
                    try
                    {
                        auto remapShortcutsData = jsonData.GetNamedObject(KeyboardManagerConstants::RemapShortcutsSettingName);
                        keyboardManagerState.ClearOSLevelShortcuts();
                        keyboardManagerState.ClearAppSpecificShortcuts();
                        if (remapShortcutsData)
                        {
                            // Load os level shortcut remaps
                            try
                            {
                                auto globalRemapShortcuts = remapShortcutsData.GetNamedArray(KeyboardManagerConstants::GlobalRemapShortcutsSettingName);
                                for (const auto& it : globalRemapShortcuts)
                                {
                                    try
                                    {
                                        auto originalKeys = it.GetObjectW().GetNamedString(KeyboardManagerConstants::OriginalKeysSettingName);
                                        auto newRemapKeys = it.GetObjectW().GetNamedString(KeyboardManagerConstants::NewRemapKeysSettingName);

                                        // If remapped to a shortcut
                                        if (std::wstring(newRemapKeys).find(L";") != std::string::npos)
                                        {
                                            keyboardManagerState.AddOSLevelShortcut(Shortcut(originalKeys.c_str()), Shortcut(newRemapKeys.c_str()));
                                        }

                                        // If remapped to a key
                                        else
                                        {
                                            keyboardManagerState.AddOSLevelShortcut(Shortcut(originalKeys.c_str()), std::stoul(newRemapKeys.c_str()));
                                        }
                                    }
                                    catch (...)
                                    {
                                        // Improper Key Data JSON. Try the next shortcut.
                                    }
                                }
                            }
                            catch (...)
                            {
                                // Improper JSON format for os level shortcut remaps. Skip to next remap type
                            }

                            // Load app specific shortcut remaps
                            try
                            {
                                auto appSpecificRemapShortcuts = remapShortcutsData.GetNamedArray(KeyboardManagerConstants::AppSpecificRemapShortcutsSettingName);
                                for (const auto& it : appSpecificRemapShortcuts)
                                {
                                    try
                                    {
                                        auto originalKeys = it.GetObjectW().GetNamedString(KeyboardManagerConstants::OriginalKeysSettingName);
                                        auto newRemapKeys = it.GetObjectW().GetNamedString(KeyboardManagerConstants::NewRemapKeysSettingName);
                                        auto targetApp = it.GetObjectW().GetNamedString(KeyboardManagerConstants::TargetAppSettingName);

                                        // If remapped to a shortcut
                                        if (std::wstring(newRemapKeys).find(L";") != std::string::npos)
                                        {
                                            keyboardManagerState.AddAppSpecificShortcut(targetApp.c_str(), Shortcut(originalKeys.c_str()), Shortcut(newRemapKeys.c_str()));
                                        }

                                        // If remapped to a key
                                        else
                                        {
                                            keyboardManagerState.AddAppSpecificShortcut(targetApp.c_str(), Shortcut(originalKeys.c_str()), std::stoul(newRemapKeys.c_str()));
                                        }
                                    }
                                    catch (...)
                                    {
                                        // Improper Key Data JSON. Try the next shortcut.
                                    }
                                }
                            }
                            catch (...)
                            {
                                // Improper JSON format for os level shortcut remaps. Skip to next remap type
                            }
                        }
                    }
                    catch (...)
                    {
                        // Improper JSON format for shortcut remaps. Skip to next remap type
                    }
                }
            }
        }
        catch (...)
        {
            // Unable to load inital config.
        }
    }

    // Destroy the powertoy and free memory
    virtual void destroy() override
    {
        delete this;
    }

    // Return the localized display name of the powertoy
    virtual const wchar_t* get_name() override
    {
        return app_name.c_str();
    }

    // Return the non localized key of the powertoy, this will be cached by the runner
    virtual const wchar_t* get_key() override
    {
        return app_key.c_str();
    }

    // Return JSON with the configuration options.
    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        // Create a Settings object.
        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.set_description(IDS_SETTINGS_DESCRIPTION);
        settings.set_overview_link(L"https://aka.ms/PowerToysOverview_KeyboardManager");

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    // Signal from the Settings editor to call a custom action.
    virtual void call_custom_action(const wchar_t* action) override
    {
    }

    // Called by the runner to pass the updated settings values as a serialized JSON.
    virtual void set_config(const wchar_t* config) override
    {
        try
        {
            // Parse the input JSON string.
            PowerToysSettings::PowerToyValues values =
                PowerToysSettings::PowerToyValues::from_json_string(config, get_key());

            // If you don't need to do any custom processing of the settings, proceed
            // to persists the values calling:
            values.save_to_settings_file();
        }
        catch (std::exception&)
        {
            // Improper JSON.
        }
    }

    // Enable the powertoy
    virtual void enable()
    {
        m_enabled = true;
        // Log telemetry
        Trace::EnableKeyboardManager(true);
        
        unsigned long powertoys_pid = GetCurrentProcessId();
        std::wstring executable_args = L"";
        executable_args.append(std::to_wstring(powertoys_pid));

        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = { SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI };
        sei.lpFile = L"modules\\KeyboardManager\\KeyboardManagerEngine\\PowerToys.KeyboardManager.exe";
        sei.nShow = SW_SHOWNORMAL;
        sei.lpParameters = executable_args.data();
        if (ShellExecuteExW(&sei) == false)
        {
            Logger::error(L"Failed to start keyboard manager engine");
            auto message = get_last_error_message(GetLastError());
            if (message.has_value())
            {
                Logger::error(message.value());
            }
        }
        else
        {
            m_hProcess = sei.hProcess;
            SetPriorityClass(m_hProcess, REALTIME_PRIORITY_CLASS);
        }
    }

    // Disable the powertoy
    virtual void disable()
    {
        m_enabled = false;
        // Log telemetry
        Trace::EnableKeyboardManager(false);

        if (m_hProcess)
        {
            TerminateProcess(m_hProcess, 0);
            m_hProcess = nullptr;
        }
    }

    // Returns if the powertoys is enabled
    virtual bool is_enabled() override
    {
        return m_enabled;
    }
};

HHOOK KeyboardManager::hook_handle = nullptr;
HHOOK KeyboardManager::hook_handle_copy = nullptr;
KeyboardManager* KeyboardManager::keyboardmanager_object_ptr = nullptr;

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new KeyboardManager();
}