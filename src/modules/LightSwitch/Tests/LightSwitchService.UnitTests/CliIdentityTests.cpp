// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <CppUnitTest.h>
#include "CliIdentity.h"

#include <sddl.h>
#include <wil/resource.h>
#include <winrt/base.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace light_switch_cli;

namespace LightSwitchServiceUnitTests
{
    namespace
    {
        std::vector<BYTE> TestSid(const wchar_t* text)
        {
            PSID raw = nullptr;
            winrt::check_bool(ConvertStringSidToSidW(text, &raw));
            wil::unique_hlocal sid(raw);
            std::vector<BYTE> result(GetLengthSid(sid.get()));
            winrt::check_bool(CopySid(static_cast<DWORD>(result.size()), result.data(), sid.get()));
            return result;
        }

        TokenIdentity TestIdentity()
        {
            return { 2, TestSid(L"S-1-5-21-1-2-3-1000"), TestSid(L"S-1-5-5-10-20") };
        }
    }

    TEST_CLASS (CliIdentityTests)
    {
    public:
        TEST_METHOD (RejectsADifferentUserInTheSameSessionAndLogon)
        {
            const auto server = TestIdentity();
            auto client = server;
            client.userSid = TestSid(L"S-1-5-21-1-2-3-1001");
            Assert::IsFalse(IsSameLogon(server, client));
        }

        TEST_METHOD (RejectsADifferentDesktopSessionForTheSameUserAndLogon)
        {
            const auto server = TestIdentity();
            auto client = server;
            client.sessionId = 3;
            Assert::IsFalse(IsSameLogon(server, client));
        }

        TEST_METHOD (RejectsAnIndependentLogonForTheSameUserAndDesktopSession)
        {
            const auto server = TestIdentity();
            auto client = server;
            client.logonSid = TestSid(L"S-1-5-5-10-21");
            Assert::IsFalse(IsSameLogon(server, client));
        }

        TEST_METHOD (MissingIdentityFieldsNeverAuthenticate)
        {
            const auto complete = TestIdentity();
            Assert::IsTrue(IsSameLogon(complete, complete));
            auto incomplete = complete;
            incomplete.sessionId.reset();
            Assert::IsFalse(IsSameLogon(complete, incomplete));
            Assert::IsFalse(IsSameLogon(incomplete, complete));
            incomplete = complete;
            incomplete.userSid.clear();
            Assert::IsFalse(IsSameLogon(complete, incomplete));
            Assert::IsFalse(IsSameLogon(incomplete, complete));
            incomplete = complete;
            incomplete.logonSid.clear();
            Assert::IsFalse(IsSameLogon(complete, incomplete));
            Assert::IsFalse(IsSameLogon(incomplete, complete));
            Assert::IsFalse(IsSameLogon({}, {}));
        }

        TEST_METHOD (AnUnreadableTokenCannotProduceAnIdentity)
        {
            Assert::ExpectException<winrt::hresult_error>([]() { ReadTokenIdentity(nullptr); });
        }

        TEST_METHOD (CurrentTokenAndAnyLinkedUacTokenShareTheAcceptedIdentity)
        {
            wil::unique_handle current;
            winrt::check_bool(OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, current.put()));
            const auto currentIdentity = ReadTokenIdentity(current.get());
            Assert::IsTrue(IsSameLogon(currentIdentity, currentIdentity));
            TOKEN_ELEVATION_TYPE elevation{};
            DWORD size = 0;
            winrt::check_bool(GetTokenInformation(current.get(), TokenElevationType, &elevation, sizeof(elevation), &size));
            if (elevation == TokenElevationTypeDefault)
            {
                Logger::WriteMessage(L"The current token was validated; linked UAC token coverage is unavailable for this account.");
                return;
            }

            // Query the existing pair without impersonating it or starting an elevated process.
            TOKEN_LINKED_TOKEN linkedInfo{};
            winrt::check_bool(GetTokenInformation(current.get(), TokenLinkedToken, &linkedInfo, sizeof(linkedInfo), &size));
            wil::unique_handle linked(linkedInfo.LinkedToken);
            TOKEN_STATISTICS currentStatistics{};
            TOKEN_STATISTICS linkedStatistics{};
            winrt::check_bool(GetTokenInformation(current.get(), TokenStatistics, &currentStatistics, sizeof(currentStatistics), &size));
            winrt::check_bool(GetTokenInformation(linked.get(), TokenStatistics, &linkedStatistics, sizeof(linkedStatistics), &size));
            Assert::IsTrue(currentStatistics.AuthenticationId.LowPart != linkedStatistics.AuthenticationId.LowPart ||
                           currentStatistics.AuthenticationId.HighPart != linkedStatistics.AuthenticationId.HighPart,
                           L"This regression requires UAC's distinct authentication IDs.");

            const auto linkedIdentity = ReadTokenIdentity(linked.get());
            Assert::IsTrue(IsSameLogon(currentIdentity, linkedIdentity));
            Assert::IsTrue(IsSameLogon(linkedIdentity, currentIdentity));
            Logger::WriteMessage(L"Validated both directions of a real UAC token pair with different authentication IDs.");
        }
    };
}
