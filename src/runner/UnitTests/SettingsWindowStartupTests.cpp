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

            AssertAction(SettingsWindowStartup::Action::None, startup.Open(L"FancyZones"));
            AssertAction(SettingsWindowStartup::Action::None, startup.Open(L"Run"));

            const auto request = startup.CompleteStartup();
            AssertAction(SettingsWindowStartup::Action::Launch, request);
            Assert::AreEqual(std::wstring(L"Run"), request.page.value());
            AssertAction(SettingsWindowStartup::Action::None, startup.CompleteStartup());
        }

        TEST_METHOD(DashboardRequestIsNotMistakenForNoRequest)
        {
            SettingsWindowStartup startup;
            AssertAction(SettingsWindowStartup::Action::None, startup.Open(std::nullopt));

            const auto request = startup.CompleteStartup();
            AssertAction(SettingsWindowStartup::Action::Launch, request);
            Assert::IsFalse(request.page.has_value());
        }

        TEST_METHOD(RequestWhileSettingsLaunchesIsShownAfterIpcIsReady)
        {
            SettingsWindowStartup startup;
            startup.CompleteStartup();
            AssertAction(SettingsWindowStartup::Action::Launch, startup.Open(L"FancyZones"));
            AssertAction(SettingsWindowStartup::Action::None, startup.Open(L"Run"));
            AssertAction(SettingsWindowStartup::Action::None, startup.Open(std::nullopt));

            startup.ProcessCreated(123);
            const auto request = startup.LaunchCompleted(123);
            AssertAction(SettingsWindowStartup::Action::Show, request);
            Assert::IsFalse(request.page.has_value());
            AssertAction(SettingsWindowStartup::Action::Show, startup.Open(L"General"));
        }

        TEST_METHOD(StartupRequestReusesOnboardingProcess)
        {
            SettingsWindowStartup startup;
            startup.Open(L"Run");
            Assert::IsTrue(startup.BeginOnboardingLaunch());
            Assert::IsFalse(startup.BeginOnboardingLaunch());
            AssertAction(SettingsWindowStartup::Action::None, startup.CompleteStartup());

            startup.ProcessCreated(123);
            const auto request = startup.LaunchCompleted(123);
            AssertAction(SettingsWindowStartup::Action::Show, request);
            Assert::AreEqual(std::wstring(L"Run"), request.page.value());
        }

        TEST_METHOD(FailedLaunchDoesNotLoseNewerRequestOrLeaveLaunchReserved)
        {
            SettingsWindowStartup startup;
            startup.CompleteStartup();
            startup.Open(L"FancyZones");
            startup.Open(L"Run");

            const auto retry = startup.Closed(0);
            AssertAction(SettingsWindowStartup::Action::Launch, retry);
            Assert::AreEqual(std::wstring(L"Run"), retry.page.value());
            AssertAction(SettingsWindowStartup::Action::None, startup.Closed(0));
            AssertAction(SettingsWindowStartup::Action::Launch, startup.Open(std::nullopt));
        }

        TEST_METHOD(RequestConcurrentWithStartupCompletionLaunchesExactlyOnce)
        {
            SettingsWindowStartup startup;
            SettingsWindowStartup::Request openRequest;
            SettingsWindowStartup::Request readyRequest;
            std::barrier<> ready(3);
            std::thread requester([&]() {
                ready.arrive_and_wait();
                openRequest = startup.Open(L"Run");
            });
            std::thread initializer([&]() {
                ready.arrive_and_wait();
                readyRequest = startup.CompleteStartup();
            });

            ready.arrive_and_wait();
            requester.join();
            initializer.join();

            const auto launches = static_cast<int>(openRequest.action == SettingsWindowStartup::Action::Launch) +
                                  static_cast<int>(readyRequest.action == SettingsWindowStartup::Action::Launch);
            Assert::AreEqual(1, launches);
            const auto& launched = openRequest.action == SettingsWindowStartup::Action::Launch ? openRequest : readyRequest;
            Assert::AreEqual(std::wstring(L"Run"), launched.page.value());
        }

        TEST_METHOD(ExitDuringModuleStartupCancelsDeferredRequests)
        {
            SettingsWindowStartup startup;
            startup.Open(L"Run");
            startup.Stop();

            AssertAction(SettingsWindowStartup::Action::None, startup.CompleteStartup());
            AssertAction(SettingsWindowStartup::Action::None, startup.LaunchCompleted(123));
            AssertAction(SettingsWindowStartup::Action::None, startup.Closed(0));
            AssertAction(SettingsWindowStartup::Action::None, startup.Open(L"FancyZones"));
            Assert::IsFalse(startup.BeginOnboardingLaunch());
        }

        TEST_METHOD(ReadinessQueuedBeforeProcessExitCannotPreventReopening)
        {
            SettingsWindowStartup startup;
            startup.CompleteStartup();
            startup.Open(L"Run");
            startup.ProcessCreated(123);
            startup.Closed(123);

            AssertAction(SettingsWindowStartup::Action::None, startup.LaunchCompleted(123));
            AssertAction(SettingsWindowStartup::Action::Launch, startup.Open(L"FancyZones"));
        }

        TEST_METHOD(OldProcessCallbacksCannotCompleteOrCloseNewerLaunch)
        {
            SettingsWindowStartup startup;
            startup.CompleteStartup();
            startup.Open(L"Run");
            startup.ProcessCreated(123);
            startup.Open(L"FancyZones");
            AssertAction(SettingsWindowStartup::Action::Launch, startup.Closed(123));
            startup.ProcessCreated(456);

            AssertAction(SettingsWindowStartup::Action::None, startup.LaunchCompleted(123));
            AssertAction(SettingsWindowStartup::Action::None, startup.Closed(123));
            AssertAction(SettingsWindowStartup::Action::None, startup.Closed(0));
            AssertAction(SettingsWindowStartup::Action::None, startup.Open(L"General"));

            const auto request = startup.LaunchCompleted(456);
            AssertAction(SettingsWindowStartup::Action::Show, request);
            Assert::AreEqual(std::wstring(L"General"), request.page.value());
        }

    private:
        static void AssertAction(SettingsWindowStartup::Action expected, const SettingsWindowStartup::Request& request)
        {
            Assert::AreEqual(static_cast<int>(expected), static_cast<int>(request.action));
        }
    };
}
