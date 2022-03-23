#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <common/logger/logger.h>
#include <common/SettingsAPI/settings_objects.h>
#include "trace.h"
#include <common/utils/winapi_error.h>
#include <filesystem>
#include <common/interop/shared_constants.h>

extern "C" IMAGE_DOS_HEADER __ImageBase;

BOOL APIENTRY DllMain(HMODULE hModule, 
                      DWORD ul_reason_for_call, 
                      LPVOID lpReserved)
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

namespace
{
    const wchar_t JSON_KEY_PROPERTIES[] = L"properties";
    const wchar_t JSON_KEY_WIN[] = L"win";
    const wchar_t JSON_KEY_ALT[] = L"alt";
    const wchar_t JSON_KEY_CTRL[] = L"ctrl";
    const wchar_t JSON_KEY_SHIFT[] = L"shift";
    const wchar_t JSON_KEY_CODE[] = L"code";
    const wchar_t JSON_KEY_ACTIVATION_SHORTCUT[] = L"ActivationShortcut";
}

// The PowerToy name that will be shown in the settings.
const static wchar_t* MODULE_NAME = L"Peek";
// Add a description that will we shown in the module settings page.
const static wchar_t* MODULE_DESC = L"A module that previews an image file.";

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
class Peek : public PowertoyModuleIface
{
private:
    // The PowerToy state.
    bool m_enabled = false;

    Hotkey m_hotkey;

    HANDLE m_hProcess;

    HANDLE m_hInvokeEvent;

    // Load the settings file.
    void init_settings()
    {
        try
        {
            // Load and parse the settings file for this PowerToy.
            PowerToysSettings::PowerToyValues settings =
                PowerToysSettings::PowerToyValues::load_from_settings_file(Peek::get_name());

            parse_settings(settings);

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

    void parse_settings(PowerToysSettings::PowerToyValues& settings)
    {
        auto settingsObject = settings.get_raw_json();
        if (settingsObject.GetView().Size())
        {
            try
            {
                auto jsonHotkeyObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_ACTIVATION_SHORTCUT);
                parse_hotkey(jsonHotkeyObject);
            }
            catch (...)
            {
                Logger::error("Failed to initialize Peek start settings");
            }
        }
        else
        {
            Logger::info("Peek settings are empty");

            set_default_settings();
        }
    }

    void set_default_settings()
    {
        Logger::info("Peek is going to use default settings");
        m_hotkey.win = false;
        m_hotkey.alt = false;
        m_hotkey.shift = false;
        m_hotkey.ctrl = true;
        m_hotkey.key = ' ';
    }

    void parse_hotkey(winrt::Windows::Data::Json::JsonObject& jsonHotkeyObject)
    {
        try
        {
            m_hotkey.win = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_WIN);
            m_hotkey.alt = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_ALT);
            m_hotkey.shift = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_SHIFT);
            m_hotkey.ctrl = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_CTRL);
            m_hotkey.key = static_cast<unsigned char>(jsonHotkeyObject.GetNamedNumber(JSON_KEY_CODE));
        }
        catch (...)
        {
            Logger::error("Failed to initialize Peek start shortcut");
        }

        if (!m_hotkey.key)
        {
            Logger::info("Peek is going to use default shortcut");
            m_hotkey.win = false;
            m_hotkey.alt = false;
            m_hotkey.shift = false;
            m_hotkey.ctrl = true;
            m_hotkey.key = ' ';
        }
    }

    bool is_viewer_running()
    {
        return WaitForSingleObject(m_hProcess, 0) == WAIT_TIMEOUT;
    }

    void launch_viewer()
    {
        Logger::trace(L"Starting PeekUI process");

        SHELLEXECUTEINFOW sei{ sizeof(sei) };

        sei.fMask = { SEE_MASK_NOCLOSEPROCESS };
        sei.lpVerb = L"open";
        sei.lpFile = L"modules\\Peek\\PeekUI\\PeekUI.exe";
        sei.nShow = SW_SHOWNORMAL;
        auto absolute = std::filesystem::absolute(L"..\\..\\src\\modules\\peek\\test\\andrew-lakersnoi-hq6EG8GdnZw-unsplash.jpg");
        sei.lpParameters = absolute.c_str();
        if (ShellExecuteExW(&sei))
        {
            Logger::trace("Successfully started the PeekViewer process");
        }
        else
        {
            Logger::error(L"PeekViewer failed to start. {}", get_last_error_or_default(GetLastError()));
        }

        m_hProcess = sei.hProcess;
    }

public:
    Peek()
    {
        init_settings();

        m_hInvokeEvent = CreateDefaultEvent(CommonSharedConstants::SHOW_PEEK_SHARED_EVENT);
    };

    ~Peek()
    {
        if (m_enabled)
        {
        }
        m_enabled = false;
    };

    // Destroy the powertoy and free memory
    virtual void destroy() override
    {
        delete this;
    }

    // Return the display name of the powertoy, this will be cached by the runner
    virtual const wchar_t* get_name() override
    {
        return MODULE_NAME;
    }

    virtual const wchar_t* get_key() override
    {
        return MODULE_NAME;
    }

    // Return JSON with the configuration options.
    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        // Create a Settings object.
        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.set_description(MODULE_DESC);

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
        ResetEvent(m_hInvokeEvent);
        launch_viewer();
        m_enabled = true;
    }

    // Disable the powertoy
    virtual void disable()
    {
        if (m_enabled)
        {
            ResetEvent(m_hInvokeEvent);
            TerminateProcess(m_hProcess, 1);
        }

        m_enabled = false;
    }

    // Returns if the powertoys is enabled
    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    virtual size_t get_hotkeys(Hotkey* hotkeys, size_t buffer_size) override
    {
        if (m_hotkey.key)
        {
            if (hotkeys && buffer_size >= 1)
            {
                hotkeys[0] = m_hotkey;
            }

            return 1;
        }
        else
        {
            return 0;
        }
    }

    virtual bool on_hotkey(size_t hotkeyId) override
    {
        if (m_enabled)
        {
            Logger::trace(L"Peek hotkey pressed");
            
            if (!is_viewer_running())
            {
                launch_viewer();
            }

            SetEvent(m_hInvokeEvent);
        }

        return false;
    }
};

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
    return new Peek();
}