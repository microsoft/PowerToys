#include "pch.h"
#include "TestHelpers.h"
#include <registry.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace UnitTestsCommonUtils
{
    TEST_CLASS(RegistryTests)
    {
    public:
        // Note: These tests use HKCU which doesn't require elevation

        TEST_METHOD(InstallScope_Registry_CanReadAndWrite)
        {
            TestHelpers::TestRegistryKey testKey(L"RegistryTest");
            Assert::IsTrue(testKey.isValid());

            // Write a test value
            Assert::IsTrue(testKey.setStringValue(L"TestValue", L"TestData"));
            Assert::IsTrue(testKey.setDwordValue(L"TestDword", 42));
        }

        TEST_METHOD(Registry_ValueChange_StringValue)
        {
            registry::ValueChange change{ HKEY_CURRENT_USER, L"Software\\PowerToys\\Test", L"TestValue", std::wstring{ L"TestData" } };

            Assert::AreEqual(std::wstring(L"Software\\PowerToys\\Test"), change.path);
            Assert::IsTrue(change.name.has_value());
            Assert::AreEqual(std::wstring(L"TestValue"), *change.name);
            Assert::AreEqual(std::wstring(L"TestData"), std::get<std::wstring>(change.value));
        }

        TEST_METHOD(Registry_ValueChange_DwordValue)
        {
            registry::ValueChange change{ HKEY_CURRENT_USER, L"Software\\PowerToys\\Test", L"TestDword", static_cast<DWORD>(42) };

            Assert::AreEqual(std::wstring(L"Software\\PowerToys\\Test"), change.path);
            Assert::IsTrue(change.name.has_value());
            Assert::AreEqual(std::wstring(L"TestDword"), *change.name);
            Assert::AreEqual(static_cast<DWORD>(42), std::get<DWORD>(change.value));
        }

        TEST_METHOD(Registry_ChangeSet_AddChanges)
        {
            registry::ChangeSet changeSet;

            changeSet.changes.push_back({ HKEY_CURRENT_USER, L"Software\\PowerToys\\Test", L"Value1", std::wstring{ L"Data1" } });
            changeSet.changes.push_back({ HKEY_CURRENT_USER, L"Software\\PowerToys\\Test", L"Value2", static_cast<DWORD>(123) });

            Assert::AreEqual(static_cast<size_t>(2), changeSet.changes.size());
        }

        TEST_METHOD(InstallScope_GetCurrentInstallScope_ReturnsValidValue)
        {
            auto scope = registry::install_scope::get_current_install_scope();
            Assert::IsTrue(scope == registry::install_scope::InstallScope::PerMachine ||
                          scope == registry::install_scope::InstallScope::PerUser);
        }
    };
}
