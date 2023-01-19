#include "pch.h"
#include "settings_objects.h"
#include "settings_helpers.h"

namespace PowerToysSettings
{
    Settings::Settings(const HINSTANCE hinstance, std::wstring_view powertoy_name)
    {
        m_instance = hinstance;
        m_json.SetNamedValue(L"version", json::value(L"1.0"));
        m_json.SetNamedValue(L"name", json::value(powertoy_name));
        m_json.SetNamedValue(L"properties", json::JsonObject{});
    }

    void Settings::set_description(UINT resource_id)
    {
        m_json.SetNamedValue(L"description", json::value(get_resource(resource_id)));
    }

    void Settings::set_description(std::wstring_view description)
    {
        m_json.SetNamedValue(L"description", json::value(description));
    }

    void Settings::set_icon_key(std::wstring_view icon_key)
    {
        m_json.SetNamedValue(L"icon_key", json::value(icon_key));
    }

    void Settings::set_overview_link(std::wstring_view overview_link)
    {
        m_json.SetNamedValue(L"overview_link", json::value(overview_link));
    }

    void Settings::set_video_link(std::wstring_view video_link)
    {
        m_json.SetNamedValue(L"video_link", json::value(video_link));
    }

    // add_bool_toggle overloads.
    void Settings::add_bool_toggle(std::wstring_view name, UINT description_resource_id, bool value)
    {
        add_bool_toggle(name, get_resource(description_resource_id), value);
    }

    void Settings::add_bool_toggle(std::wstring_view name, std::wstring_view description, bool value)
    {
        json::JsonObject toggle;
        toggle.SetNamedValue(L"display_name", json::value(description));
        toggle.SetNamedValue(L"editor_type", json::value(L"bool_toggle"));
        toggle.SetNamedValue(L"value", json::value(value));
        toggle.SetNamedValue(L"order", json::value(++m_curr_priority));

        m_json.GetNamedObject(L"properties").SetNamedValue(name, toggle);
    }

    // add_int_spinner overloads.
    void Settings::add_int_spinner(std::wstring_view name, UINT description_resource_id, int value, int min, int max, int step)
    {
        add_int_spinner(name, get_resource(description_resource_id), value, min, max, step);
    }

    void Settings::add_int_spinner(std::wstring_view name, std::wstring_view description, int value, int min, int max, int step)
    {
        json::JsonObject spinner;
        spinner.SetNamedValue(L"display_name", json::value(description));
        spinner.SetNamedValue(L"editor_type", json::value(L"int_spinner"));
        spinner.SetNamedValue(L"value", json::value(value));
        spinner.SetNamedValue(L"min", json::value(min));
        spinner.SetNamedValue(L"max", json::value(max));
        spinner.SetNamedValue(L"step", json::value(step));
        spinner.SetNamedValue(L"order", json::value(++m_curr_priority));

        m_json.GetNamedObject(L"properties").SetNamedValue(name, spinner);
    }

    // add_string overloads.
    void Settings::add_string(std::wstring_view name, UINT description_resource_id, std::wstring_view value)
    {
        add_string(name, get_resource(description_resource_id), value);
    }

    void Settings::add_string(std::wstring_view name, std::wstring_view description, std::wstring_view value)
    {
        json::JsonObject string;
        string.SetNamedValue(L"display_name", json::value(description));
        string.SetNamedValue(L"editor_type", json::value(L"string_text"));
        string.SetNamedValue(L"value", json::value(value));
        string.SetNamedValue(L"order", json::value(++m_curr_priority));

        m_json.GetNamedObject(L"properties").SetNamedValue(name, string);
    }

    // add_multiline_string overloads.
    void Settings::add_multiline_string(std::wstring_view name, UINT description_resource_id, std::wstring_view value)
    {
        add_multiline_string(name, get_resource(description_resource_id), value);
    }

    void Settings::add_multiline_string(std::wstring_view name, std::wstring_view description, std::wstring_view value)
    {
        json::JsonObject ml_string;
        ml_string.SetNamedValue(L"display_name", json::value(description));
        ml_string.SetNamedValue(L"editor_type", json::value(L"string_text"));
        ml_string.SetNamedValue(L"value", json::value(value));
        ml_string.SetNamedValue(L"order", json::value(++m_curr_priority));
        ml_string.SetNamedValue(L"multiline", json::value(true));

        m_json.GetNamedObject(L"properties").SetNamedValue(name, ml_string);
    }

    void Settings::add_header_szLarge(std::wstring_view name, std::wstring_view description, std::wstring_view value)
    {
        json::JsonObject string;
        string.SetNamedValue(L"display_name", json::value(description));
        string.SetNamedValue(L"editor_type", json::value(L"header_large"));
        string.SetNamedValue(L"value", json::value(value));
        string.SetNamedValue(L"order", json::value(++m_curr_priority));

        m_json.GetNamedObject(L"properties").SetNamedValue(name, string);
    }

