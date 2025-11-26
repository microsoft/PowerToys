#include "pch.h"

#include <interface/powertoy_module_interface.h>
#include <interface/lowlevel_keyboard_event_data.h>
#include <interface/win_hook_event_data.h>
#include <common/settings_objects.h>
#include "trace.h"
#include <common/utils/gpo.h>

#include "EventQueue.h"
#include "Batcher.h"
#include "KeystrokeEvent.h"

extern "C" IMAGE_DOS_HEADER __ImageBase;

const std::wstring PIPE_NAME = L"\\\\.\\pipe\\PowerToys.KeystrokeOverlay";
static SpscRing<KeystrokeEvent, 256> g_eventQueue;
static Batcher g_batcher(g_eventQueue, PIPE_NAME);

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

// The PowerToy name that will be shown in the settings.
const static wchar_t* MODULE_NAME = L"Keystroke Overlay";
// Add a description that will we shown in the module settings page.
const static wchar_t* MODULE_DESC = L"<no description>";

// These are the properties shown in the Settings page.
struct ModuleSettings
{
    // Add the PowerToy module properties with default values.

    bool is_draggable = true;
    int overlay_timeout = 3000;
    
    int text_size = 24;
    int text_opacity = 100;

    int bg_opacity = 50;

    std::wstring text_color = L"#FFFFFF";
    std::wstring bg_color = L"#000000";

} g_settings;

// Implement the PowerToy Module Interface and all the required methods.
class KeystrokeOverlay : public KeystrokeOverlayInterface
{
private:
    // The PowerToy state.
    bool m_enabled = false;

