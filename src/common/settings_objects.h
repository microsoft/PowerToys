#pragma once
#include <string>
#include <cpprest/json.h>

namespace PowerToysSettings {

  class HotkeyObject;

  class Settings {
  public:
    Settings(
      const HINSTANCE hinstance, // Module handle of the PowerToy DLL 'IMAGE_DOS_HEADER __ImageBase'
      const std::wstring& powertoy_name
    );

    // Add additional general information to the PowerToy settings.
    void set_description(UINT resource_id);
    void set_description(const std::wstring& description);

    void set_icon_key(const std::wstring& icon_key);
    void set_overview_link(const std::wstring& overview_link);
    void set_video_link(const std::wstring& video_link);

    // Add properties to the PowerToy settings.
    void add_bool_toogle(const std::wstring& name, UINT description_resource_id, bool value);
    void add_bool_toogle(const std::wstring& name, const std::wstring& description, bool value);

    void add_int_spinner(const std::wstring& name, UINT description_resource_id, int value, int min, int max, int step);
    void add_int_spinner(const std::wstring& name, const std::wstring& description, int value, int min, int max, int step);

    void add_string(const std::wstring& name, UINT description_resource_id, const std::wstring& value);
    void add_string(const std::wstring& name, const std::wstring& description, const std::wstring& value);

    void add_color_picker(const std::wstring& name, UINT description_resource_id, const std::wstring& value);
    void add_color_picker(const std::wstring& name, const std::wstring& description, const std::wstring& value);

    void add_hotkey(const std::wstring& name, UINT description_resource_id, const HotkeyObject& hotkey);
    void add_hotkey(const std::wstring& name, const std::wstring& description, const HotkeyObject& hotkey);

    void add_choice_group(const std::wstring& name, UINT description_resource_id, const std::wstring& value, const std::vector<std::pair<std::wstring, UINT>>& keys_and_text_ids);
    void add_choice_group(const std::wstring& name, const std::wstring& description, const std::wstring& value, const std::vector<std::pair<std::wstring, std::wstring>>& keys_and_texts);

    void add_dropdown(const std::wstring& name, UINT description_resource_id, const std::wstring& value, const std::vector<std::pair<std::wstring, UINT>>& keys_and_text_ids);
    void add_dropdown(const std::wstring& name, const std::wstring& description, const std::wstring& value, const std::vector<std::pair<std::wstring, std::wstring>>& keys_and_texts);

    void add_custom_action(const std::wstring& name, UINT description_resource_id, UINT button_text_resource_id, UINT ext_description_resource_id);
    void add_custom_action(const std::wstring& name, UINT description_resource_id, UINT button_text_resource_id, const std::wstring& value);
    void add_custom_action(const std::wstring& name, const std::wstring& description, const std::wstring& button_text, const std::wstring& value);


    // Serialize the internal json to a string.
    std::wstring serialize();
    // Serialize the internal json to the input buffer.
    bool serialize_to_buffer(wchar_t* buffer, int* buffer_size);

  private:
    web::json::value m_json;
    int m_curr_priority = 0; // For keeping order when adding elements.
    HINSTANCE m_instance;

    std::wstring get_resource(UINT resource_id);
  };

  class PowerToyValues {
  public:
    PowerToyValues(const std::wstring& powertoy_name);
    static PowerToyValues from_json_string(const std::wstring& json);
    static PowerToyValues load_from_settings_file(const std::wstring& powertoy_name);

    template <typename T>
    void add_property(const std::wstring& name, T value);

    // Check property value type
    bool is_bool_value(const std::wstring& property_name);
    bool is_int_value(const std::wstring& property_name);
    bool is_string_value(const std::wstring& property_name);
    bool is_object_value(const std::wstring& property_name);

    // Get property value
    bool get_bool_value(const std::wstring& property_name);
    int get_int_value(const std::wstring& property_name);
    std::wstring get_string_value(const std::wstring& property_name);
    web::json::value get_json(const std::wstring& property_name);

    std::wstring serialize();
    void save_to_settings_file();

  private:
    const std::wstring m_version = L"1.0";
    void set_version();
    web::json::value m_json;
    std::wstring _name;
    PowerToyValues() {}
  };

  class CustomActionObject {
  public:
    static CustomActionObject from_json_string(const std::wstring& json) {
      web::json::value parsed_json = web::json::value::parse(json);
      return CustomActionObject(parsed_json);
    }

    std::wstring get_name() { return m_json[L"action_name"].as_string(); }
    std::wstring get_value() { return m_json[L"value"].as_string(); }

  protected:
    CustomActionObject(web::json::value action_json) : m_json(action_json) {};
    web::json::value m_json;
  };
  
  class HotkeyObject {
  public:
    static HotkeyObject from_json(web::json::value json) {
      return HotkeyObject(json);
    }
    static HotkeyObject from_json_string(const std::wstring& json) {
      web::json::value parsed_json = web::json::value::parse(json);
      return HotkeyObject(parsed_json);
    }
    static HotkeyObject from_settings(bool win_pressed, bool ctrl_pressed, bool alt_pressed, bool shift_pressed, UINT vk_code, const std::wstring& key) {
      web::json::value json = web::json::value::object();
      json.as_object()[L"win"] = web::json::value::boolean(win_pressed);
      json.as_object()[L"ctrl"] = web::json::value::boolean(ctrl_pressed);
      json.as_object()[L"alt"] = web::json::value::boolean(alt_pressed);
      json.as_object()[L"shift"] = web::json::value::boolean(shift_pressed);
      json.as_object()[L"code"] = web::json::value::number(vk_code);
      json.as_object()[L"key"] = web::json::value::string(key);
      return HotkeyObject(json);
    }
    const web::json::value& get_json() const { return m_json; }

    std::wstring get_key() { return m_json[L"key"].as_string(); }
    UINT get_code() { return m_json[L"code"].as_integer(); }
    bool win_pressed() { return m_json[L"win"].as_bool(); }
    bool ctrl_pressed() { return m_json[L"ctrl"].as_bool(); }
    bool alt_pressed() { return m_json[L"alt"].as_bool(); }
    bool shift_pressed() { return m_json[L"shift"].as_bool(); }
    UINT get_modifiers_repeat()  {
      return (win_pressed()   ? MOD_WIN : 0) |
             (ctrl_pressed()  ? MOD_CONTROL : 0) |
             (alt_pressed()   ? MOD_ALT : 0) |
             (shift_pressed() ? MOD_SHIFT : 0);
    }
    UINT get_modifiers() {
      return get_modifiers_repeat() | MOD_NOREPEAT;
    }
    std::wstring to_string() {
      std::wstring result = L"";
      if (win_pressed()) {
        result += L"Win";
      }
      if (ctrl_pressed()) {
        if (!result.empty()) {
          result += L" + ";
        }
        result += L"Ctrl";
      }
      if (alt_pressed()) {
        if (!result.empty()) {
          result += L" + ";
        }
        result += L"Alt";
      }
      if (shift_pressed()) {
        if (!result.empty()) {
          result += L" + ";
        }
        result += L"Shift";
      }
      if (!result.empty()) {
        result += L" + ";
      }
      result += get_key();
      return result;
    }
  protected:
    HotkeyObject(web::json::value hotkey_json) : m_json(hotkey_json) {};
    web::json::value m_json;
  };

}
