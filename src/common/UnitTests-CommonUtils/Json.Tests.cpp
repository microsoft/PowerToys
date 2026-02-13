#include "pch.h"
#include "TestHelpers.h"
#include <json.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace winrt::Windows::Data::Json;

namespace UnitTestsCommonUtils
{
    TEST_CLASS(JsonTests)
    {
    public:
        // from_file tests
        TEST_METHOD(FromFile_NonExistentFile_ReturnsNullopt)
        {
            auto result = json::from_file(L"C:\\NonExistent\\File\\Path.json");
            Assert::IsFalse(result.has_value());
        }

        TEST_METHOD(FromFile_ValidJsonFile_ReturnsJsonObject)
        {
            TestHelpers::TempFile tempFile(L"", L".json");
            tempFile.write("{\"key\": \"value\"}");

            auto result = json::from_file(tempFile.path());
            Assert::IsTrue(result.has_value());
        }

        TEST_METHOD(FromFile_InvalidJson_ReturnsNullopt)
        {
            TestHelpers::TempFile tempFile(L"", L".json");
            tempFile.write("not valid json {{{");

            auto result = json::from_file(tempFile.path());
            Assert::IsFalse(result.has_value());
        }

        TEST_METHOD(FromFile_EmptyFile_ReturnsNullopt)
        {
            TestHelpers::TempFile tempFile(L"", L".json");
            // File is empty

            auto result = json::from_file(tempFile.path());
            Assert::IsFalse(result.has_value());
        }

        TEST_METHOD(FromFile_ValidComplexJson_ParsesCorrectly)
        {
            TestHelpers::TempFile tempFile(L"", L".json");
            tempFile.write("{\"name\": \"test\", \"value\": 42, \"enabled\": true}");

            auto result = json::from_file(tempFile.path());
            Assert::IsTrue(result.has_value());

            auto& obj = *result;
            Assert::IsTrue(obj.HasKey(L"name"));
            Assert::IsTrue(obj.HasKey(L"value"));
            Assert::IsTrue(obj.HasKey(L"enabled"));
        }

        // to_file tests
        TEST_METHOD(ToFile_ValidObject_WritesFile)
        {
            TestHelpers::TempFile tempFile(L"", L".json");

            JsonObject obj;
            obj.SetNamedValue(L"key", JsonValue::CreateStringValue(L"value"));
            json::to_file(tempFile.path(), obj);

            // Read back and verify
            auto result = json::from_file(tempFile.path());
            Assert::IsTrue(result.has_value());
            Assert::IsTrue(result->HasKey(L"key"));
        }

        TEST_METHOD(ToFile_ComplexObject_WritesFile)
        {
            TestHelpers::TempFile tempFile(L"", L".json");

            JsonObject obj;
            obj.SetNamedValue(L"name", JsonValue::CreateStringValue(L"test"));
            obj.SetNamedValue(L"value", JsonValue::CreateNumberValue(42));
            obj.SetNamedValue(L"enabled", JsonValue::CreateBooleanValue(true));
            json::to_file(tempFile.path(), obj);

            auto result = json::from_file(tempFile.path());
            Assert::IsTrue(result.has_value());
            Assert::AreEqual(std::wstring(L"test"), std::wstring(result->GetNamedString(L"name")));
            Assert::AreEqual(42.0, result->GetNamedNumber(L"value"));
            Assert::IsTrue(result->GetNamedBoolean(L"enabled"));
        }

        // has tests
        TEST_METHOD(Has_ExistingKey_ReturnsTrue)
        {
            JsonObject obj;
            obj.SetNamedValue(L"key", JsonValue::CreateStringValue(L"value"));
            Assert::IsTrue(json::has(obj, L"key", JsonValueType::String));
        }

        TEST_METHOD(Has_NonExistingKey_ReturnsFalse)
        {
            JsonObject obj;
            Assert::IsFalse(json::has(obj, L"key", JsonValueType::String));
        }

        TEST_METHOD(Has_WrongType_ReturnsFalse)
        {
            JsonObject obj;
            obj.SetNamedValue(L"key", JsonValue::CreateStringValue(L"value"));
            Assert::IsFalse(json::has(obj, L"key", JsonValueType::Number));
        }

        TEST_METHOD(Has_NumberType_ReturnsTrue)
        {
            JsonObject obj;
            obj.SetNamedValue(L"key", JsonValue::CreateNumberValue(42));
            Assert::IsTrue(json::has(obj, L"key", JsonValueType::Number));
        }

        TEST_METHOD(Has_BooleanType_ReturnsTrue)
        {
            JsonObject obj;
            obj.SetNamedValue(L"key", JsonValue::CreateBooleanValue(true));
            Assert::IsTrue(json::has(obj, L"key", JsonValueType::Boolean));
        }

        TEST_METHOD(Has_ObjectType_ReturnsTrue)
        {
            JsonObject obj;
            JsonObject nested;
            obj.SetNamedValue(L"key", nested);
            Assert::IsTrue(json::has(obj, L"key", JsonValueType::Object));
        }

        // value function tests
        TEST_METHOD(Value_IntegerType_CreatesNumberValue)
        {
            auto val = json::value(42);
            Assert::IsTrue(val.ValueType() == JsonValueType::Number);
            Assert::AreEqual(42.0, val.GetNumber());
        }

        TEST_METHOD(Value_DoubleType_CreatesNumberValue)
        {
            auto val = json::value(3.14);
            Assert::IsTrue(val.ValueType() == JsonValueType::Number);
            Assert::AreEqual(3.14, val.GetNumber());
        }

        TEST_METHOD(Value_BooleanTrue_CreatesBooleanValue)
        {
            auto val = json::value(true);
            Assert::IsTrue(val.ValueType() == JsonValueType::Boolean);
            Assert::IsTrue(val.GetBoolean());
        }

        TEST_METHOD(Value_BooleanFalse_CreatesBooleanValue)
        {
            auto val = json::value(false);
            Assert::IsTrue(val.ValueType() == JsonValueType::Boolean);
            Assert::IsFalse(val.GetBoolean());
        }

        TEST_METHOD(Value_String_CreatesStringValue)
        {
            auto val = json::value(L"hello");
            Assert::IsTrue(val.ValueType() == JsonValueType::String);
            Assert::AreEqual(std::wstring(L"hello"), std::wstring(val.GetString()));
        }

        TEST_METHOD(Value_JsonObject_ReturnsJsonValue)
        {
            JsonObject obj;
            obj.SetNamedValue(L"key", JsonValue::CreateStringValue(L"value"));
            auto val = json::value(obj);
            Assert::IsTrue(val.ValueType() == JsonValueType::Object);
        }

        TEST_METHOD(Value_JsonValue_ReturnsIdentity)
        {
            auto original = JsonValue::CreateStringValue(L"test");
            auto result = json::value(original);
            Assert::AreEqual(std::wstring(L"test"), std::wstring(result.GetString()));
        }

        // get function tests
        TEST_METHOD(Get_BooleanValue_ReturnsValue)
        {
            JsonObject obj;
            obj.SetNamedValue(L"enabled", JsonValue::CreateBooleanValue(true));

            bool result = false;
            json::get(obj, L"enabled", result);
            Assert::IsTrue(result);
        }

        TEST_METHOD(Get_IntValue_ReturnsValue)
        {
            JsonObject obj;
            obj.SetNamedValue(L"count", JsonValue::CreateNumberValue(42));

            int result = 0;
            json::get(obj, L"count", result);
            Assert::AreEqual(42, result);
        }

        TEST_METHOD(Get_DoubleValue_ReturnsValue)
        {
            JsonObject obj;
            obj.SetNamedValue(L"ratio", JsonValue::CreateNumberValue(3.14));

            double result = 0.0;
            json::get(obj, L"ratio", result);
            Assert::AreEqual(3.14, result);
        }

        TEST_METHOD(Get_StringValue_ReturnsValue)
        {
            JsonObject obj;
            obj.SetNamedValue(L"name", JsonValue::CreateStringValue(L"test"));

            std::wstring result;
            json::get(obj, L"name", result);
            Assert::AreEqual(std::wstring(L"test"), result);
        }

        TEST_METHOD(Get_MissingKey_UsesDefault)
        {
            JsonObject obj;

            int result = 0;
            json::get(obj, L"missing", result, 99);
            Assert::AreEqual(99, result);
        }

        TEST_METHOD(Get_MissingKeyNoDefault_PreservesOriginal)
        {
            JsonObject obj;

            int result = 42;
            json::get(obj, L"missing", result);
            // When key is missing and no default, original value is preserved
            Assert::AreEqual(42, result);
        }

        TEST_METHOD(Get_JsonObject_ReturnsObject)
        {
            JsonObject obj;
            JsonObject nested;
            nested.SetNamedValue(L"inner", JsonValue::CreateStringValue(L"value"));
            obj.SetNamedValue(L"nested", nested);

            JsonObject result;
            json::get(obj, L"nested", result);
            Assert::IsTrue(result.HasKey(L"inner"));
        }

        // Roundtrip tests
        TEST_METHOD(Roundtrip_ComplexObject_PreservesData)
        {
            TestHelpers::TempFile tempFile(L"", L".json");

            JsonObject original;
            original.SetNamedValue(L"string", JsonValue::CreateStringValue(L"hello"));
            original.SetNamedValue(L"number", JsonValue::CreateNumberValue(42));
            original.SetNamedValue(L"boolean", JsonValue::CreateBooleanValue(true));

            JsonObject nested;
            nested.SetNamedValue(L"inner", JsonValue::CreateStringValue(L"world"));
            original.SetNamedValue(L"object", nested);

            json::to_file(tempFile.path(), original);
            auto loaded = json::from_file(tempFile.path());

            Assert::IsTrue(loaded.has_value());
            Assert::AreEqual(std::wstring(L"hello"), std::wstring(loaded->GetNamedString(L"string")));
            Assert::AreEqual(42.0, loaded->GetNamedNumber(L"number"));
            Assert::IsTrue(loaded->GetNamedBoolean(L"boolean"));
            Assert::AreEqual(std::wstring(L"world"), std::wstring(loaded->GetNamedObject(L"object").GetNamedString(L"inner")));
        }
    };
}
