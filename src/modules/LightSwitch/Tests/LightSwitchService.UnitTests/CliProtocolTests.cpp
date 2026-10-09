// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <CppUnitTest.h>
#include "CliProtocol.h"
#include "TestSupport.h"

#include <array>
#include <utility>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace light_switch_cli;
using namespace winrt::Windows::Data::Json;

namespace LightSwitchServiceUnitTests
{
    TEST_CLASS (CliProtocolTests)
    {
    public:
        TEST_METHOD (AcceptsSupportedCommandsAndExplicitModes)
        {
            Apartment apartment;
            const std::array<std::pair<std::wstring_view, Command>, 6> commands{
                { { L"status", Command::Status }, { L"light", Command::Light }, { L"dark", Command::Dark }, { L"toggle", Command::Toggle }, { L"schedule-enable", Command::ScheduleEnable }, { L"schedule-disable", Command::ScheduleDisable } }
            };
            for (const auto& command : commands)
            {
                const auto request = ParseRequest(L"{\"version\":1,\"command\":\"" + std::wstring(command.first) + L"\"}");
                Assert::IsTrue(command.second == request.command);
                Assert::IsFalse(request.mode.has_value());
            }
            for (const auto mode : { L"FixedHours", L"SunsetToSunrise", L"FollowNightLight" })
            {
                const auto request = ParseRequest(L"{\"version\":1,\"command\":\"schedule-enable\",\"mode\":\"" + std::wstring(mode) + L"\"}");
                Assert::IsTrue(request.command == Command::ScheduleEnable);
                Assert::AreEqual(std::wstring(mode), request.mode.value());
            }
        }

        TEST_METHOD (RejectsUnknownOrIncompatibleArguments)
        {
            Apartment apartment;
            for (const auto message : {
                     L"{\"version\":1,\"command\":\"unknown\"}",
                     L"{\"version\":1,\"command\":true}",
                     L"{\"version\":1}",
                     L"{\"version\":1,\"command\":\"status\",\"mode\":\"FixedHours\"}",
                     L"{\"version\":1,\"command\":\"schedule-disable\",\"mode\":\"FixedHours\"}",
                     L"{\"version\":1,\"command\":\"schedule-enable\",\"mode\":null}",
                     L"{\"version\":1,\"command\":\"schedule-enable\",\"mode\":\"Off\"}",
                     L"{\"version\":1,\"command\":\"schedule-enable\",\"mode\":\"fixedhours\"}",
                     L"{\"version\":1,\"command\":\"status\",\"extra\":0}" })
            {
                AssertRequestError(message, L"INVALID_ARGUMENT");
            }
        }

        TEST_METHOD (RejectsMalformedAndUnsupportedProtocol)
        {
            Apartment apartment;
            for (const auto message : {
                     L"", L"not json", L"[]", L"null", L"{", L"{\"command\":\"status\"}", L"{\"version\":2,\"command\":\"status\"}", L"{\"version\":1.5,\"command\":\"status\"}", L"{\"version\":\"1\",\"command\":\"status\"}", L"{\"version\":1,\"command\":\"status\"} {}", L"{\"version\":1,\"command\":\"status\",\"command\":\"toggle\"}", L"{\"version\":1,\"command\":\"status\",\"\\u0063ommand\":\"toggle\"}" })
            {
                AssertRequestError(message, L"PROTOCOL_ERROR");
            }
        }

        TEST_METHOD (EnforcesBomlessUtf16AndMessageLength)
        {
            Apartment apartment;
            const std::wstring valid = L"{\"version\":1,\"command\":\"status\"}";
            AssertRequestError(std::wstring(1, L'\xfeff') + valid, L"PROTOCOL_ERROR");
            AssertRequestError(valid + std::wstring(1, L'\0'), L"PROTOCOL_ERROR");
            AssertRequestError(valid + std::wstring(1, L'\xd800'), L"PROTOCOL_ERROR");
            AssertRequestError(valid + std::wstring(1, L'\xdc00'), L"PROTOCOL_ERROR");
            AssertRequestError(std::wstring(MaxMessageCharacters + 1, L' '), L"PROTOCOL_ERROR");
            AssertRequestError(std::wstring(16000, L'[') + std::wstring(16000, L']'), L"PROTOCOL_ERROR");
            const auto atLimit = std::wstring(MaxMessageCharacters - valid.size(), L' ') + valid;
            Assert::IsTrue(ParseRequest(atLimit).command == Command::Status);
        }

        TEST_METHOD (ResponseEnvelopesRetainStateAndStructuredError)
        {
            Apartment apartment;
            const auto state = TestState();
            const auto success = MakeSuccess(state);
            Assert::AreEqual(1.0, success.GetNamedNumber(L"version"));
            Assert::IsTrue(success.GetNamedBoolean(L"success"));
            Assert::IsFalse(success.HasKey(L"error"));
            Assert::AreEqual(std::wstring(L"unknown"), std::wstring(success.GetNamedObject(L"state").GetNamedString(L"appsTheme")));

            const auto failure = MakeError(L"EXECUTION_FAILED", L"A test failure", state);
            Assert::IsFalse(failure.GetNamedBoolean(L"success"));
            Assert::IsTrue(failure.HasKey(L"state"));
            Assert::AreEqual(std::wstring(L"EXECUTION_FAILED"), std::wstring(failure.GetNamedObject(L"error").GetNamedString(L"code")));
            Assert::IsFalse(MakeError(L"INVALID_ARGUMENT", L"Invalid request").HasKey(L"state"));
        }

    private:
        static void AssertRequestError(std::wstring_view message, std::wstring_view expectedCode)
        {
            try
            {
                ParseRequest(message);
                Assert::Fail(L"The request should have been rejected.");
            }
            catch (const RequestError& error)
            {
                Assert::AreEqual(std::wstring(expectedCode), error.Code());
                Assert::IsFalse(error.Message().empty());
            }
        }
    };
}
