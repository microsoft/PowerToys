// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <CppUnitTest.h>
#include "ThemeHelper.h"
#include <atomic>
#include <string>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace LightSwitchServiceUnitTests
{
    namespace
    {
        struct TemporaryRegistryKey
        {
            HKEY key = nullptr;
            std::wstring path;
            TemporaryRegistryKey()
            {
                static std::atomic<unsigned long> sequence{ 0 };
                path = L"Software\\Microsoft\\PowerToys\\LightSwitchUnitTests\\" + std::to_wstring(GetCurrentProcessId()) + L"-" + std::to_wstring(++sequence);
                Assert::AreEqual<LSTATUS>(ERROR_SUCCESS, RegCreateKeyExW(HKEY_CURRENT_USER, path.c_str(), 0, nullptr, 0, KEY_QUERY_VALUE | KEY_SET_VALUE, nullptr, &key, nullptr));
            }
            ~TemporaryRegistryKey()
            {
                if (key)
                    RegCloseKey(key);
                RegDeleteTreeW(HKEY_CURRENT_USER, path.c_str());
            }
        };
    }

    TEST_CLASS (ThemeHelperTests)
    {
    public:
        TEST_METHOD (ThemeValuesCanBeWrittenAndVerifiedWithoutBroadcasting)
        {
            TemporaryRegistryKey registry;
            bool light = false;
            Assert::AreEqual<LSTATUS>(ERROR_SUCCESS, LightSwitchThemeHelpers::WriteThemeValue(registry.key, L"Theme", true));
            Assert::AreEqual<LSTATUS>(ERROR_SUCCESS, LightSwitchThemeHelpers::ReadThemeValue(registry.key, L"Theme", light));
            Assert::IsTrue(light);
            Assert::AreEqual<LSTATUS>(ERROR_SUCCESS, LightSwitchThemeHelpers::WriteThemeValue(registry.key, L"Theme", false));
            Assert::AreEqual<LSTATUS>(ERROR_SUCCESS, LightSwitchThemeHelpers::ReadThemeValue(registry.key, L"Theme", light));
            Assert::IsFalse(light);
        }

        TEST_METHOD (MissingValuesDoNotGuessLightOrModifyTheOutput)
        {
            TemporaryRegistryKey registry;
            bool light = false;
            Assert::AreEqual<LSTATUS>(ERROR_FILE_NOT_FOUND, LightSwitchThemeHelpers::ReadThemeValue(registry.key, L"Missing", light));
            Assert::IsFalse(light);
        }

        TEST_METHOD (InvalidTypesAndValuesAreRejected)
        {
            TemporaryRegistryKey registry;
            DWORD value = 2;
            Assert::AreEqual<LSTATUS>(ERROR_SUCCESS, RegSetValueExW(registry.key, L"Theme", 0, REG_DWORD, reinterpret_cast<const BYTE*>(&value), sizeof(value)));
            bool light = true;
            Assert::AreEqual<LSTATUS>(ERROR_INVALID_DATA, LightSwitchThemeHelpers::ReadThemeValue(registry.key, L"Theme", light));
            Assert::IsTrue(light);
            const wchar_t text[] = L"light";
            Assert::AreEqual<LSTATUS>(ERROR_SUCCESS, RegSetValueExW(registry.key, L"Theme", 0, REG_SZ, reinterpret_cast<const BYTE*>(text), sizeof(text)));
            Assert::IsTrue(LightSwitchThemeHelpers::ReadThemeValue(registry.key, L"Theme", light) != ERROR_SUCCESS);
        }

        TEST_METHOD (ReadOnlyKeysReportWriteFailures)
        {
            TemporaryRegistryKey registry;
            HKEY readOnly = nullptr;
            Assert::AreEqual<LSTATUS>(ERROR_SUCCESS, RegOpenKeyExW(HKEY_CURRENT_USER, registry.path.c_str(), 0, KEY_QUERY_VALUE, &readOnly));
            const auto result = LightSwitchThemeHelpers::WriteThemeValue(readOnly, L"Theme", true);
            RegCloseKey(readOnly);
            Assert::AreEqual<LSTATUS>(ERROR_ACCESS_DENIED, result);
        }
    };
}
