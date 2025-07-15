#include "pch.h"

#include <interface/powertoy_module_interface.h>
#include <common/SettingsAPI/settings_objects.h>

#include <common/logger/logger.h>
#include <common/utils/logger_helper.h>
#include <common/utils/resources.h>
#include <common/utils/winapi_error.h>

#include <shellapi.h>

#include "trace.h"
#include "common/interop/shared_constants.h"

extern "C" IMAGE_DOS_HEADER __ImageBase;

BOOL APIENTRY DllMain(HMODULE /* hModule*/, DWORD ul_reason_for_call, LPVOID /*lpReserved*/)
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

// The PowerToy name that will be shown in the settings.
const static wchar_t* MODULE_NAME = L"$projectname$";
// Add a description that will we shown in the module settings page.
const static wchar_t* MODULE_DESC = L"<no description>";

// These are the properties shown in the Settings page.
struct ModuleSettings
{
    // Add the PowerToy module properties with default values.
    // Currently available types:
    // - int
    // - bool
    // - string

    //bool bool_prop = true;
    //int int_prop = 10;
    //std::wstring string_prop = L"The quick brown fox jumps over the lazy dog";
    //std::wstring color_prop = L"#1212FF";

} g_settings;

// Implement the PowerToy Module Interface and all the required methods.
class ClipPingModuleInterface : public PowertoyModuleIface
{
private:
    // The PowerToy state.
    bool m_enabled = false;

    // Load initial settings from the persisted values.
    void init_settings();

    std::wstring app_name;
    std::wstring app_key; //contains the non localized key of the powertoy

    HANDLE m_hProcess = nullptr;
    HANDLE m_exit_event_handle = nullptr;

public:
    // Constructor
    ClipPingModuleInterface()
    {
        // TODO: localization?
        app_name = L"ClipPing";
        app_key = L"ClipPing";
        LoggerHelpers::init_logger(app_key, L"ModuleInterface", "ClipPing");
        m_exit_event_handle = CreateDefaultEvent(CommonSharedConstants::CLIPPING_EXIT_EVENT);
        init_settings();
    };

    // Destroy the powertoy and free memory
    virtual void destroy() override
    {
        Disable(false);
        delete this;
    }

