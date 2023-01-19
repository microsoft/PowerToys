#pragma once

#include "../utils/json.h"

#include <cwctype>

namespace PowerToysSettings
{
    class HotkeyObject;

    class Settings
    {
    public:
        Settings(
            const HINSTANCE hinstance, // Module handle of the PowerToy DLL 'IMAGE_DOS_HEADER __ImageBase'
            std::wstring_view powertoy_name);

        // Add additional general information to the PowerToy settings.
        void set_description(UINT resource_id);
        void set_description(std::wstring_view description);

        void set_icon_key(std::wstring_view icon_key);
        void set_overview_link(std::wstring_view overview_link);
        void set_video_link(std::wstring_view video_link);

        // Add properties to the PowerToy settings.
        void add_bool_toggle(std::wstring_view name, UINT description_resource_id, bool value);
        void add_bool_toggle(std::wstring_view name, std::wstring_view description, bool value);

        void add_int_spinner(std::wstring_view name, UINT description_resource_id, int value, int min, int max, int step);
        void add_int_spinner(std::wstring_view name, std::wstring_view description, int value, int min, int max, int step);

        void add_string(std::wstring_view name, UINT description_resource_id, std::wstring_view value);
        void add_string(std::wstring_view name, std::wstring_view description, std::wstring_view value);

        void add_multiline_string(std::wstring_view name, UINT description_resource_id, std::wstring_view value);
        void add_multiline_string(std::wstring_view name, std::wstring_view description, std::wstring_view value);

        void add_color_picker(std::wstring_view name, UINT description_resource_id, std::wstring_view value);
        void add_color_picker(std::wstring_view name, std::wstring_view description, std::wstring_view value);

        void add_hotkey(std::wstring_view name, UINT description_resource_id, const HotkeyObject& hotkey);
        void add_hotkey(std::wstring_view name, std::wstring_view description, const HotkeyObject& hotkey);

        void add_choice_group(std::wstring_view name, UINT description_resource_id, std::wstring_view value, const std::vector<std::pair<std::wstring, UINT>>& keys_and_text_ids);
        void add_choice_group(std::wstring_view name, std::wstring_view description, std::wstring_view value, const std::vector<std::pair<std::wstring, std::wstring>>& keys_and_texts);

        void add_dropdown(std::wstring_view name, UINT description_resource_id, std::wstring_view value, const std::vector<std::pair<std::wstring, UINT>>& keys_and_text_ids);
        void add_dropdown(std::wstring_view name, std::wstring_view description, std::wstring_view value, const std::vector<std::pair<std::wstring, std::wstring>>& keys_and_texts);

        void add_custom_action(std::wstring_view name, UINT description_resource_id, UINT button_text_resource_id, UINT ext_description_resource_id);
        void add_custom_action(std::wstring_view name, UINT description_resource_id, UINT button_text_resource_id, std::wstring_view value);
        void add_custom_action(std::wstring_view name, std::wstring_view description, std::wstring_view button_text, std::wstring_view value);

        void add_header_szLarge(std::wstring_view name, std::wstring_view description, std::wstring_view value);
        // Serialize the internal json to a string.
        std::wstring serialize();
        // Serialize the internal json to the input buffer.
        bool serialize_to_buffer(wchar_t* buffer, int* buffer_size);

    private:
        json::JsonObject m_json;
        int m_curr_priority = 0; // For keeping order when adding elements.
        HINSTANCE m_instance;

        std::wstring get_resource(UINT resource_id);
    };

    class PowerToyValues
    {
    public:
        PowerToyValues(std::wstring_view powertoy_name, std::wstring_view powertoy_key);
        static PowerToyValues from_json_string(std::wstring_view json, std::wstring_view powertoy_key);
        static PowerToyValues load_from_settings_file(std::wstring_view powertoy_key);

        template<typename T>
        inline void add_property(std::wstring_view name, T value)
        {
            json::JsonObject prop_value;
            prop_value.SetNamedValue(L"value", json::value(value));
            m_json.GetNamedObject(L"properties").SetNamedValue(name, prop_value);
        }

        std::optional<bool> get_bool_value(std::wstring_view property_name) const;
        std::optional<int> get_int_value(std::wstring_view property_name) const;
        std::optional<std::wstring> get_string_value(std::wstring_view property_name) const;
        std::optional<json::JsonObject> get_json(std::wstring_view property_name) const;
        json::JsonObject get_raw_json();

        std::wstring serialize();
        void save_to_settings_file();

    private:
        const std::wstring m_version = L"1.0";
        void set_version();
        json::JsonObject m_json;
        std::wstring _key;
        PowerToyValues() {}
    };

    class CustomActionObject
    {
    public:
        static CustomActionObject from_json_string(std::wstring_view json)
        {
            return CustomActionObject(json::JsonValue::Parse(json).GetObjectW());
        }

        std::wstring get_name() { return m_json.GetNamedString(L"action_name").c_str(); }
        std::wstring get_value() { return m_json.GetNamedString(L"value").c_str(); }

