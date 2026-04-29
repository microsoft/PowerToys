#include "pch.h"
#include "settings_objects.h"
#include "settings_helpers.h"

namespace PowerToysSettings
{
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

    std::optional<unsigned int> PowerToyValues::get_uint_value(std::wstring_view property_name) const
    {
        if (!has_property(m_json, property_name, json::JsonValueType::Number))
        {
            return std::nullopt;
        }
        return static_cast<unsigned int>(m_json.GetNamedObject(L"properties").GetNamedObject(property_name).GetNamedNumber(L"value"));
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