    // Return the display name of the powertoy, this will be cached by the runner
    virtual const wchar_t* get_name() override
    {
        return MODULE_NAME;
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

        // Show an overview link in the Settings page
        //settings.set_overview_link(L"https://");

        // Show a video link in the Settings page.
        //settings.set_video_link(L"https://");

        // A bool property with a toggle editor.
        //settings.add_bool_toogle(
        //  L"bool_toggle_1", // property name.
        //  L"This is what a BoolToggle property looks like", // description or resource id of the localized string.
        //  g_settings.bool_prop // property value.
        //);

        // An integer property with a spinner editor.
        //settings.add_int_spinner(
        //  L"int_spinner_1", // property name
        //  L"This is what a IntSpinner property looks like", // description or resource id of the localized string.
        //  g_settings.int_prop, // property value.
        //  0, // min value.
        //  100, // max value.
        //  10 // incremental step.
        //);

        // A string property with a textbox editor.
        //settings.add_string(
        //  L"string_text_1", // property name.
        //  L"This is what a String property looks like", // description or resource id of the localized string.
        //  g_settings.string_prop // property value.
        //);

        // A string property with a color picker editor.
        //settings.add_color_picker(
        //  L"color_picker_1", // property name.
        //  L"This is what a ColorPicker property looks like", // description or resource id of the localized string.
        //  g_settings.color_prop // property value.
        //);

        // A custom action property. When using this settings type, the "PowertoyModuleIface::call_custom_action()"
        // method should be overriden as well.
        //settings.add_custom_action(
        //  L"custom_action_id", // action name.
        //  L"This is what a CustomAction property looks like", // label above the field.
        //  L"Call a custom action", // button text.
        //  L"Press the button to call a custom action." // display values / extended info.
        //);

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    // Signal from the Settings editor to call a custom action.
    // This can be used to spawn more complex editors.
    virtual void call_custom_action(const wchar_t* action) override
    {
        static UINT custom_action_num_calls = 0;
        try
        {
            // Parse the action values, including name.
            PowerToysSettings::CustomActionObject action_object =
                PowerToysSettings::CustomActionObject::from_json_string(action);

            //if (action_object.get_name() == L"custom_action_id") {
            //  // Execute your custom action
            //}
        }
        catch (std::exception&)
        {
            // Improper JSON.
        }
    }

    // Called by the runner to pass the updated settings values as a serialized JSON.
    virtual void set_config(const wchar_t* config) override
    {
        try
        {
            // Parse the input JSON string.
            PowerToysSettings::PowerToyValues values =
                PowerToysSettings::PowerToyValues::from_json_string(config, get_key());

            // Update a bool property.
            //if (auto v = values.get_bool_value(L"bool_toggle_1")) {
            //  g_settings.bool_prop = *v;
            //}

            // Update an int property.
            //if (auto v = values.get_int_value(L"int_spinner_1")) {
            //  g_settings.int_prop = *v;
            //}

            // Update a string property.
            //if (auto v = values.get_string_value(L"string_text_1")) {
            //  g_settings.string_prop = *v;
            //}

            // Update a color property.
            //if (auto v = values.get_string_value(L"color_picker_1")) {
            //  g_settings.color_prop = *v;
            //}

            // If you don't need to do any custom processing of the settings, proceed
            // to persists the values calling:
            values.save_to_settings_file();
            // Otherwise call a custom function to process the settings before saving them to disk:
            // save_settings();
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
        Trace::Enable(true);

        ResetEvent(m_exit_event_handle);

        unsigned long powertoys_pid = GetCurrentProcessId();
        std::wstring executable_args;
        executable_args.append(std::to_wstring(powertoys_pid));

        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = { SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI };
        sei.lpFile = L"WinUI3Apps\\Powertoys.ClipPing.exe";
        sei.nShow = SW_SHOWNORMAL;
        sei.lpParameters = executable_args.data();
        if (ShellExecuteExW(&sei) == false)
        {
            Logger::error(L"Failed to start ClipPing");
            auto message = get_last_error_message(GetLastError());
            if (message.has_value())
            {
                Logger::error(message.value());
            }
        }
        else
        {
            m_hProcess = sei.hProcess;
        }

    }

    // Disable the powertoy
    virtual void disable()
    {
        m_enabled = false;
        Disable(true);
    }

    // Returns if the powertoys is enabled
    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    void Disable(bool const traceEvent)
    {
        m_enabled = false;

        // Log telemetry
        if (traceEvent)
        {
            Trace::Enable(false);
        }

        // Tell the ClipPing process to exit.
        SetEvent(m_exit_event_handle);

        // Wait for 1.5 seconds for the process to end correctly and stop etw tracer
        WaitForSingleObject(m_hProcess, 1500);

        // If process is still running, terminate it
        if (m_hProcess)
        {
            TerminateProcess(m_hProcess, 0);
            m_hProcess = nullptr;
        }
    }
};

// Load the settings file.
void ClipPingModuleInterface::init_settings()
{
    try
    {
        // Load and parse the settings file for this PowerToy.
        PowerToysSettings::PowerToyValues settings =
            PowerToysSettings::PowerToyValues::load_from_settings_file(ClipPingModuleInterface::get_name());

        // Load a bool property.
        //if (auto v = settings.get_bool_value(L"bool_toggle_1")) {
        //  g_settings.bool_prop = *v;
        //}

        // Load an int property.
        //if (auto v = settings.get_int_value(L"int_spinner_1")) {
        //  g_settings.int_prop = *v;
        //}

        // Load a string property.
        //if (auto v = settings.get_string_value(L"string_text_1")) {
        //  g_settings.string_prop = *v;
        //}

        // Load a color property.
        //if (auto v = settings.get_string_value(L"color_picker_1")) {
        //  g_settings.color_prop = *v;
        //}
    }
    catch (std::exception&)
    {
        // Error while loading from the settings file. Let default values stay as they are.
    }
}

// This method of saving the module settings is only required if you need to do any
// custom processing of the settings before saving them to disk.
//void $projectname$::save_settings() {
//  try {
//    // Create a PowerToyValues object for this PowerToy
//    PowerToysSettings::PowerToyValues values(get_name());
//
//    // Save a bool property.
//    //values.add_property(
//    //  L"bool_toggle_1", // property name
//    //  g_settings.bool_prop // property value
//    //);
//
//    // Save an int property.
//    //values.add_property(
//    //  L"int_spinner_1", // property name
//    //  g_settings.int_prop // property value
//    //);
//
//    // Save a string property.
//    //values.add_property(
//    //  L"string_text_1", // property name
//    //  g_settings.string_prop // property value
//    );
//
//    // Save a color property.
//    //values.add_property(
//    //  L"color_picker_1", // property name
//    //  g_settings.color_prop // property value
//    //);
//
//    // Save the PowerToyValues JSON to the power toy settings file.
//    values.save_to_settings_file();
//  }
//  catch (std::exception ex) {
//    // Couldn't save the settings.
//  }
//}

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new ClipPingModuleInterface();
}