#include "pch.h"
#include <settings_objects.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace PowerToysSettings;

namespace UnitTestsCommonLib
{
    void compareJsons(const json::JsonObject& expected, const json::JsonObject& actual, bool recursive = true)
    {
        auto iter = expected.First();
        while (iter.HasCurrent())
        {
            const auto key = iter.Current().Key();
            Assert::IsTrue(actual.HasKey(key));

            const std::wstring expectedStringified = iter.Current().Value().Stringify().c_str();
            const std::wstring actualStringified = actual.GetNamedValue(key).Stringify().c_str();

            if (recursive)
            {
                json::JsonObject expectedJson;
                if (json::JsonObject::TryParse(expectedStringified, expectedJson))
                {
                    json::JsonObject actualJson;
                    if (json::JsonObject::TryParse(actualStringified, actualJson))
                    {
                        compareJsons(expectedJson, actualJson, true);
                    }
                    else
                    {
                        Assert::IsTrue(false);
                    }
                }
                else
                {
                    Assert::AreEqual(expectedStringified, actualStringified);
                }
            }
            else
            {
                Assert::AreEqual(expectedStringified, actualStringified);
            }

            iter.MoveNext();
        }
    }

    TEST_CLASS(PowerToyValuesUnitTests)
    {
    private:
        const std::wstring m_json = L"{\"name\":\"Module Name\",\"properties\" : {\"bool_toggle_true\":{\"value\":true},\"bool_toggle_false\":{\"value\":false},\"color_picker\" : {\"value\":\"#ff8d12\"},\"int_spinner\" : {\"value\":10},\"string_text\" : {\"value\":\"a quick fox\"}},\"version\" : \"1.0\" }";
        const std::wstring m_moduleName = L"Module Name";

    public:
        TEST_METHOD(LoadFromJsonBoolTrue)
        {
            PowerToyValues values = PowerToyValues::from_json_string(m_json);
            auto value = values.get_bool_value(L"bool_toggle_true");
            Assert::IsTrue(value.has_value());
            Assert::AreEqual(true, *value);
        }

        TEST_METHOD(LoadFromJsonBoolFalse)
        {
            PowerToyValues values = PowerToyValues::from_json_string(m_json);
            auto value = values.get_bool_value(L"bool_toggle_false");
            Assert::IsTrue(value.has_value());
            Assert::AreEqual(false, *value);
        }

        TEST_METHOD(LoadFromJsonInt)
        {
            PowerToyValues values = PowerToyValues::from_json_string(m_json);
            auto value = values.get_int_value(L"int_spinner");
            Assert::IsTrue(value.has_value());
            Assert::AreEqual(10, *value);
        }

        TEST_METHOD(LoadFromJsonString)
        {
            PowerToyValues values = PowerToyValues::from_json_string(m_json);
            auto value = values.get_string_value(L"string_text");

            Assert::IsTrue(value.has_value());
            std::wstring expected = L"a quick fox";
            Assert::AreEqual(expected, *value);
        }

        TEST_METHOD(LoadFromJsonColorPicker)
        {
            PowerToyValues values = PowerToyValues::from_json_string(m_json);
            auto value = values.get_string_value(L"color_picker");

            Assert::IsTrue(value.has_value());
            std::wstring expected = L"#ff8d12";
            Assert::AreEqual(expected, *value);
        }

        TEST_METHOD(LoadFromEmptyString)
        {
            auto func = [] { PowerToyValues values = PowerToyValues::from_json_string(L""); };
            Assert::ExpectException<winrt::hresult_error>(func);
        }

        TEST_METHOD(LoadFromInvalidString_NameMissed)
        {
            auto func = [] { PowerToyValues values = PowerToyValues::from_json_string(L"{\"properties\" : {\"bool_toggle_true\":{\"value\":true},\"bool_toggle_false\":{\"value\":false},\"color_picker\" : {\"value\":\"#ff8d12\"},\"int_spinner\" : {\"value\":10},\"string_text\" : {\"value\":\"a quick fox\"}},\"version\" : \"1.0\" }"); };
            Assert::ExpectException<winrt::hresult_error>(func);
        }

        TEST_METHOD(LoadFromInvalidString_VersionMissed)
        {
            PowerToyValues values = PowerToyValues::from_json_string(L"{\"name\":\"Module Name\",\"properties\" : {}}");
            const std::wstring expectedStr = L"{\"name\" : \"Module Name\", \"properties\" : {},\"version\" : \"1.0\"}";
            const auto expected = json::JsonObject::Parse(expectedStr);
            const auto actual = json::JsonObject::Parse(values.serialize());

            compareJsons(expected, actual);
        }

        TEST_METHOD(LoadFromInvalidString_PropertiesMissed)
        {
            PowerToyValues values = PowerToyValues::from_json_string(L"{\"name\":\"Module Name\",\"version\" : \"1.0\" }");
            const std::wstring expectedStr = L"{\"name\":\"Module Name\",\"properties\" : {},\"version\" : \"1.0\" }";
            const auto expected = json::JsonObject::Parse(expectedStr);
            const auto actual = json::JsonObject::Parse(values.serialize());

            compareJsons(expected, actual);
        }

        TEST_METHOD(LoadFromValidString_EmptyProperties)
        {
            PowerToyValues values = PowerToyValues::from_json_string(L"{\"name\":\"Module Name\",\"properties\" : {}, \"version\" : \"1.0\" }");
            const std::wstring expectedStr = L"{\"name\":\"Module Name\",\"properties\" : {},\"version\" : \"1.0\" }";
            const auto expected = json::JsonObject::Parse(expectedStr);
            const auto actual = json::JsonObject::Parse(values.serialize());

            compareJsons(expected, actual);
        }

        TEST_METHOD(LoadFromValidString_ChangedVersion)
        {
            PowerToyValues values = PowerToyValues::from_json_string(L"{\"name\":\"Module Name\",\"properties\" : {},\"version\" : \"2.0\"}");
            const std::wstring expectedStr = L"{\"name\" : \"Module Name\", \"properties\" : {},\"version\" : \"2.0\"}";

            const auto expected = json::JsonObject::Parse(expectedStr);
            const auto actual = json::JsonObject::Parse(values.serialize());

            compareJsons(expected, actual);
        }

        TEST_METHOD(CreateWithName)
        {
            PowerToyValues values(m_moduleName);
            const std::wstring expectedStr = L"{\"name\":\"Module Name\",\"properties\" : {},\"version\" : \"1.0\" }";

            const auto expected = json::JsonObject::Parse(expectedStr);
            const auto actual = json::JsonObject::Parse(values.serialize());

            compareJsons(expected, actual);
        }

        TEST_METHOD(AddPropertyBoolPositive)
        {
            PowerToyValues values(m_moduleName);
            values.add_property<bool>(L"positive_bool_value", true);

            auto value = values.get_bool_value(L"positive_bool_value");
            Assert::IsTrue(value.has_value());
            Assert::AreEqual(true, *value);
        }

        TEST_METHOD(AddPropertyBoolNegative)
        {
            PowerToyValues values(m_moduleName);
            values.add_property<bool>(L"negative_bool_value", false);

            auto value = values.get_bool_value(L"negative_bool_value");
            Assert::IsTrue(value.has_value());
            Assert::AreEqual(false, *value);
        }

        TEST_METHOD(AddPropertyIntPositive)
        {
            PowerToyValues values(m_moduleName);
            const int intVal = 4392854;
            values.add_property<int>(L"integer", intVal);

            auto value = values.get_int_value(L"integer");
            Assert::IsTrue(value.has_value());
            Assert::AreEqual(intVal, *value);
        }

        TEST_METHOD(AddPropertyIntNegative)
        {
            PowerToyValues values(m_moduleName);
            const int intVal = -4392854;
            values.add_property<int>(L"integer", intVal);

            auto value = values.get_int_value(L"integer");
            Assert::IsTrue(value.has_value());
            Assert::AreEqual(intVal, *value);
        }

        TEST_METHOD(AddPropertyIntZero)
        {
            PowerToyValues values(m_moduleName);
            const int intVal = 0;
            values.add_property<int>(L"integer", intVal);

            auto value = values.get_int_value(L"integer");
            Assert::IsTrue(value.has_value());
            Assert::AreEqual(intVal, *value);
        }

        TEST_METHOD(AddPropertyStringEmpty)
        {
            PowerToyValues values(m_moduleName);
            const std::wstring stringVal = L"";
            values.add_property<std::wstring>(L"stringval", stringVal);

            auto value = values.get_string_value(L"stringval");
            Assert::IsTrue(value.has_value());
            Assert::AreEqual(stringVal, *value);
        }

        TEST_METHOD(AddPropertyString)
        {
            PowerToyValues values(m_moduleName);
            const std::wstring stringVal = L"Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.";
            values.add_property<std::wstring>(L"stringval", stringVal);

            auto value = values.get_string_value(L"stringval");
            Assert::IsTrue(value.has_value());
            Assert::AreEqual(stringVal, *value);
        }

        TEST_METHOD(AddPropertyJsonEmpty)
        {
            PowerToyValues values(m_moduleName);
            const auto json = json::JsonObject();
            values.add_property<json::JsonObject>(L"jsonval", json);

            auto value = values.get_json(L"jsonval");
            Assert::IsTrue(value.has_value());
            compareJsons(json, *value);
        }

        TEST_METHOD(AddPropertyJsonObject)
        {
            PowerToyValues values(m_moduleName);
            const auto json = json::JsonObject::Parse(m_json);
            values.add_property<json::JsonObject>(L"jsonval", json);

            auto value = values.get_json(L"jsonval");
            Assert::IsTrue(value.has_value());
            compareJsons(json, *value);
        }
    };

