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
    };
}
