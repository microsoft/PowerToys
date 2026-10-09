// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "CppUnitTest.h"

#include <barrier>

#include "../settings_window_startup.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace RunnerUnitTests
{
    TEST_CLASS(SettingsWindowStartupTests)
    {
    public:
        TEST_METHOD(RequestDuringModuleStartupWaitsAndPreservesLatestPage)
        {
            SettingsWindowStartup startup;
            Assert::IsTrue(startup.DeferOrCancel(L"FancyZones"));
            Assert::IsTrue(startup.DeferOrCancel(L"Run"));

            const auto completion = startup.CompleteStartup();
            Assert::IsFalse(completion.canceled);
            Assert::IsTrue(completion.request.has_value());
            Assert::AreEqual(std::wstring(L"Run"), completion.request->page.value());
            Assert::IsFalse(startup.CompleteStartup().request.has_value());
        }

        TEST_METHOD(DashboardRequestIsNotMistakenForNoRequest)
        {
            SettingsWindowStartup startup;
            Assert::IsTrue(startup.DeferOrCancel(std::nullopt));

            const auto completion = startup.CompleteStartup();
            Assert::IsTrue(completion.request.has_value());
            Assert::IsFalse(completion.request->page.has_value());
        }

        TEST_METHOD(CommandLineRequestIsUsedWhenNoNewerRequestArrives)
        {
            SettingsWindowStartup startup;
            const auto completion = startup.CompleteStartup(true, L"Run");

            Assert::IsFalse(completion.canceled);
            Assert::IsTrue(completion.request.has_value());
            Assert::AreEqual(std::wstring(L"Run"), completion.request->page.value());
        }

        TEST_METHOD(DeferredRequestTakesPrecedenceOverCommandLineAndAutomaticOnboarding)
        {
            SettingsWindowStartup startup;
            startup.DeferOrCancel(L"FancyZones");

            const auto completion = startup.CompleteStartup(true, L"Run");
            Assert::IsFalse(completion.canceled);
            // A requested normal page is selected instead of automatic OOBE/SCOOBE.
            Assert::IsTrue(completion.request.has_value());
            Assert::AreEqual(std::wstring(L"FancyZones"), completion.request->page.value());
        }

        TEST_METHOD(NoNormalRequestLeavesAutomaticOnboardingAvailable)
        {
            SettingsWindowStartup startup;
            const auto completion = startup.CompleteStartup();

            Assert::IsFalse(completion.canceled);
            Assert::IsFalse(completion.request.has_value());
        }

        TEST_METHOD(RequestsAfterStartupUseExistingWindowBehavior)
        {
            SettingsWindowStartup startup;
            startup.CompleteStartup();

            Assert::IsFalse(startup.DeferOrCancel(L"Run"));
            Assert::IsFalse(startup.DeferOrCancel(std::nullopt));
            Assert::IsFalse(startup.CompleteStartup().request.has_value());
        }

        TEST_METHOD(StopCancelsPendingCommandLineAndAutomaticStartupWindows)
        {
            SettingsWindowStartup startup;
            startup.DeferOrCancel(L"Run");
            startup.Stop();

            const auto completion = startup.CompleteStartup(true, L"FancyZones");
            Assert::IsTrue(completion.canceled);
            Assert::IsFalse(completion.request.has_value());
            Assert::IsTrue(startup.CompleteStartup().canceled);
            Assert::IsTrue(startup.DeferOrCancel(L"General"));
        }

        TEST_METHOD(RequestConcurrentWithStartupCompletionIsHandledExactlyOnce)
        {
            SettingsWindowStartup startup;
            bool deferred = false;
            SettingsWindowStartup::Completion completion;
            std::barrier<> ready(3);
            std::thread requester([&]() {
                ready.arrive_and_wait();
                deferred = startup.DeferOrCancel(L"Run");
            });
            std::thread initializer([&]() {
                ready.arrive_and_wait();
                completion = startup.CompleteStartup();
            });

            ready.arrive_and_wait();
            requester.join();
            initializer.join();

            Assert::IsFalse(completion.canceled);
            Assert::AreEqual(deferred, completion.request.has_value());
            if (completion.request)
            {
                Assert::AreEqual(std::wstring(L"Run"), completion.request->page.value());
            }
        }
    };
}