    // Load initial settings from the persisted values.
    void init_settings();

public:
    // Constructor
    KeystrokeOverlay()
    {
        init_settings();
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

    // // Return array of the names of all events that this powertoy listens for, with
    // // nullptr as the last element of the array. Nullptr can also be retured for empty
    // // list.
    // virtual const wchar_t** get_events() override
    // {
    //     static const wchar_t* events[] = { nullptr };
    //     // Available events:
    //     // - ll_keyboard
    //     // - win_hook_event
    //     //
    //     // static const wchar_t* events[] = { ll_keyboard,
    //     //                                   win_hook_event,
    //     //                                   nullptr };

    //     return events;
    // }

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

        // isDraggableOverlayEnabled Toggle
        settings.add_bool_toggle(
            L"IsDraggableOverlayEnabled",   // property name.
            L"Enable Draggable Overlay",    // description or resource id of the localized string.
            g_settings.is_draggable         // property value.
        );

        // Overlay Timeout Spinner
        settings.add_int_spinner(
            L"OverlayTimeout",              // property name
            L"Overlay Timeout (ms)",        // description or resource id of the localized string.
            g_settings.overlay_timeout,     // property value.
            500,                            // Min
            10000,                          // Max
            100                             // Step
        );

        // 3. Text Size Spinner
        settings.add_int_spinner(
            L"TextSize",                    // property name
            L"Text Font Size",              // description or resource id of the localized string.    
            g_settings.text_size,           // property value.
            10,                             // Min
            72,                             // Max
            2                               // Step
        );

        // 4. Text Opacity
        settings.add_int_spinner(
            L"TextOpacity",                 // property name
            L"Text Opacity (%)",            // description or resource id of the localized string.
            g_settings.text_opacity,        // property value.
            0,                              // Min
            100,                            // Max
            5                               // Step
        );

        // 5. Background Opacity
        settings.add_int_spinner(
            L"BackgroundOpacity",           // property name
            L"Background Opacity (%)",      // description or resource id of the localized string.
            g_settings.bg_opacity,          // property value.
            0,                              // Min
            100,                            // Max
            5                               // Step
        );

        // 6. Text Color
        settings.add_color_picker(
            L"TextColor",                   // property name.
            L"Text Color",                   // description or resource id of the localized string.
            g_settings.text_color            // property value.
        );

        // 7. Background Color
        settings.add_color_picker(
            L"BackgroundColor",               // property name.
            L"Background Color",               // description or resource id of the localized string.
            g_settings.bg_color                // property value.
        );

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    // Return the configured status for the gpo policy for the module
    virtual powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
    {
        return powertoys_gpo::getConfiguredKeystrokeOverlayEnabledValue();
    }

    // // Signal from the Settings editor to call a custom action.
    // // This can be used to spawn more complex editors.
    // virtual void call_custom_action(const wchar_t* action) override
    // {
    //     static UINT custom_action_num_calls = 0;
    //     try
    //     {
    //         // Parse the action values, including name.
    //         PowerToysSettings::CustomActionObject action_object =
    //             PowerToysSettings::CustomActionObject::from_json_string(action);

    //         //if (action_object.get_name() == L"custom_action_id") {
    //         //  // Execute your custom action
    //         //}
    //     }
    //     catch (std::exception&)
    //     {
    //         // Improper JSON.
    //     }
    // }

    // Called by the runner to pass the updated settings values as a serialized JSON.
    virtual void set_config(const wchar_t* config) override
    {
        try
        {
            // Parse the input JSON string.
            PowerToysSettings::PowerToyValues values =
                PowerToysSettings::PowerToyValues::from_json_string(config);

            if (auto v = values.get_bool_value(L"IsDraggableOverlayEnabled")) {
                g_settings.is_draggable = *v;
            }
            if (auto v = values.get_int_value(L"OverlayTimeout")) {
                g_settings.overlay_timeout = *v;
            }
            if (auto v = values.get_int_value(L"TextSize")) {
                g_settings.text_size = *v;
            }
            if (auto v = values.get_int_value(L"TextOpacity")) {
                g_settings.text_opacity = *v;
            }
            if (auto v = values.get_int_value(L"BackgroundOpacity")) {
                g_settings.bg_opacity = *v;
            }
            if (auto v = values.get_string_value(L"TextColor")) {
                g_settings.text_color = *v;
            }
            if (auto v = values.get_string_value(L"BackgroundColor")) {
                g_settings.bg_color = *v;
            }

            // Save to disk so the C# App can read the updated settings.json
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
        g_batcher.Start(); // Start the worker thread
    }

    // Disable the powertoy
    virtual void disable()
    {
        m_enabled = false;
        g_batcher.Stop(); // Stop the worker thread
    }

    // Returns if the powertoys is enabled
    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    // Handle incoming event, data is event-specific
    virtual intptr_t signal_event(const wchar_t* name, intptr_t data) override
    {
        if (wcscmp(name, ll_keyboard) == 0)
        {
            if (!m_enabled) return 0;

            auto& event = *(reinterpret_cast<LowlevelKeyboardEvent*>(data));
            
            // 3. Convert WinHook data to your KeystrokeEvent
            KeystrokeEvent kEvent;
            
            // Map WM_ messages to your Type enum
            if (event.wParam == WM_KEYDOWN || event.wParam == WM_SYSKEYDOWN) 
                kEvent.type = KeystrokeEvent::Type::Down;
            else if (event.wParam == WM_KEYUP || event.wParam == WM_SYSKEYUP) 
                kEvent.type = KeystrokeEvent::Type::Up;
            else 
                return 0; // Ignore others for now

            kEvent.vk = event.lParam->vkCode;
            kEvent.ts_micros = GetTickCount64() * 1000; 
            kEvent.ch = 0; // Character translation requires MapVirtualKey or ToUnicode APIs here if desired

            // Capture Modifier states (simplified for example)
            kEvent.mods[0] = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
            kEvent.mods[1] = (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
            kEvent.mods[2] = (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;
            kEvent.mods[3] = (GetAsyncKeyState(VK_LWIN) & 0x8000) != 0 || (GetAsyncKeyState(VK_RWIN) & 0x8000) != 0;

            // Push to queue (non-blocking)
            g_eventQueue.try_push(kEvent);

            return 0;
        }
        else if (wcscmp(name, win_hook_event) == 0)
        {
            auto& event = *(reinterpret_cast<WinHookEvent*>(data));
            // Return value is ignored
            return 0;
        }
        return 0;
    }

    // This methods are part of an experimental features not fully supported yet
    virtual void register_system_menu_helper(PowertoySystemMenuIface* helper) override
    {
    }

    virtual void signal_system_menu_action(const wchar_t* name) override
    {
    }
};

// Load the settings file.
void KeystrokeOverlay::init_settings()
{
    try
    {
        // Load and parse the settings file for this PowerToy.
        PowerToysSettings::PowerToyValues settings =
            PowerToysSettings::PowerToyValues::load_from_settings_file(KeystrokeOverlay::get_name());

        if (auto v = settings.get_bool_value(L"IsDraggableOverlayEnabled")) {
            g_settings.is_draggable = *v;
        }
        if (auto v = settings.get_int_value(L"OverlayTimeout")) {
            g_settings.overlay_timeout = *v;
        }
        if (auto v = settings.get_int_value(L"TextSize")) {
            g_settings.text_size = *v;
        }
        if (auto v = settings.get_int_value(L"TextOpacity")) {
            g_settings.text_opacity = *v;
        }
        if (auto v = settings.get_int_value(L"BackgroundOpacity")) {
            g_settings.bg_opacity = *v;
        }
        if (auto v = settings.get_string_value(L"TextColor")) {
            g_settings.text_color = *v;
        }
        if (auto v = settings.get_string_value(L"BackgroundColor")) {
            g_settings.bg_color = *v;
        }
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
    return new KeystrokeOverlay();
}