    TEST_CLASS(SettingsUnitTests)
    {
    private:
        const std::wstring m_moduleName = L"Module Name";
        const std::wstring m_defaultSettingsName = L"Default setting name";
        const std::wstring m_defaultSettingsDescription = L"Default setting description";
        const json::JsonObject m_defaultSettingsJson = json::JsonObject::Parse(L"{\"name\" : \"Module Name\", \"properties\" : {},\"version\" : \"1.0\"}");

        json::JsonObject createSettingsProperties(const std::wstring& editorType)
        {
            json::JsonObject properties = json::JsonObject();
            properties.SetNamedValue(L"display_name", json::JsonValue::CreateStringValue(m_defaultSettingsDescription));
            properties.SetNamedValue(L"editor_type", json::JsonValue::CreateStringValue(editorType));
            properties.SetNamedValue(L"order", json::JsonValue::CreateNumberValue(1));
            return properties;
        }

    public:
        TEST_METHOD(SettingsSerialization)
        {
            Settings settings(nullptr, m_moduleName);

            const auto expected = m_defaultSettingsJson;
            const auto actual = json::JsonObject::Parse(settings.serialize());
            compareJsons(expected, actual);
        }

        TEST_METHOD(SettingsSerializationToBuffer)
        {
            Settings settings(nullptr, m_moduleName);

            const auto expected = m_defaultSettingsJson;
            int expectedSize = expected.Stringify().size() + 1;

            int actualSize = expectedSize;
            wchar_t* buffer = new wchar_t[expectedSize];
            bool serizalizationSuccess = settings.serialize_to_buffer(buffer, &actualSize);

            Assert::IsTrue(serizalizationSuccess);
            Assert::AreEqual(expectedSize, actualSize);

            auto actualJson = json::JsonObject::Parse(std::wstring(buffer));

            compareJsons(m_defaultSettingsJson, actualJson);
        }

        TEST_METHOD(SettingsSetDescription)
        {
            const auto value = L"description value";
            Settings settings(nullptr, m_moduleName);
            settings.set_description(value);

            const auto expected = m_defaultSettingsJson;
            expected.SetNamedValue(L"description", json::JsonValue::CreateStringValue(value));
            const auto actual = json::JsonObject::Parse(settings.serialize());

            compareJsons(expected, actual);
        }

        TEST_METHOD(SettingsSetIconKey)
        {
            const auto value = L"icon key";
            Settings settings(nullptr, m_moduleName);
            settings.set_icon_key(value);

            const auto expected = m_defaultSettingsJson;
            expected.SetNamedValue(L"icon_key", json::JsonValue::CreateStringValue(value));
            const auto actual = json::JsonObject::Parse(settings.serialize());

            compareJsons(expected, actual);
        }

        TEST_METHOD(SettingsSetOverviewLink)
        {
            const auto value = L"overview link";
            Settings settings(nullptr, m_moduleName);
            settings.set_overview_link(value);

            const auto expected = m_defaultSettingsJson;
            expected.SetNamedValue(L"overview_link", json::JsonValue::CreateStringValue(value));
            const auto actual = json::JsonObject::Parse(settings.serialize());

            compareJsons(expected, actual);
        }

        TEST_METHOD(SettingsSetVideoLink)
        {
            const auto value = L"video link";
            Settings settings(nullptr, m_moduleName);
            settings.set_video_link(value);

            const auto expected = m_defaultSettingsJson;
            expected.SetNamedValue(L"video_link", json::JsonValue::CreateStringValue(value));
            const auto actual = json::JsonObject::Parse(settings.serialize());

            compareJsons(expected, actual);
        }

        TEST_METHOD(SettingsAddBoolTogglePositive)
        {
            const auto value = true;

            Settings settings(nullptr, m_moduleName);
            settings.add_bool_toogle(m_defaultSettingsName, m_defaultSettingsDescription, value);

            auto expected = m_defaultSettingsJson;
            auto expectedProperties = createSettingsProperties(L"bool_toggle");
            expectedProperties.SetNamedValue(L"value", json::JsonValue::CreateBooleanValue(value));
            expected.GetNamedObject(L"properties").SetNamedValue(m_defaultSettingsName, expectedProperties);

            const auto actual = json::JsonObject::Parse(settings.serialize());

            compareJsons(expected, actual);
        }

        TEST_METHOD(SettingsAddBoolToggleNegative)
        {
            const auto value = false;

            Settings settings(nullptr, m_moduleName);
            settings.add_bool_toogle(m_defaultSettingsName, m_defaultSettingsDescription, value);

            auto expected = m_defaultSettingsJson;
            auto expectedProperties = createSettingsProperties(L"bool_toggle");
            expectedProperties.SetNamedValue(L"value", json::JsonValue::CreateBooleanValue(value));
            expected.GetNamedObject(L"properties").SetNamedValue(m_defaultSettingsName, expectedProperties);

            const auto actual = json::JsonObject::Parse(settings.serialize());

            compareJsons(expected, actual);
        }

        TEST_METHOD(SettingsAddSpinner)
        {
            const int value = 738543;
            const int min = 0;
            const int max = 1000000;
            const int step = 10;

            Settings settings(nullptr, m_moduleName);
            settings.add_int_spinner(m_defaultSettingsName, m_defaultSettingsDescription, value, min, max, step);

            auto expected = m_defaultSettingsJson;
            auto expectedProperties = createSettingsProperties(L"int_spinner");
            expectedProperties.SetNamedValue(L"value", json::JsonValue::CreateNumberValue(value));
            expectedProperties.SetNamedValue(L"min", json::JsonValue::CreateNumberValue(min));
            expectedProperties.SetNamedValue(L"max", json::JsonValue::CreateNumberValue(max));
            expectedProperties.SetNamedValue(L"step", json::JsonValue::CreateNumberValue(step));
            expected.GetNamedObject(L"properties").SetNamedValue(m_defaultSettingsName, expectedProperties);

            const auto actual = json::JsonObject::Parse(settings.serialize());

            compareJsons(expected, actual);
        }

        TEST_METHOD(SettingsAddString)
        {
            const auto value = L"string text ";

            Settings settings(nullptr, m_moduleName);
            settings.add_string(m_defaultSettingsName, m_defaultSettingsDescription, value);

            auto expected = m_defaultSettingsJson;
            auto expectedProperties = createSettingsProperties(L"string_text");
            expectedProperties.SetNamedValue(L"value", json::JsonValue::CreateStringValue(value));
            expected.GetNamedObject(L"properties").SetNamedValue(m_defaultSettingsName, expectedProperties);

            const auto actual = json::JsonObject::Parse(settings.serialize());

            compareJsons(expected, actual);
        }

        TEST_METHOD(SettingsAddStringMultiline)
        {
            const auto value = L"Lorem ipsum dolor sit amet,\nconsectetur adipiscing elit,\nsed do eiusmod tempor incididunt ut labore et dolore magna aliqua.\nUt enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.\nDuis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur.\nExcepteur sint occaecat cupidatat non proident,\nsunt in culpa qui officia deserunt mollit anim id est laborum.";

            Settings settings(nullptr, m_moduleName);
            settings.add_multiline_string(m_defaultSettingsName, m_defaultSettingsDescription, value);

            auto expected = m_defaultSettingsJson;
            auto expectedProperties = createSettingsProperties(L"string_text");
            expectedProperties.SetNamedValue(L"value", json::JsonValue::CreateStringValue(value));
            expectedProperties.SetNamedValue(L"multiline", json::JsonValue::CreateBooleanValue(true));
            expected.GetNamedObject(L"properties").SetNamedValue(m_defaultSettingsName, expectedProperties);

            const auto actual = json::JsonObject::Parse(settings.serialize());

            compareJsons(expected, actual);
        }

        TEST_METHOD(SettingsAddColorPicker)
        {
            const auto value = L"#ffffff";

            Settings settings(nullptr, m_moduleName);
            settings.add_color_picker(m_defaultSettingsName, m_defaultSettingsDescription, value);

            auto expected = m_defaultSettingsJson;
            auto expectedProperties = createSettingsProperties(L"color_picker");
            expectedProperties.SetNamedValue(L"value", json::JsonValue::CreateStringValue(value));
            expected.GetNamedObject(L"properties").SetNamedValue(m_defaultSettingsName, expectedProperties);

            const auto actual = json::JsonObject::Parse(settings.serialize());

            compareJsons(expected, actual);
        }

        TEST_METHOD(SettingsAddHotkey)
        {
            const auto value = PowerToysSettings::HotkeyObject::from_settings(true, true, true, true, 0);

            Settings settings(nullptr, m_moduleName);
            settings.add_hotkey(m_defaultSettingsName, m_defaultSettingsDescription, value);

            auto expected = m_defaultSettingsJson;
            auto expectedProperties = createSettingsProperties(L"hotkey");
            expectedProperties.SetNamedValue(L"value", value.get_json());
            expected.GetNamedObject(L"properties").SetNamedValue(m_defaultSettingsName, expectedProperties);

            const auto actual = json::JsonObject::Parse(settings.serialize());

            compareJsons(expected, actual);
        }

        TEST_METHOD(SettingsAddChoiceGroup)
        {
            const auto value = L"choice group value";
            const auto keysAndTexts = {
                std::make_pair<std::wstring, std::wstring>(L"key1", L"value1"),
                std::make_pair<std::wstring, std::wstring>(L"key2", L"value2"),
                std::make_pair<std::wstring, std::wstring>(L"key3", L"value3")
            };

            Settings settings(nullptr, m_moduleName);
            settings.add_choice_group(m_defaultSettingsName, m_defaultSettingsDescription, value, keysAndTexts);

            auto expected = m_defaultSettingsJson;
            auto expectedProperties = createSettingsProperties(L"choice_group");
            expectedProperties.SetNamedValue(L"value", json::JsonValue::CreateStringValue(value));
            json::JsonArray options;
            for (const auto& [key, text] : keysAndTexts)
            {
                json::JsonObject entry;
                entry.SetNamedValue(L"key", json::value(key));
                entry.SetNamedValue(L"text", json::value(text));
                options.Append(std::move(entry));
            }
            expectedProperties.SetNamedValue(L"options", std::move(options));
            expected.GetNamedObject(L"properties").SetNamedValue(m_defaultSettingsName, expectedProperties);

            const auto actual = json::JsonObject::Parse(settings.serialize());

            compareJsons(expected, actual);
        }

        TEST_METHOD(SettingsAddChoiceGroupEmpty)
        {
            const auto value = L"choice group value";

            Settings settings(nullptr, m_moduleName);
            settings.add_choice_group(m_defaultSettingsName, m_defaultSettingsDescription, value, {});

            auto expected = m_defaultSettingsJson;
            auto expectedProperties = createSettingsProperties(L"choice_group");
            expectedProperties.SetNamedValue(L"value", json::JsonValue::CreateStringValue(value));
            expectedProperties.SetNamedValue(L"options", json::JsonArray());
            expected.GetNamedObject(L"properties").SetNamedValue(m_defaultSettingsName, expectedProperties);

            const auto actual = json::JsonObject::Parse(settings.serialize());

            compareJsons(expected, actual);
        }

        TEST_METHOD(SettingsAddDropdown)
        {
            const auto value = L"dropdown value";
            const auto keysAndTexts = {
                std::make_pair<std::wstring, std::wstring>(L"key1", L"value1"),
                std::make_pair<std::wstring, std::wstring>(L"key2", L"value2"),
                std::make_pair<std::wstring, std::wstring>(L"key3", L"value3")
            };

            Settings settings(nullptr, m_moduleName);
            settings.add_dropdown(m_defaultSettingsName, m_defaultSettingsDescription, value, keysAndTexts);

            auto expected = m_defaultSettingsJson;
            auto expectedProperties = createSettingsProperties(L"dropdown");
            expectedProperties.SetNamedValue(L"value", json::JsonValue::CreateStringValue(value));
            json::JsonArray options;
            for (const auto& [key, text] : keysAndTexts)
            {
                json::JsonObject entry;
                entry.SetNamedValue(L"key", json::value(key));
                entry.SetNamedValue(L"text", json::value(text));
                options.Append(std::move(entry));
            }
            expectedProperties.SetNamedValue(L"options", std::move(options));
            expected.GetNamedObject(L"properties").SetNamedValue(m_defaultSettingsName, expectedProperties);

            const auto actual = json::JsonObject::Parse(settings.serialize());

            compareJsons(expected, actual);
        }

        TEST_METHOD(SettingsAddDropdownEmpty)
        {
            const auto value = L"dropdown value";

            Settings settings(nullptr, m_moduleName);
            settings.add_dropdown(m_defaultSettingsName, m_defaultSettingsDescription, value, {});

            auto expected = m_defaultSettingsJson;
            auto expectedProperties = createSettingsProperties(L"dropdown");
            expectedProperties.SetNamedValue(L"value", json::JsonValue::CreateStringValue(value));
            expected.GetNamedObject(L"properties").SetNamedValue(m_defaultSettingsName, expectedProperties);

            const auto actual = json::JsonObject::Parse(settings.serialize());

            compareJsons(expected, actual);
        }

        TEST_METHOD(SettingsAddCustomAction)
        {
            const auto value = L"custom action value";
            const std::wstring buttonText = L"button text";

            Settings settings(nullptr, m_moduleName);
            settings.add_custom_action(m_defaultSettingsName, m_defaultSettingsDescription, buttonText, value);

            auto expected = m_defaultSettingsJson;
            auto expectedProperties = createSettingsProperties(L"custom_action");
            expectedProperties.SetNamedValue(L"value", json::JsonValue::CreateStringValue(value));
            expectedProperties.SetNamedValue(L"button_text", json::JsonValue::CreateStringValue(buttonText));
            expected.GetNamedObject(L"properties").SetNamedValue(m_defaultSettingsName, expectedProperties);

            const auto actual = json::JsonObject::Parse(settings.serialize());

            compareJsons(expected, actual);
        }
    };

    TEST_CLASS(CustomActionObjectUnitTests)
    {
    public:
        TEST_METHOD(CustomActionObjectName)
        {
            const std::wstring json = L"{\"action_name\": \"action name\", \"value\": \"action value\"}";
            CustomActionObject obj = CustomActionObject::from_json_string(json);
            Assert::AreEqual(std::wstring(L"action name"), obj.get_name());
        }

        TEST_METHOD(CustomActionObjectValue)
        {
            const std::wstring json = L"{\"action_name\": \"action name\", \"value\": \"action value\"}";
            CustomActionObject obj = CustomActionObject::from_json_string(json);
            Assert::AreEqual(std::wstring(L"action value"), obj.get_value());
        }
    };
}