    protected:
        CustomActionObject(json::JsonObject action_json) :
            m_json(std::move(action_json)){};
        json::JsonObject m_json;
    };

    class HotkeyObject
    {
    public:
        static HotkeyObject from_json(json::JsonObject json)
        {
            return HotkeyObject(std::move(json));
        }
        static HotkeyObject from_json_string(std::wstring_view json)
        {
            return HotkeyObject(json::JsonValue::Parse(json).GetObjectW());
        }
        static HotkeyObject from_settings(bool win_pressed, bool ctrl_pressed, bool alt_pressed, bool shift_pressed, UINT vk_code)
        {
            json::JsonObject json;
            json.SetNamedValue(L"win", json::value(win_pressed));
            json.SetNamedValue(L"ctrl", json::value(ctrl_pressed));
            json.SetNamedValue(L"alt", json::value(alt_pressed));
            json.SetNamedValue(L"shift", json::value(shift_pressed));
            json.SetNamedValue(L"code", json::value(vk_code));
            json.SetNamedValue(L"key", json::value(key_from_code(vk_code)));
            return std::move(json);
        }
        const json::JsonObject& get_json() const { return m_json; }

        std::wstring get_key() const { return m_json.GetNamedString(L"key").c_str(); }
        UINT get_code() const { return static_cast<UINT>(m_json.GetNamedNumber(L"code")); }
        bool win_pressed() const { return m_json.GetNamedBoolean(L"win"); }
        bool ctrl_pressed() const { return m_json.GetNamedBoolean(L"ctrl"); }
        bool alt_pressed() const { return m_json.GetNamedBoolean(L"alt"); }
        bool shift_pressed() const { return m_json.GetNamedBoolean(L"shift"); }
        UINT get_modifiers_repeat() const
        {
            return (win_pressed() ? MOD_WIN : 0) |
                   (ctrl_pressed() ? MOD_CONTROL : 0) |
                   (alt_pressed() ? MOD_ALT : 0) |
                   (shift_pressed() ? MOD_SHIFT : 0);
        }
        UINT get_modifiers() const
        {
            return get_modifiers_repeat() | MOD_NOREPEAT;
        }

        std::wstring to_string()
        {
            std::wstring result = L"";
            if (shift_pressed())
            {
                result += L"shift+";
            }

            if (ctrl_pressed())
            {
                result += L"ctrl+";
            }

            if (win_pressed())
            {
                result += L"win+";
            }

            if (alt_pressed())
            {
                result += L"alt+";
            }

            result += key_from_code(get_code());
            return result;
        }

        static std::wstring key_from_code(UINT key_code)
        {
            auto layout = GetKeyboardLayout(0);
            auto scan_code = MapVirtualKeyExW(key_code, MAPVK_VK_TO_VSC_EX, layout);
            // Determinate if vk is an extended key. Unfortunately MAPVK_VK_TO_VSC_EX
            // does not return correct values.
            static std::vector<UINT> extended_keys = {
                VK_APPS,
                VK_CANCEL,
                VK_SNAPSHOT,
                VK_DIVIDE,
                VK_NUMLOCK,
                VK_LWIN,
                VK_RWIN,
                VK_RMENU,
                VK_RCONTROL,
                VK_RSHIFT,
                VK_RETURN,
                VK_INSERT,
                VK_DELETE,
                VK_PRIOR,
                VK_NEXT,
                VK_HOME,
                VK_END,
                VK_UP,
                VK_DOWN,
                VK_LEFT,
                VK_RIGHT,
            };
            if (find(begin(extended_keys), end(extended_keys), key_code) != end(extended_keys))
            {
                scan_code |= 0x100;
            }
            std::array<BYTE, 256> key_states{}; // Zero-initialize
            std::array<wchar_t, 256> output;
            const UINT wFlags = 1 << 2; // If bit 2 is set, keyboard state is not changed (Windows 10, version 1607 and newer)
            auto output_bytes = ToUnicodeEx(key_code, scan_code, key_states.data(), output.data(), static_cast<int>(output.size()) - 1, wFlags, layout);
            if (output_bytes <= 0)
            {
                // If ToUnicodeEx fails (e.g. for F1-F12 keys) use GetKeyNameTextW
                output_bytes = GetKeyNameTextW(scan_code << 16, output.data(), static_cast<int>(output.size()));
            }
            if (output_bytes > 0)
            {
                output[output_bytes] = 0;
                if (output_bytes == 1 && output[0] >= 'a' && output[0] <= 'z')
                {
                    // Make Latin letters keys capital, as it looks better
                    output[0] = std::towupper(output[0]);
                }
                return output.data();
            }
            return L"(Key " + std::to_wstring(key_code) + L")";
        }

    protected:
        HotkeyObject(json::JsonObject hotkey_json) :
            m_json(std::move(hotkey_json))
        {
            if (get_key() == L"~" && get_modifiers_repeat() == MOD_WIN)
            {
                m_json.SetNamedValue(L"key", json::value(key_from_code(get_code())));
            }
        };
        json::JsonObject m_json;
    };

}
