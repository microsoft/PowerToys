#include "pch.h"
#include "settings_objects.h"
#include "settings_helpers.h"

namespace PowerToysSettings {

  Settings::Settings(const HINSTANCE hinstance, const std::wstring& powertoy_name) {
    m_instance = hinstance;
    m_json = web::json::value::object();
    m_json.as_object()[L"version"] = web::json::value::string(L"1.0");
    m_json.as_object()[L"name"] = web::json::value::string(powertoy_name);
    m_json.as_object()[L"properties"] = web::json::value::object();
  }

  void Settings::set_description(UINT resource_id) {
    m_json.as_object()[L"description"] = web::json::value::string(get_resource(resource_id));
  }

  void Settings::set_description(const std::wstring& description) {
    m_json.as_object()[L"description"] = web::json::value::string(description);
  }

  void Settings::set_icon_key(const std::wstring& icon_key) {
    m_json.as_object()[L"icon_key"] = web::json::value::string(icon_key);
  }

  void Settings::set_overview_link(const std::wstring& overview_link) {
    m_json.as_object()[L"overview_link"] = web::json::value::string(overview_link);
  }

  void Settings::set_video_link(const std::wstring& video_link) {
    m_json.as_object()[L"video_link"] = web::json::value::string(video_link);
  }

  // add_bool_toogle overloads.
  void Settings::add_bool_toogle(const std::wstring& name, UINT description_resource_id, bool value) {
    add_bool_toogle(name, get_resource(description_resource_id), value);
  }

  void Settings::add_bool_toogle(const std::wstring& name, const std::wstring& description, bool value) {
    web::json::value item = web::json::value::object();
    item.as_object()[L"display_name"] = web::json::value::string(description);
    item.as_object()[L"editor_type"] = web::json::value::string(L"bool_toggle");
    item.as_object()[L"value"] = web::json::value::boolean(value);
    item.as_object()[L"order"] = web::json::value::number(++m_curr_priority);

    m_json.as_object()[L"properties"].as_object()[name] = item;
  }

  // add_int_spinner overloads.
  void Settings::add_int_spinner(const std::wstring& name, UINT description_resource_id, int value, int min, int max, int step) {
    add_int_spinner(name, get_resource(description_resource_id), value, min, max, step);
  }

  void Settings::add_int_spinner(const std::wstring& name, const std::wstring& description, int value, int min, int max, int step) {
    web::json::value item = web::json::value::object();
    item.as_object()[L"display_name"] = web::json::value::string(description);
    item.as_object()[L"editor_type"] = web::json::value::string(L"int_spinner");
    item.as_object()[L"value"] = web::json::value::number(value);
    item.as_object()[L"min"] = web::json::value::number(min);
    item.as_object()[L"max"] = web::json::value::number(max);
    item.as_object()[L"step"] = web::json::value::number(step);
    item.as_object()[L"order"] = web::json::value::number(++m_curr_priority);

    m_json.as_object()[L"properties"].as_object()[name] = item;
  }

  // add_string overloads.
  void Settings::add_string(const std::wstring& name, UINT description_resource_id, const std::wstring& value) {
    add_string(name, get_resource(description_resource_id), value);
  }

  void Settings::add_string(const std::wstring& name, const std::wstring& description, const std::wstring& value) {
    web::json::value item = web::json::value::object();
    item.as_object()[L"display_name"] = web::json::value::string(description);
    item.as_object()[L"editor_type"] = web::json::value::string(L"string_text");
    item.as_object()[L"value"] = web::json::value::string(value);
    item.as_object()[L"order"] = web::json::value::number(++m_curr_priority);

    m_json.as_object()[L"properties"].as_object()[name] = item;
  }

  // add_color_picker overloads.
  void Settings::add_color_picker(const std::wstring& name, UINT description_resource_id, const std::wstring& value) {
    add_color_picker(name, get_resource(description_resource_id), value);
  }

  void Settings::add_color_picker(const std::wstring& name, const std::wstring& description, const std::wstring& value) {
    web::json::value item = web::json::value::object();
    item.as_object()[L"display_name"] = web::json::value::string(description);
    item.as_object()[L"editor_type"] = web::json::value::string(L"color_picker");
    item.as_object()[L"value"] = web::json::value::string(value);
    item.as_object()[L"order"] = web::json::value::number(++m_curr_priority);

    m_json.as_object()[L"properties"].as_object()[name] = item;
  }

  void Settings::add_hotkey(const std::wstring& name, UINT description_resource_id, const HotkeyObject& hotkey) {
    add_hotkey(name, get_resource(description_resource_id), hotkey);
  }

  void Settings::add_hotkey(const std::wstring& name, const std::wstring& description, const HotkeyObject& hotkey) {
    web::json::value item = web::json::value::object();
    item.as_object()[L"display_name"] = web::json::value::string(description);
    item.as_object()[L"editor_type"] = web::json::value::string(L"hotkey");
    item.as_object()[L"value"] = hotkey.get_json();
    item.as_object()[L"order"] = web::json::value::number(++m_curr_priority);

    m_json.as_object()[L"properties"].as_object()[name] = item;
  }

  // add_custom_action overloads.
  void Settings::add_custom_action(const std::wstring& name, UINT description_resource_id, UINT button_text_resource_id, UINT ext_description_resource_id) {
    add_custom_action(name, get_resource(description_resource_id), get_resource(button_text_resource_id), get_resource(ext_description_resource_id));
  }

  void Settings::add_custom_action(const std::wstring& name, UINT description_resource_id, UINT button_text_resource_id, const std::wstring& value) {
    add_custom_action(name, get_resource(description_resource_id), get_resource(button_text_resource_id), value);
  }

