#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <interface/lowlevel_keyboard_event_data.h>
#include <interface/win_hook_event_data.h>
#include <common/settings_objects.h>
#include "trace.h"
#include "resource.h"

extern "C" IMAGE_DOS_HEADER __ImageBase;

BOOL APIENTRY DllMain(HMODULE hModule, DWORD  ul_reason_for_call, LPVOID lpReserved) {
  switch (ul_reason_for_call) {
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


// These are the properties shown in the Settings page.
struct ModuleSettings {
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
class Wox_Launcher : public PowertoyModuleIface {
private:
  // The PowerToy state.
  bool m_enabled = false;

  // Load initial settings from the persisted values.
  void init_settings();

  // Handle to launch and terminate the launcher
  HANDLE m_hProcess;

  //contains the name of the powerToys
  std::wstring app_name;

public:
  // Constructor
  Wox_Launcher() {
    app_name = GET_RESOURCE_STRING(IDS_LAUNCHER_NAME);
    init_settings();
  };

  // Destroy the powertoy and free memory
  virtual void destroy() override {
    delete this;
  }

  // Return the display name of the powertoy, this will be cached by the runner
  virtual const wchar_t* get_name() override {
      return app_name.c_str();
  }

  // Return array of the names of all events that this powertoy listens for, with
  // nullptr as the last element of the array. Nullptr can also be retured for empty
  // list.
  virtual const wchar_t** get_events() override {
    static const wchar_t* events[] = { nullptr };
    // Available events:
    // - ll_keyboard
    // - win_hook_event
    //
    // static const wchar_t* events[] = { ll_keyboard,
    //                                   win_hook_event,
    //                                   nullptr };

    return events;
  }

  // Return JSON with the configuration options.
  virtual bool get_config(wchar_t* buffer, int* buffer_size) override {
    HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

    // Create a Settings object.
    PowerToysSettings::Settings settings(hinstance, get_name());
    settings.set_description(GET_RESOURCE_STRING(IDS_LAUNCHER_SETTINGS_DESC));

    // Show an overview link in the Settings page
    //settings.set_overview_link(L"https://");

    // Show a video link in the Settings page.
    //settings.set_video_link(L"https://");

    // A bool property with a toggle editor.
    /*settings.add_bool_toogle(
      L"bool_toggle_1", // property name.
      L"This is what a BoolToggle property looks like", // description or resource id of the localized string.
      g_settings.bool_prop // property value.
    );*/

    // An integer property with a spinner editor.
    /*settings.add_int_spinner(
      L"int_spinner_1", // property name
      L"This is what a IntSpinner property looks like", // description or resource id of the localized string.
      g_settings.int_prop, // property value.
      0, // min value.
      100, // max value.
      10 // incremental step.
    );*/

    // A string property with a textbox editor.
    /*settings.add_string(
      L"string_text_1", // property name.
      L"This is what a String property looks like", // description or resource id of the localized string.
      g_settings.string_prop // property value.
    );*/

    // A string property with a color picker editor.
    /*settings.add_color_picker(
      L"color_picker_1", // property name.
      L"This is what a ColorPicker property looks like", // description or resource id of the localized string.
      g_settings.color_prop // property value.
    );*/

    // A custom action property. When using this settings type, the "PowertoyModuleIface::call_custom_action()"
    // method should be overriden as well.
    /*settings.add_custom_action(
      L"custom_action_id", // action name.
      L"This is what a CustomAction property looks like", // label above the field.
      L"Call a custom action", // button text.
      L"Press the button to call a custom action." // display values / extended info.
    );*/

    return settings.serialize_to_buffer(buffer, buffer_size);
  }

  // Signal from the Settings editor to call a custom action.
  // This can be used to spawn more complex editors.
  virtual void call_custom_action(const wchar_t* action) override {
    static UINT custom_action_num_calls = 0;
    try {
      // Parse the action values, including name.
      PowerToysSettings::CustomActionObject action_object =
        PowerToysSettings::CustomActionObject::from_json_string(action);

      /*
      if (action_object.get_name() == L"custom_action_id") {
        // Execute your custom action
      }
      */
    }
    catch (std::exception ex) {
      // Improper JSON.
    }
  }

  // Called by the runner to pass the updated settings values as a serialized JSON.
  virtual void set_config(const wchar_t* config) override {
    try {
      // Parse the input JSON string.
      PowerToysSettings::PowerToyValues values =
        PowerToysSettings::PowerToyValues::from_json_string(config);

      // Update a bool property.
      /*
      if (values.is_bool_value(L"bool_toggle_1")) {
        g_settings.bool_prop = values.get_bool_value(L"bool_toggle_1");
      }
      */

      // Update an int property.
      /*
      if (values.is_int_value(L"int_spinner_1")) {
        g_settings.int_prop = values.get_int_value(L"int_spinner_1");
      }
      */

      // Update a string property.
      /*
      if (values.is_string_value(L"string_text_1")) {
        g_settings.string_prop = values.get_string_value(L"string_text_1");
      }
      */

      // Update a color property.
      /*
      if (values.is_string_value(L"color_picker_1")) {
        g_settings.color_prop = values.get_string_value(L"color_picker_1");
      }
      */

      // If you don't need to do any custom processing of the settings, proceed
      // to persists the values calling:
      values.save_to_settings_file();
      // Otherwise call a custom function to process the settings before saving them to disk:
      // save_settings();
    }
    catch (std::exception ex) {
      // Improper JSON.
    }
  }

   // Enable the powertoy
  virtual void enable()
  {
      SHELLEXECUTEINFO sei{ sizeof(sei) };
      sei.fMask = { SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI };
      sei.lpFile = L"modules\\launcher\\Wox.exe";
      sei.nShow = SW_SHOWNORMAL;
      ShellExecuteEx(&sei);

      m_hProcess = sei.hProcess;

      m_enabled = true;
  }

  // Disable the powertoy
  virtual void disable()
  {
      if (m_enabled)
      {
          TerminateProcess(m_hProcess, 1);
      }

      m_enabled = false;
  }

  // Returns if the powertoys is enabled
  virtual bool is_enabled() override {
    return m_enabled;
  }

  // Handle incoming event, data is event-specific
  virtual intptr_t signal_event(const wchar_t* name, intptr_t data)  override {
    if (wcscmp(name, ll_keyboard) == 0) {
      auto& event = *(reinterpret_cast<LowlevelKeyboardEvent*>(data));
      // Return 1 if the keypress is to be suppressed (not forwarded to Windows),
      // otherwise return 0.
      return 0;
    }
    else if (wcscmp(name, win_hook_event) == 0) {
      auto& event = *(reinterpret_cast<WinHookEvent*>(data));
      // Return value is ignored
      return 0;
    }
    return 0;
  }

  /* Register helper class to handle system menu items related actions. */
  virtual void register_system_menu_helper(PowertoySystemMenuIface* helper) {}
  /* Handle action on system menu item. */
  virtual void signal_system_menu_action(const wchar_t* name) {}
};

// Load the settings file.
void Wox_Launcher::init_settings() {
  try {
    // Load and parse the settings file for this PowerToy.
    PowerToysSettings::PowerToyValues settings =
      PowerToysSettings::PowerToyValues::load_from_settings_file(get_name());

    // Load a bool property.
    /*
    if (settings.is_bool_value(L"bool_toggle_1")) {
      g_settings.bool_prop = settings.get_bool_value(L"bool_toggle_1");
    }
    */

    // Load an int property.
    /*
    if (settings.is_int_value(L"int_spinner_1")) {
      g_settings.int_prop = settings.get_int_value(L"int_spinner_1");
    }
    */

    // Load a string property.
    /*
    if (settings.is_string_value(L"string_text_1")) {
      g_settings.string_prop = settings.get_string_value(L"string_text_1");
    }
    */

    // Load a color property.
    /*
    if (settings.is_string_value(L"color_picker_1")) {
      g_settings.color_prop = settings.get_string_value(L"color_picker_1");
    }
    */
  }
  catch (std::exception ex) {
    // Error while loading from the settings file. Let default values stay as they are.
  }
}

// This method of saving the module settings is only required if you need to do any
// custom processing of the settings before saving them to disk.
/*
void Wox.Launcher::save_settings() {
  try {
    // Create a PowerToyValues object for this PowerToy
    PowerToysSettings::PowerToyValues values(get_name());

    // Save a bool property.
    //values.add_property(
    //  L"bool_toggle_1", // property name
    //  g_settings.bool_prop // property value
    //);

    // Save an int property.
    //values.add_property(
    //  L"int_spinner_1", // property name
    //  g_settings.int_prop // property value
    //);

    // Save a string property.
    //values.add_property(
    //  L"string_text_1", // property name
    //  g_settings.string_prop // property value
    );

    // Save a color property.
    //values.add_property(
    //  L"color_picker_1", // property name
    //  g_settings.color_prop // property value
    //);

    // Save the PowerToyValues JSON to the power toy settings file.
    values.save_to_settings_file();
  }
  catch (std::exception ex) {
    // Couldn't save the settings.
  }
}
*/

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create() {
  return new Wox_Launcher();
}
