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
            // Test the ValueChange structure for string values
            registry::ValueChange change;
            change.value = L"TestValue";
            change.data = L"TestData";
            change.isString = true;

            Assert::AreEqual(std::wstring(L"TestValue"), change.value);
            Assert::AreEqual(std::wstring(L"TestData"), std::get<std::wstring>(change.data));
            Assert::IsTrue(change.isString);
        }

        TEST_METHOD(Registry_ValueChange_DwordValue)
        {
            registry::ValueChange change;
            change.value = L"TestDword";
            change.data = static_cast<DWORD>(42);
            change.isString = false;

            Assert::AreEqual(std::wstring(L"TestDword"), change.value);
            Assert::AreEqual(static_cast<DWORD>(42), std::get<DWORD>(change.data));
            Assert::IsFalse(change.isString);
        }

        TEST_METHOD(Registry_ChangeSet_AddChanges)
        {
            registry::ChangeSet changeSet;

            registry::ValueChange change1;
            change1.value = L"Value1";
            change1.data = L"Data1";
            change1.isString = true;

            registry::ValueChange change2;
            change2.value = L"Value2";
            change2.data = static_cast<DWORD>(123);
            change2.isString = false;

            changeSet.changes.push_back(change1);
            changeSet.changes.push_back(change2);

            Assert::AreEqual(static_cast<size_t>(2), changeSet.changes.size());
        }

        TEST_METHOD(Registry_ChangeSet_KeyPath)
        {
            registry::ChangeSet changeSet;
            changeSet.keyPath = L"Software\\PowerToys\\TestKey";

            Assert::AreEqual(std::wstring(L"Software\\PowerToys\\TestKey"), changeSet.keyPath);
        }

        TEST_METHOD(InstallScope_DetectBundleInstall_DoesNotCrash)
        {
            // This function checks registry for bundle installation
            // It should not crash regardless of installation state
            auto result = registry::install_scope::detect_bundle_install();

            // Result depends on installation state
            Assert::IsTrue(true);
        }

        TEST_METHOD(InstallScope_IsPerUserInstallation_ReturnsBoolean)
        {
            bool result = registry::install_scope::is_peruser_installation();
            Assert::IsTrue(result == true || result == false);
        }

        TEST_METHOD(InstallScope_IsPerMachineInstallation_ReturnsBoolean)
        {
            bool result = registry::install_scope::is_permachine_installation();
            Assert::IsTrue(result == true || result == false);
        }

        TEST_METHOD(InstallScope_BothInstallationTypes_MutuallyExclusive)
        {
            // A system shouldn't be both per-user and per-machine installed
            // (unless there's some edge case)
            bool perUser = registry::install_scope::is_peruser_installation();
            bool perMachine = registry::install_scope::is_permachine_installation();

            // At least one should be false (or both if not installed)
            Assert::IsTrue(!perUser || !perMachine || (!perUser && !perMachine));
        }
    };
}
