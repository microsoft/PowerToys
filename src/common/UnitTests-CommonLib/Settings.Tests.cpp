#include "pch.h"
#include <settings_objects.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace PowerToysSettings;

namespace UnitTestsCommonLib
{
    TEST_CLASS(SettingsUnitTests)
    {
    private:
        const std::wstring m_json = L"{\"name\":\"Module Name\",\"properties\" : {\"bool_toggle_true\":{\"value\":true},\"bool_toggle_false\":{\"value\":false},\"color_picker\" : {\"value\":\"#ff8d12\"},\"int_spinner\" : {\"value\":10},\"string_text\" : {\"value\":\"a quick fox\"}},\"version\" : \"1.0\" }";
        const std::wstring m_moduleName = L"Module Name";

        void compareJsons(const json::JsonObject& expected, const json::JsonObject& actual)
        {
            auto iter = expected.First();
            while (iter.HasCurrent())
            {
                const auto key = iter.Current().Key();
                Assert::IsTrue(actual.HasKey(key));

                const std::wstring expectedStringified = iter.Current().Value().Stringify().c_str();
                const std::wstring actualStringified = actual.GetNamedValue(key).Stringify().c_str();

                Assert::AreEqual(expectedStringified, actualStringified);
                iter.MoveNext();
            }
        }

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
}
