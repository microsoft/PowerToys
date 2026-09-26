// Copyright (c) Microsoft Corporation.
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <CppUnitTest.h>
#include "CliProtocol.h"
#include "LocalizedStrings.h"
#include "TestSupport.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace light_switch_cli;

namespace LightSwitchServiceUnitTests
{
    TEST_CLASS (LocalizedStringsTests)
    {
    public:
        TEST_METHOD (NeutralResourcesBelongToTheTestModule)
        {
            const auto instance = reinterpret_cast<HINSTANCE>(&__ImageBase);
            Assert::IsTrue(instance != GetModuleHandleW(nullptr));
            Assert::AreEqual(L"Light Switch is stopping.", get_english_fallback_string(IDS_SERVICE_STOPPING, instance).c_str());
        }

        TEST_METHOD (WindowsErrorPlaceholdersAreFormatted)
        {
            Apartment apartment;
            const auto message = LightSwitchStrings::Format(GET_RESOURCE_STRING(IDS_SETTINGS_OPEN_AFTER_INITIALIZATION_FAILED), 12345, 54321);
            Assert::IsTrue(message.find(L"12345") != std::wstring::npos);
            Assert::IsTrue(message.find(L"54321") != std::wstring::npos);
            Assert::IsTrue(message.find(L"IDS_") == std::wstring::npos);
            Assert::IsTrue(message.find(L"{0}") == std::wstring::npos);
            Assert::IsTrue(message.find(L"{1}") == std::wstring::npos);
        }

        TEST_METHOD (ProtocolErrorsUseResourcesAndKeepInvariantCodesAndModes)
        {
            Apartment apartment;
            try
            {
                ParseRequest(L"{\"version\":1,\"command\":\"schedule-enable\",\"mode\":\"Off\"}");
                Assert::Fail(L"The unsupported mode should have been rejected.");
            }
            catch (const RequestError& error)
            {
                Assert::AreEqual(L"INVALID_ARGUMENT", error.Code().c_str());
                Assert::AreEqual(GET_RESOURCE_STRING(IDS_REQUEST_MODE_UNSUPPORTED).c_str(), error.Message().c_str());
                Assert::IsFalse(error.Message().empty());
                Assert::IsTrue(error.Message().find(L"IDS_") == std::wstring::npos);
                for (const auto mode : { L"FixedHours", L"SunsetToSunrise", L"FollowNightLight" })
                {
                    Assert::IsTrue(error.Message().find(mode) != std::wstring::npos);
                    const auto request = ParseRequest(L"{\"version\":1,\"command\":\"schedule-enable\",\"mode\":\"" + std::wstring(mode) + L"\"}");
                    Assert::AreEqual(mode, request.mode->c_str());
                }
            }
        }
    };
}
