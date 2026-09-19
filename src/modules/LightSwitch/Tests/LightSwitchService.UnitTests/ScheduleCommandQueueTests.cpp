// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root.

#include <CppUnitTest.h>
#include "TestSupport.h"
#include "../../LightSwitchService/ScheduleCommandQueue.h"
#include <atomic>
#include <thread>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace winrt::Windows::Data::Json;
using namespace light_switch_cli;
using LightSwitchServiceUnitTests::Apartment;

namespace LightSwitchServiceTests
{
    TEST_CLASS (ScheduleCommandQueueTests)
    {
    public:
        TEST_METHOD (WorkerCompletesTheSubmittedRequest)
        {
            Apartment apartment;
            ScheduleCommandQueue queue;
            auto response = std::async(std::launch::async, [&]() {
                Apartment apartment;
                return queue.Submit({ Command::ScheduleEnable, L"FixedHours" });
            });
            Assert::AreEqual(static_cast<DWORD>(WAIT_OBJECT_0), WaitForSingleObject(queue.Event(), 5000));
            queue.ProcessPending([](const Request& request) {
                Assert::IsTrue(request.command == Command::ScheduleEnable);
                Assert::AreEqual(L"FixedHours", request.mode->c_str());
                return MakeSuccess(JsonObject{});
            });
            Assert::IsTrue(response.get().GetNamedBoolean(L"success"));
        }

        TEST_METHOD (TimedOutQueuedRequestIsNotExecuted)
        {
            Apartment apartment;
            ScheduleCommandQueue queue;
            const auto response = queue.Submit(
                { Command::ScheduleDisable, std::nullopt }, std::chrono::milliseconds(1));
            bool executed = false;
            queue.ProcessPending([&](const Request&) {
                executed = true;
                return MakeSuccess(JsonObject{});
            });
            Assert::IsFalse(executed);
            Assert::AreEqual(L"TIMEOUT", response.GetNamedObject(L"error").GetNamedString(L"code").c_str());
        }

        TEST_METHOD (StopReleasesWaitingCallerAndRejectsNewRequests)
        {
            Apartment apartment;
            ScheduleCommandQueue queue;
            auto response = std::async(std::launch::async, [&]() {
                Apartment apartment;
                return queue.Submit({ Command::ScheduleDisable, std::nullopt });
            });
            Assert::AreEqual(static_cast<DWORD>(WAIT_OBJECT_0), WaitForSingleObject(queue.Event(), 5000));
            queue.Stop();
            Assert::AreEqual(L"SERVICE_UNAVAILABLE", response.get().GetNamedObject(L"error").GetNamedString(L"code").c_str());
            Assert::IsFalse(queue.Submit({ Command::ScheduleDisable, std::nullopt }).GetNamedBoolean(L"success"));
        }

        TEST_METHOD (HandlerExceptionBecomesAnErrorResponse)
        {
            Apartment apartment;
            ScheduleCommandQueue queue;
            auto response = std::async(std::launch::async, [&]() {
                Apartment apartment;
                return queue.Submit({ Command::ScheduleDisable, std::nullopt });
            });
            Assert::AreEqual(static_cast<DWORD>(WAIT_OBJECT_0), WaitForSingleObject(queue.Event(), 5000));
            queue.ProcessPending([](const Request&) -> JsonObject {
                throw std::runtime_error("Fake schedule failure");
            });
            Assert::AreEqual(L"EXECUTION_FAILED", response.get().GetNamedObject(L"error").GetNamedString(L"code").c_str());
        }
    };
}
