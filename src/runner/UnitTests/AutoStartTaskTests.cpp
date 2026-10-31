#include "pch.h"
#include "CppUnitTest.h"

#include "../auto_start_task.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace
{
    const std::wstring Executable = L"C:\\Program Files\\PowerToys\\PowerToys.exe";
    const std::wstring UserSid = L"S-1-5-21-1004336348-1177238915-682003330-1001";
    const std::wstring OtherUserSid = L"S-1-5-21-1004336348-1177238915-682003330-1002";
    const std::wstring ExpectedDescriptor = L"D:(A;;FA;;;SY)(A;;FA;;;BA)(A;;FA;;;" + UserSid + L")";
    const std::wstring RegisteredDescriptor = ExpectedDescriptor + L"(A;ID;0x1f019f;;;BA)(A;ID;0x1f019f;;;SY)(A;ID;FA;;;" + UserSid + L")(A;;FR;;;" + UserSid + L")";

    auto_start_task::RegisteredTask PowerToysTask()
    {
        return { Executable, true, L"PT3S", RegisteredDescriptor };
    }
}

namespace RunnerUnitTests
{
    TEST_CLASS (AutoStartTaskTests)
    {
    public:
        TEST_METHOD (TaskRegisteredByPowerToysMatches)
        {
            Assert::IsTrue(auto_start_task::matches(PowerToysTask(), Executable, true));
        }

        TEST_METHOD (CustomizedLogonDelayStillMatches)
        {
            auto task = PowerToysTask();
            task.logonTriggerDelay = L"PT1M";
            Assert::IsTrue(auto_start_task::matches(task, Executable, true));

            task.logonTriggerDelay = L"";
            Assert::IsTrue(auto_start_task::matches(task, Executable, true));
        }

        TEST_METHOD (ExecutablePathIsComparedCaseInsensitively)
        {
            auto task = PowerToysTask();
            task.executablePath = L"c:\\program files\\powertoys\\POWERTOYS.EXE";
            Assert::IsTrue(auto_start_task::matches(task, Executable, true));
        }

        TEST_METHOD (DifferentExecutableDoesNotMatch)
        {
            auto task = PowerToysTask();
            task.executablePath = L"C:\\Users\\Someone\\AppData\\Local\\PowerToys\\PowerToys.exe";
            Assert::IsFalse(auto_start_task::matches(task, Executable, true));

            task.executablePath = L"";
            Assert::IsFalse(auto_start_task::matches(task, Executable, true));
        }

        TEST_METHOD (RunLevelChangeDoesNotMatch)
        {
            auto task = PowerToysTask();
            Assert::IsFalse(auto_start_task::matches(task, Executable, false));

            task.runElevated = false;
            Assert::IsFalse(auto_start_task::matches(task, Executable, true));
            Assert::IsTrue(auto_start_task::matches(task, Executable, false));
        }

        TEST_METHOD (TaskWithoutLogonTriggerDoesNotMatch)
        {
            auto task = PowerToysTask();
            task.logonTriggerDelay.reset();
            Assert::IsFalse(auto_start_task::matches(task, Executable, true));
        }

        TEST_METHOD (DescriptorRegisteredByPowerToysIsTrusted)
        {
            Assert::IsTrue(auto_start_task::grants_access_only_to(ExpectedDescriptor, ExpectedDescriptor));
            Assert::IsTrue(auto_start_task::grants_access_only_to(RegisteredDescriptor, ExpectedDescriptor));
        }

        TEST_METHOD (LegacyEveryoneDescriptorIsNotTrusted)
        {
            const std::wstring legacy = L"D:(A;;FA;;;WD)(A;ID;0x1f019f;;;BA)(A;ID;0x1f019f;;;SY)(A;ID;FA;;;" + UserSid + L")(A;;FR;;;" + UserSid + L")";
            Assert::IsFalse(auto_start_task::grants_access_only_to(legacy, ExpectedDescriptor));
        }

        TEST_METHOD (DescriptorGrantingAnotherPrincipalIsNotTrusted)
        {
            Assert::IsFalse(auto_start_task::grants_access_only_to(RegisteredDescriptor + L"(A;;FA;;;" + OtherUserSid + L")", ExpectedDescriptor));
            Assert::IsFalse(auto_start_task::grants_access_only_to(RegisteredDescriptor + L"(A;;GW;;;AU)", ExpectedDescriptor));
        }

        TEST_METHOD (TaskOfAnotherAccountWithSameNameIsNotTrusted)
        {
            const std::wstring otherAccountExpected = L"D:(A;;FA;;;SY)(A;;FA;;;BA)(A;;FA;;;" + OtherUserSid + L")";
            Assert::IsFalse(auto_start_task::grants_access_only_to(RegisteredDescriptor, otherAccountExpected));
        }

        TEST_METHOD (InheritedAcesAreIgnored)
        {
            Assert::IsTrue(auto_start_task::grants_access_only_to(RegisteredDescriptor + L"(A;ID;FA;;;" + OtherUserSid + L")", ExpectedDescriptor));
        }

        TEST_METHOD (UnexpectedAceTypeIsNotTrusted)
        {
            Assert::IsFalse(auto_start_task::grants_access_only_to(RegisteredDescriptor + L"(D;;FA;;;" + OtherUserSid + L")", ExpectedDescriptor));
        }

        TEST_METHOD (MissingOrMalformedDaclIsNotTrusted)
        {
            Assert::IsFalse(auto_start_task::grants_access_only_to(L"D:NO_ACCESS_CONTROL", ExpectedDescriptor));
            Assert::IsFalse(auto_start_task::grants_access_only_to(L"", ExpectedDescriptor));
            Assert::IsFalse(auto_start_task::grants_access_only_to(L"D:(A;;FA;;;", ExpectedDescriptor));
            Assert::IsFalse(auto_start_task::grants_access_only_to(RegisteredDescriptor, L""));
        }
    };
}