  void Settings::add_custom_action(const std::wstring& name, const std::wstring& description, const std::wstring& button_text, const std::wstring& value) {
    web::json::value item = web::json::value::object();
    item.as_object()[L"display_name"] = web::json::value::string(description);
    item.as_object()[L"button_text"] = web::json::value::string(button_text);
    item.as_object()[L"editor_type"] = web::json::value::string(L"custom_action");
    item.as_object()[L"value"] = web::json::value::string(value);
    item.as_object()[L"order"] = web::json::value::number(++m_curr_priority);

    m_json.as_object()[L"properties"].as_object()[name] = item;
  }

  // Serialization methods.
  std::wstring Settings::serialize() {
    return m_json.serialize();
  }

  bool Settings::serialize_to_buffer(wchar_t* buffer, int *buffer_size) {
    std::wstring result = m_json.serialize();
    int result_len = (int)result.length();

    if (buffer == nullptr || *buffer_size < result_len) {
      *buffer_size = result_len + 1;
      return false;
    } else {
      wcscpy_s(buffer, *buffer_size, result.c_str());
      return true;
    }
  }

  // Resource helper.
  std::wstring Settings::get_resource(UINT resource_id) {
    if (resource_id != 0) {
      wchar_t buffer[512];
      if (LoadString(m_instance, resource_id, buffer, ARRAYSIZE(buffer)) > 0) {
        return std::wstring(buffer);
      }
    }

    return L"RESOURCE ID NOT FOUND: " + std::to_wstring(resource_id);
  }

    PowerToyValues::PowerToyValues(const std::wstring& powertoy_name) {
    _name = powertoy_name;
    m_json = web::json::value::object();
    set_version();
    m_json.as_object()[L"name"] = web::json::value::string(powertoy_name);
    m_json.as_object()[L"properties"] = web::json::value::object();
  }

  PowerToyValues PowerToyValues::from_json_string(const std::wstring& json) {
    PowerToyValues result = PowerToyValues();
    result.m_json = web::json::value::parse(json);
    result._name = result.m_json.as_object()[L"name"].as_string();
    return result;
  }

  PowerToyValues PowerToyValues::load_from_settings_file(const std::wstring & powertoy_name) {
    PowerToyValues result = PowerToyValues();
    result.m_json = PTSettingsHelper::load_module_settings(powertoy_name);
    result._name = powertoy_name;
    return result;
  }

  template <typename T>
  web::json::value add_property_generic(const std::wstring& name, T value) {
    std::vector<std::pair<std::wstring, web::json::value>> vector = { std::make_pair(L"value", web::json::value(value)) };
    return web::json::value::object(vector);
  }

  template <>
  void PowerToyValues::add_property(const std::wstring& name, bool value) {
    m_json.as_object()[L"properties"].as_object()[name] = add_property_generic(name, value);
  };
  
  template <>
  void PowerToyValues::add_property(const std::wstring& name, int value) {
    m_json.as_object()[L"properties"].as_object()[name] = add_property_generic(name, value);
  };

  template <>
  void PowerToyValues::add_property(const std::wstring& name, std::wstring value) {
    m_json.as_object()[L"properties"].as_object()[name] = add_property_generic(name, value);
  };

  template  <>
  void PowerToyValues::add_property(const std::wstring& name, HotkeyObject value) {
    m_json.as_object()[L"properties"].as_object()[name] = add_property_generic(name, value.get_json());
  };

  bool PowerToyValues::is_bool_value(const std::wstring& property_name) {
    return m_json.is_object() &&
      m_json.has_object_field(L"properties") &&
      m_json[L"properties"].has_object_field(property_name) &&
      m_json[L"properties"][property_name].has_boolean_field(L"value");
  }

  bool PowerToyValues::is_int_value(const std::wstring& property_name) {
    return m_json.is_object() &&
      m_json.has_object_field(L"properties") &&
      m_json[L"properties"].has_object_field(property_name) &&
      m_json[L"properties"][property_name].has_integer_field(L"value");
  }

  bool PowerToyValues::is_string_value(const std::wstring& property_name) {
    return m_json.is_object() &&
      m_json.has_object_field(L"properties") &&
      m_json[L"properties"].has_object_field(property_name) &&
      m_json[L"properties"][property_name].has_string_field(L"value");
  }

  bool PowerToyValues::is_object_value(const std::wstring& property_name) {
    return m_json.is_object() &&
      m_json.has_object_field(L"properties") &&
      m_json[L"properties"].has_object_field(property_name) &&
      m_json[L"properties"][property_name].has_object_field(L"value");
  }

  bool PowerToyValues::get_bool_value(const std::wstring& property_name) {
    return m_json[L"properties"][property_name][L"value"].as_bool();
  }

  int PowerToyValues::get_int_value(const std::wstring& property_name) {
    return m_json[L"properties"][property_name][L"value"].as_integer();
  }

  std::wstring PowerToyValues::get_string_value(const std::wstring& property_name) {
    return m_json[L"properties"][property_name][L"value"].as_string();
  }

  web::json::value PowerToyValues::get_json(const std::wstring& property_name) {
    return m_json[L"properties"][property_name][L"value"];
  }

  std::wstring PowerToyValues::serialize() {
    set_version();
    return m_json.serialize();
  }

  void PowerToyValues::save_to_settings_file() {
    set_version();
    PTSettingsHelper::save_module_settings(_name, m_json);
  }

  void PowerToyValues::set_version() {
    m_json.as_object()[L"version"] = web::json::value::string(m_version);
  }
}