    // add_color_picker overloads.
    void Settings::add_color_picker(std::wstring_view name, UINT description_resource_id, std::wstring_view value)
    {
        add_color_picker(name, get_resource(description_resource_id), value);
    }

    void Settings::add_color_picker(std::wstring_view name, std::wstring_view description, std::wstring_view value)
    {
        json::JsonObject picker;
        picker.SetNamedValue(L"display_name", json::value(description));
        picker.SetNamedValue(L"editor_type", json::value(L"color_picker"));
        picker.SetNamedValue(L"value", json::value(value));
        picker.SetNamedValue(L"order", json::value(++m_curr_priority));

        m_json.GetNamedObject(L"properties").SetNamedValue(name, picker);
    }

    void Settings::add_hotkey(std::wstring_view name, UINT description_resource_id, const HotkeyObject& hotkey)
    {
        add_hotkey(name, get_resource(description_resource_id), hotkey);
    }

    void Settings::add_hotkey(std::wstring_view name, std::wstring_view description, const HotkeyObject& hotkey_obj)
    {
        json::JsonObject hotkey;
        hotkey.SetNamedValue(L"display_name", json::value(description));
        hotkey.SetNamedValue(L"editor_type", json::value(L"hotkey"));
        hotkey.SetNamedValue(L"value", hotkey_obj.get_json());
        hotkey.SetNamedValue(L"order", json::value(++m_curr_priority));

        m_json.GetNamedObject(L"properties").SetNamedValue(name, hotkey);
    }

    void Settings::add_choice_group(std::wstring_view name, UINT description_resource_id, std::wstring_view value, const std::vector<std::pair<std::wstring, UINT>>& keys_and_text_ids)
    {
        std::vector<std::pair<std::wstring, std::wstring>> keys_and_texts;
        keys_and_texts.reserve(keys_and_text_ids.size());
        for (const auto& kv : keys_and_text_ids)
        {
            keys_and_texts.emplace_back(kv.first, get_resource(kv.second));
        }
        add_choice_group(name, get_resource(description_resource_id), value, keys_and_texts);
    }

    void Settings::add_choice_group(std::wstring_view name, std::wstring_view description, std::wstring_view value, const std::vector<std::pair<std::wstring, std::wstring>>& keys_and_texts)
    {
        json::JsonObject choice_group;
        choice_group.SetNamedValue(L"display_name", json::value(description));
        choice_group.SetNamedValue(L"editor_type", json::value(L"choice_group"));
        json::JsonArray options;
        for (const auto& [key, text] : keys_and_texts)
        {
            json::JsonObject entry;
            entry.SetNamedValue(L"key", json::value(key));
            entry.SetNamedValue(L"text", json::value(text));
            options.Append(std::move(entry));
        }
        choice_group.SetNamedValue(L"options", std::move(options));
        choice_group.SetNamedValue(L"value", json::value(value));
        choice_group.SetNamedValue(L"order", json::value(++m_curr_priority));

        m_json.GetNamedObject(L"properties").SetNamedValue(name, choice_group);
    }

    void Settings::add_dropdown(std::wstring_view name, UINT description_resource_id, std::wstring_view value, const std::vector<std::pair<std::wstring, UINT>>& keys_and_text_ids)
    {
        std::vector<std::pair<std::wstring, std::wstring>> keys_and_texts;
        keys_and_texts.reserve(keys_and_text_ids.size());
        for (const auto& kv : keys_and_text_ids)
        {
            keys_and_texts.emplace_back(kv.first, get_resource(kv.second));
        }
        add_dropdown(name, get_resource(description_resource_id), value, keys_and_texts);
    }

    void Settings::add_dropdown(std::wstring_view name, std::wstring_view description, std::wstring_view value, const std::vector<std::pair<std::wstring, std::wstring>>& keys_and_texts)
    {
        json::JsonObject dropdown;
        dropdown.SetNamedValue(L"display_name", json::value(description));
        dropdown.SetNamedValue(L"editor_type", json::value(L"dropdown"));
        json::JsonArray options;
        for (const auto& [key, text] : keys_and_texts)
        {
            json::JsonObject entry;
            entry.SetNamedValue(L"key", json::value(key));
            entry.SetNamedValue(L"text", json::value(text));
            options.Append(std::move(entry));
        }
        dropdown.SetNamedValue(L"options", std::move(options));
        dropdown.SetNamedValue(L"value", json::value(value));
        dropdown.SetNamedValue(L"order", json::value(++m_curr_priority));

        m_json.GetNamedObject(L"properties").SetNamedValue(name, dropdown);
    }

    // add_custom_action overloads.
    void Settings::add_custom_action(std::wstring_view name, UINT description_resource_id, UINT button_text_resource_id, UINT ext_description_resource_id)
    {
        add_custom_action(name, get_resource(description_resource_id), get_resource(button_text_resource_id), get_resource(ext_description_resource_id));
    }

    void Settings::add_custom_action(std::wstring_view name, UINT description_resource_id, UINT button_text_resource_id, std::wstring_view value)
    {
        add_custom_action(name, get_resource(description_resource_id), get_resource(button_text_resource_id), value);
    }

