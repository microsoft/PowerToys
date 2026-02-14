#include "pch.h"
#include <common/SettingsAPI/settings_objects.h>

#include "version_helper.h"

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

    TEST_CLASS (PowerToyValuesUnitTests)
    {
    private:
        const std::wstring m_json = L"{\"name\":\"Module Name\",\"properties\" : {\"bool_toggle_true\":{\"value\":true},\"bool_toggle_false\":{\"value\":false},\"color_picker\" : {\"value\":\"#ff8d12\"},\"int_spinner\" : {\"value\":10},\"string_text\" : {\"value\":\"a quick fox\"}},\"version\" : \"1.0\" }";
        const std::wstring m_moduleName = L"Module Name";
        const std::wstring m_moduleKey = L"Module Key";

    public:
        TEST_METHOD (LoadFromJsonBoolTrue)
        {
            PowerToyValues values = PowerToyValues::from_json_string(m_json, m_moduleKey);
            auto value = values.get_bool_value(L"bool_toggle_true");
            Assert::IsTrue(value.has_value());
            Assert::AreEqual(true, *value);
        }

        TEST_METHOD (LoadFromJsonBoolFalse)
        {
            PowerToyValues values = PowerToyValues::from_json_string(m_json, m_moduleKey);
            auto value = values.get_bool_value(L"bool_toggle_false");
            Assert::IsTrue(value.has_value());
            Assert::AreEqual(false, *value);
        }

        TEST_METHOD (LoadFromJsonInt)
        {
            PowerToyValues values = PowerToyValues::from_json_string(m_json, m_moduleKey);
            auto value = values.get_int_value(L"int_spinner");
            Assert::IsTrue(value.has_value());
            Assert::AreEqual(10, *value);
        }

        TEST_METHOD (LoadFromJsonString)
        {
            PowerToyValues values = PowerToyValues::from_json_string(m_json, m_moduleKey);
            auto value = values.get_string_value(L"string_text");

            Assert::IsTrue(value.has_value());
            std::wstring expected = L"a quick fox";
            Assert::AreEqual(expected, *value);
        }

        TEST_METHOD (LoadFromJsonColorPicker)
        {
            PowerToyValues values = PowerToyValues::from_json_string(m_json, m_moduleKey);
            auto value = values.get_string_value(L"color_picker");

            Assert::IsTrue(value.has_value());
            std::wstring expected = L"#ff8d12";
            Assert::AreEqual(expected, *value);
        }

        TEST_METHOD (LoadFromEmptyString)
        {
            auto func = [] { PowerToyValues values = PowerToyValues::from_json_string(L"", L"Module Key"); };
            Assert::ExpectException<winrt::hresult_error>(func);
        }

        TEST_METHOD (LoadFromInvalidString_NameMissed)
        {
            auto func = [] { PowerToyValues values = PowerToyValues::from_json_string(L"{\"properties\" : {\"bool_toggle_true\":{\"value\":true},\"bool_toggle_false\":{\"value\":false},\"color_picker\" : {\"value\":\"#ff8d12\"},\"int_spinner\" : {\"value\":10},\"string_text\" : {\"value\":\"a quick fox\"}},\"version\" : \"1.0\" }", L"Module Key"); };
            Assert::ExpectException<winrt::hresult_error>(func);
        }

        TEST_METHOD (LoadFromInvalidString_VersionMissed)
        {
            PowerToyValues values = PowerToyValues::from_json_string(L"{\"name\":\"Module Name\",\"properties\" : {}}", L"Module Key");
            const std::wstring expectedStr = L"{\"name\" : \"Module Name\", \"properties\" : {},\"version\" : \"1.0\"}";
            const auto expected = json::JsonObject::Parse(expectedStr);
            const auto actual = json::JsonObject::Parse(values.serialize());

            compareJsons(expected, actual);
        }

        TEST_METHOD (LoadFromInvalidString_PropertiesMissed)
        {
            PowerToyValues values = PowerToyValues::from_json_string(L"{\"name\":\"Module Name\",\"version\" : \"1.0\" }", L"Module Key");
            const std::wstring expectedStr = L"{\"name\":\"Module Name\",\"version\" : \"1.0\" }";
            const auto expected = json::JsonObject::Parse(expectedStr);
            const auto actual = json::JsonObject::Parse(values.serialize());

            compareJsons(expected, actual);
        }

        TEST_METHOD (LoadFromValidString_EmptyProperties)
        {
            PowerToyValues values = PowerToyValues::from_json_string(L"{\"name\":\"Module Name\",\"properties\" : {}, \"version\" : \"1.0\" }", L"Module Key");
            const std::wstring expectedStr = L"{\"name\":\"Module Name\",\"properties\" : {},\"version\" : \"1.0\" }";
            const auto expected = json::JsonObject::Parse(expectedStr);
            const auto actual = json::JsonObject::Parse(values.serialize());

            compareJsons(expected, actual);
        }

        TEST_METHOD (LoadFromValidString_ChangedVersion)
        {
            PowerToyValues values = PowerToyValues::from_json_string(L"{\"name\":\"Module Name\",\"properties\" : {},\"version\" : \"2.0\"}", L"Module Key");
            const std::wstring expectedStr = L"{\"name\" : \"Module Name\", \"properties\" : {},\"version\" : \"1.0\"}"; //version from input json is ignored

            const auto expected = json::JsonObject::Parse(expectedStr);
            const auto actual = json::JsonObject::Parse(values.serialize());

            compareJsons(expected, actual);
        }

        TEST_METHOD (CreateWithName)
        {
            PowerToyValues values(m_moduleName, m_moduleKey);
            const std::wstring expectedStr = L"{\"name\":\"Module Name\",\"properties\" : {},\"version\" : \"1.0\" }";

            const auto expected = json::JsonObject::Parse(expectedStr);
            const auto actual = json::JsonObject::Parse(values.serialize());

            compareJsons(expected, actual);
        }

        TEST_METHOD (AddPropertyBoolPositive)
        {
            PowerToyValues values(m_moduleName, m_moduleKey);
            values.add_property<bool>(L"positive_bool_value", true);

            auto value = values.get_bool_value(L"positive_bool_value");
            Assert::IsTrue(value.has_value());
            Assert::AreEqual(true, *value);
        }

        TEST_METHOD (AddPropertyBoolNegative)
        {
            PowerToyValues values(m_moduleName, m_moduleKey);
            values.add_property<bool>(L"negative_bool_value", false);

            auto value = values.get_bool_value(L"negative_bool_value");
            Assert::IsTrue(value.has_value());
            Assert::AreEqual(false, *value);
        }

        TEST_METHOD (AddPropertyIntPositive)
        {
            PowerToyValues values(m_moduleName, m_moduleKey);
            const int intVal = 4392854;
            values.add_property<int>(L"integer", intVal);

            auto value = values.get_int_value(L"integer");
            Assert::IsTrue(value.has_value());
            Assert::AreEqual(intVal, *value);
        }

        TEST_METHOD (AddPropertyIntNegative)
        {
            PowerToyValues values(m_moduleName, m_moduleKey);
            const int intVal = -4392854;
            values.add_property<int>(L"integer", intVal);

            auto value = values.get_int_value(L"integer");
            Assert::IsTrue(value.has_value());
            Assert::AreEqual(intVal, *value);
        }

        TEST_METHOD (AddPropertyIntZero)
        {
            PowerToyValues values(m_moduleName, m_moduleKey);
            const int intVal = 0;
            values.add_property<int>(L"integer", intVal);

            auto value = values.get_int_value(L"integer");
            Assert::IsTrue(value.has_value());
            Assert::AreEqual(intVal, *value);
        }

        TEST_METHOD (AddPropertyStringEmpty)
        {
            PowerToyValues values(m_moduleName, m_moduleKey);
            const std::wstring stringVal = L"";
            values.add_property<std::wstring>(L"stringval", stringVal);

            auto value = values.get_string_value(L"stringval");
            Assert::IsTrue(value.has_value());
            Assert::AreEqual(stringVal, *value);
        }

        TEST_METHOD (AddPropertyString)
        {
            PowerToyValues values(m_moduleName, m_moduleKey);
            const std::wstring stringVal = L"Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.";
            values.add_property<std::wstring>(L"stringval", stringVal);

            auto value = values.get_string_value(L"stringval");
            Assert::IsTrue(value.has_value());
            Assert::AreEqual(stringVal, *value);
        }

        TEST_METHOD (AddPropertyJsonEmpty)
        {
            PowerToyValues values(m_moduleName, m_moduleKey);
            const auto json = json::JsonObject();
            values.add_property<json::JsonObject>(L"jsonval", json);

            auto value = values.get_json(L"jsonval");
            Assert::IsTrue(value.has_value());
            compareJsons(json, *value);
        }

        TEST_METHOD (AddPropertyJsonObject)
        {
            PowerToyValues values(m_moduleName, m_moduleKey);
            const auto json = json::JsonObject::Parse(m_json);
            values.add_property<json::JsonObject>(L"jsonval", json);

            auto value = values.get_json(L"jsonval");
            Assert::IsTrue(value.has_value());
            compareJsons(json, *value);
        }
    };

    TEST_CLASS (CustomActionObjectUnitTests)
    {
    public:
        TEST_METHOD (CustomActionObjectName)
        {
            const std::wstring json = L"{\"action_name\": \"action name\", \"value\": \"action value\"}";
            CustomActionObject obj = CustomActionObject::from_json_string(json);
            Assert::AreEqual(std::wstring(L"action name"), obj.get_name());
        }

        TEST_METHOD (CustomActionObjectValue)
        {
            const std::wstring json = L"{\"action_name\": \"action name\", \"value\": \"action value\"}";
            CustomActionObject obj = CustomActionObject::from_json_string(json);
            Assert::AreEqual(std::wstring(L"action value"), obj.get_value());
        }
    };

    TEST_CLASS (HotkeyObjectUnitTests)
    {
    private:
        json::JsonObject m_defaultHotkeyJson = json::JsonObject::Parse(L"{\"key\":\"(Key 0)\", \"code\": 123, \"win\": true, \"ctrl\": true, \"alt\": true, \"shift\": true}");
        json::JsonObject m_defaultHotkeyJsonAlternative = json::JsonObject::Parse(L"{\"key\":\"(Key 0)\", \"code\": 123, \"win\": false, \"ctrl\": false, \"alt\": false, \"shift\": false}");

    public:
        TEST_METHOD (GetKeyFromJson)
        {
            HotkeyObject object = HotkeyObject::from_json(m_defaultHotkeyJson);
            Assert::AreEqual(std::wstring(L"(Key 0)"), object.get_key());
        }

        TEST_METHOD (GetKeyFromJsonString)
        {
            HotkeyObject object = HotkeyObject::from_json_string(m_defaultHotkeyJson.Stringify());
            Assert::AreEqual(std::wstring(L"(Key 0)"), object.get_key());
        }

        TEST_METHOD (GetCodeFromJson)
        {
            HotkeyObject object = HotkeyObject::from_json(m_defaultHotkeyJson);
            Assert::AreEqual(123U, object.get_code());
        }

        TEST_METHOD (GetCodeFromJsonString)
        {
            HotkeyObject object = HotkeyObject::from_json_string(m_defaultHotkeyJson.Stringify());
            Assert::AreEqual(123U, object.get_code());
        }

        TEST_METHOD (GetCodeFromSettings)
        {
            HotkeyObject object = HotkeyObject::from_settings(true, true, true, true, 123);
            Assert::AreEqual(123U, object.get_code());
        }

        TEST_METHOD (GetWinPressedFromJson)
        {
            HotkeyObject object = HotkeyObject::from_json(m_defaultHotkeyJson);
            Assert::AreEqual(true, object.win_pressed());

            HotkeyObject objectNegativeValues = HotkeyObject::from_json(m_defaultHotkeyJsonAlternative);
            Assert::AreEqual(false, objectNegativeValues.win_pressed());
        }

        TEST_METHOD (GetWinPressedFromJsonString)
        {
            HotkeyObject object = HotkeyObject::from_json_string(m_defaultHotkeyJson.Stringify());
            Assert::AreEqual(true, object.win_pressed());

            HotkeyObject objectNegativeValues = HotkeyObject::from_json_string(m_defaultHotkeyJsonAlternative.Stringify());
            Assert::AreEqual(false, objectNegativeValues.win_pressed());
        }

        TEST_METHOD (GetWinPressedFromSettings)
        {
            HotkeyObject object = HotkeyObject::from_settings(true, true, true, true, 123);
            Assert::AreEqual(true, object.win_pressed());

            HotkeyObject objectNegativeValues = HotkeyObject::from_settings(false, true, true, true, 123);
            Assert::AreEqual(false, objectNegativeValues.win_pressed());
        }

        TEST_METHOD (GetCtrlPressedFromJson)
        {
            HotkeyObject object = HotkeyObject::from_json(m_defaultHotkeyJson);
            Assert::AreEqual(true, object.ctrl_pressed());

            HotkeyObject objectNegativeValues = HotkeyObject::from_json(m_defaultHotkeyJsonAlternative);
            Assert::AreEqual(false, objectNegativeValues.ctrl_pressed());
        }

        TEST_METHOD (GetCtrlPressedFromJsonString)
        {
            HotkeyObject object = HotkeyObject::from_json_string(m_defaultHotkeyJson.Stringify());
            Assert::AreEqual(true, object.ctrl_pressed());

            HotkeyObject objectNegativeValues = HotkeyObject::from_json_string(m_defaultHotkeyJsonAlternative.Stringify());
            Assert::AreEqual(false, objectNegativeValues.ctrl_pressed());
        }

        TEST_METHOD (GetCtrlPressedFromSettings)
        {
            HotkeyObject object = HotkeyObject::from_settings(true, true, true, true, 123);
            Assert::AreEqual(true, object.ctrl_pressed());

            HotkeyObject objectNegativeValues = HotkeyObject::from_settings(true, false, true, true, 123);
            Assert::AreEqual(false, objectNegativeValues.ctrl_pressed());
        }

        TEST_METHOD (GetAltPressedFromJson)
        {
            HotkeyObject object = HotkeyObject::from_json(m_defaultHotkeyJson);
            Assert::AreEqual(true, object.alt_pressed());

            HotkeyObject objectNegativeValues = HotkeyObject::from_json(m_defaultHotkeyJsonAlternative);
            Assert::AreEqual(false, objectNegativeValues.alt_pressed());
        }

        TEST_METHOD (GetAltPressedFromJsonString)
        {
            HotkeyObject object = HotkeyObject::from_json_string(m_defaultHotkeyJson.Stringify());
            Assert::AreEqual(true, object.alt_pressed());

            HotkeyObject objectNegativeValues = HotkeyObject::from_json_string(m_defaultHotkeyJsonAlternative.Stringify());
            Assert::AreEqual(false, objectNegativeValues.alt_pressed());
        }

        TEST_METHOD (GetAltPressedFromSettings)
        {
            HotkeyObject object = HotkeyObject::from_settings(true, true, true, true, 123);
            Assert::AreEqual(true, object.alt_pressed());

            HotkeyObject objectNegativeValues = HotkeyObject::from_settings(true, true, false, true, 123);
            Assert::AreEqual(false, objectNegativeValues.alt_pressed());
        }

        TEST_METHOD (GetShiftPressedFromJson)
        {
            HotkeyObject object = HotkeyObject::from_json(m_defaultHotkeyJson);
            Assert::AreEqual(true, object.shift_pressed());

            HotkeyObject objectNegativeValues = HotkeyObject::from_json(m_defaultHotkeyJsonAlternative);
            Assert::AreEqual(false, objectNegativeValues.shift_pressed());
        }

        TEST_METHOD (GetShiftPressedFromJsonString)
        {
            HotkeyObject object = HotkeyObject::from_json_string(m_defaultHotkeyJson.Stringify());
            Assert::AreEqual(true, object.shift_pressed());

            HotkeyObject objectNegativeValues = HotkeyObject::from_json_string(m_defaultHotkeyJsonAlternative.Stringify());
            Assert::AreEqual(false, objectNegativeValues.shift_pressed());
        }

        TEST_METHOD (GetShiftPressedFromSettings)
        {
            HotkeyObject object = HotkeyObject::from_settings(true, true, true, true, 123);
            Assert::AreEqual(true, object.shift_pressed());

            HotkeyObject objectNegativeValues = HotkeyObject::from_settings(true, true, true, false, 123);
            Assert::AreEqual(false, objectNegativeValues.shift_pressed());
        }

        TEST_METHOD (GetModifiersRepeat)
        {
            std::map<UINT, HotkeyObject> expectedMap = {
                std::make_pair(0x0000, HotkeyObject::from_settings(false, false, false, false, 0)),
                std::make_pair(0x0001, HotkeyObject::from_settings(false, false, true, false, 0)),
                std::make_pair(0x0002, HotkeyObject::from_settings(false, true, false, false, 0)),
                std::make_pair(0x0003, HotkeyObject::from_settings(false, true, true, false, 0)),
                std::make_pair(0x0004, HotkeyObject::from_settings(false, false, false, true, 0)),
                std::make_pair(0x0005, HotkeyObject::from_settings(false, false, true, true, 0)),
                std::make_pair(0x0006, HotkeyObject::from_settings(false, true, false, true, 0)),
                std::make_pair(0x0007, HotkeyObject::from_settings(false, true, true, true, 0)),
                std::make_pair(0x0008, HotkeyObject::from_settings(true, false, false, false, 0)),
                std::make_pair(0x0009, HotkeyObject::from_settings(true, false, true, false, 0)),
                std::make_pair(0x000A, HotkeyObject::from_settings(true, true, false, false, 0)),
                std::make_pair(0x000B, HotkeyObject::from_settings(true, true, true, false, 0)),
                std::make_pair(0x000C, HotkeyObject::from_settings(true, false, false, true, 0)),
                std::make_pair(0x000D, HotkeyObject::from_settings(true, false, true, true, 0)),
                std::make_pair(0x000E, HotkeyObject::from_settings(true, true, false, true, 0)),
                std::make_pair(0x000F, HotkeyObject::from_settings(true, true, true, true, 0))
            };

            for (const auto& iter : expectedMap)
            {
                Assert::AreEqual(iter.first, iter.second.get_modifiers_repeat());
            }
        }

        TEST_METHOD (GetModifiers)
        {
            std::map<UINT, HotkeyObject> expectedMap = {
                std::make_pair(0x4000, HotkeyObject::from_settings(false, false, false, false, 0)),
                std::make_pair(0x4001, HotkeyObject::from_settings(false, false, true, false, 0)),
                std::make_pair(0x4002, HotkeyObject::from_settings(false, true, false, false, 0)),
                std::make_pair(0x4003, HotkeyObject::from_settings(false, true, true, false, 0)),
                std::make_pair(0x4004, HotkeyObject::from_settings(false, false, false, true, 0)),
                std::make_pair(0x4005, HotkeyObject::from_settings(false, false, true, true, 0)),
                std::make_pair(0x4006, HotkeyObject::from_settings(false, true, false, true, 0)),
                std::make_pair(0x4007, HotkeyObject::from_settings(false, true, true, true, 0)),
                std::make_pair(0x4008, HotkeyObject::from_settings(true, false, false, false, 0)),
                std::make_pair(0x4009, HotkeyObject::from_settings(true, false, true, false, 0)),
                std::make_pair(0x400A, HotkeyObject::from_settings(true, true, false, false, 0)),
                std::make_pair(0x400B, HotkeyObject::from_settings(true, true, true, false, 0)),
                std::make_pair(0x400C, HotkeyObject::from_settings(true, false, false, true, 0)),
                std::make_pair(0x400D, HotkeyObject::from_settings(true, false, true, true, 0)),
                std::make_pair(0x400E, HotkeyObject::from_settings(true, true, false, true, 0)),
                std::make_pair(0x400F, HotkeyObject::from_settings(true, true, true, true, 0))
            };

            for (const auto& iter : expectedMap)
            {
                Assert::AreEqual(iter.first, iter.second.get_modifiers());
            }
        }
    };
}