    void Settings::add_custom_action(std::wstring_view name, std::wstring_view description, std::wstring_view button_text, std::wstring_view value)
    {
        json::JsonObject custom_action;
        custom_action.SetNamedValue(L"display_name", json::value(description));
        custom_action.SetNamedValue(L"button_text", json::value(button_text));
        custom_action.SetNamedValue(L"editor_type", json::value(L"custom_action"));
        custom_action.SetNamedValue(L"value", json::value(value));
        custom_action.SetNamedValue(L"order", json::value(++m_curr_priority));

        m_json.GetNamedObject(L"properties").SetNamedValue(name, custom_action);
    }

    // Serialization methods.
    std::wstring Settings::serialize()
    {
        return m_json.Stringify().c_str();
    }

    bool Settings::serialize_to_buffer(wchar_t* buffer, int* buffer_size)
    {
        auto result = m_json.Stringify();
        const int result_len = static_cast<int>(result.size() + 1);

        if (buffer == nullptr || *buffer_size < result_len)
        {
            *buffer_size = result_len;
            return false;
        }
        else
        {
            wcscpy_s(buffer, *buffer_size, result.c_str());
            return true;
        }
    }

    // Resource helper.
    std::wstring Settings::get_resource(UINT resource_id)
    {
        if (resource_id != 0)
        {
            wchar_t* res_ptr;
            const size_t resource_length = LoadStringW(m_instance, resource_id, reinterpret_cast<wchar_t*>(&res_ptr), 0);
            if (resource_length != 0)
            {
                return { *reinterpret_cast<wchar_t**>(&res_ptr), resource_length };
            }
        }

        return L"RESOURCE ID NOT FOUND: " + std::to_wstring(resource_id);
    }

    PowerToyValues::PowerToyValues(std::wstring_view powertoy_name, std::wstring_view powertoy_key)
    {
        _key = powertoy_key;
        set_version();
        m_json.SetNamedValue(L"name", json::value(powertoy_name));
        m_json.SetNamedValue(L"properties", json::JsonObject{});
    }

    PowerToyValues PowerToyValues::from_json_string(std::wstring_view json, std::wstring_view powertoy_key)
    {
        PowerToyValues result = PowerToyValues();
        json::JsonObject jsonObject = json::JsonValue::Parse(json).GetObjectW();
        if (!jsonObject.HasKey(L"name"))
        {
            throw winrt::hresult_error(E_NOT_SET, L"name field not set");
        }

        result.m_json = json::JsonValue::Parse(json).GetObjectW();
        result._key = powertoy_key;
        return result;
    }

    PowerToyValues PowerToyValues::load_from_settings_file(std::wstring_view powertoy_key)
    {
        PowerToyValues result = PowerToyValues();
        result.m_json = PTSettingsHelper::load_module_settings(powertoy_key);
        result._key = powertoy_key;
        return result;
    }

    inline bool has_property(const json::JsonObject& o, std::wstring_view name, const json::JsonValueType type)
    {
        const json::JsonObject props = o.GetNamedObject(L"properties", json::JsonObject{});
        return json::has(props, name) && json::has(props.GetNamedObject(name), L"value", type);
    }

    std::optional<bool> PowerToyValues::get_bool_value(std::wstring_view property_name) const
    {
        if (!has_property(m_json, property_name, json::JsonValueType::Boolean))
        {
            return std::nullopt;
        }
        return m_json.GetNamedObject(L"properties").GetNamedObject(property_name).GetNamedBoolean(L"value");
    }

    std::optional<int> PowerToyValues::get_int_value(std::wstring_view property_name) const
    {
        if (!has_property(m_json, property_name, json::JsonValueType::Number))
        {
            return std::nullopt;
        }
        return static_cast<int>(m_json.GetNamedObject(L"properties").GetNamedObject(property_name).GetNamedNumber(L"value"));
    }

    std::optional<std::wstring> PowerToyValues::get_string_value(std::wstring_view property_name) const
    {
        if (!has_property(m_json, property_name, json::JsonValueType::String))
        {
            return std::nullopt;
        }
        return m_json.GetNamedObject(L"properties").GetNamedObject(property_name).GetNamedString(L"value").c_str();
    }

    std::optional<json::JsonObject> PowerToyValues::get_json(std::wstring_view property_name) const
    {
        if (!has_property(m_json, property_name, json::JsonValueType::Object))
        {
            return std::nullopt;
        }
        return m_json.GetNamedObject(L"properties").GetNamedObject(property_name).GetNamedObject(L"value");
    }

    json::JsonObject PowerToyValues::get_raw_json()
    {
        return m_json;
    }

    std::wstring PowerToyValues::serialize()
    {
        set_version();
        return m_json.Stringify().c_str();
    }

    void PowerToyValues::save_to_settings_file()
    {
        set_version();
        PTSettingsHelper::save_module_settings(_key, m_json);
    }

    void PowerToyValues::set_version()
    {
        m_json.SetNamedValue(L"version", json::value(m_version));
    }
}