// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root.

#include <CppUnitTest.h>
#include "TestSupport.h"
#include "../../LightSwitchService/ScheduleCommandQueue.h"

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

        TEST_METHOD (TimedOutQueuedRequestsReleaseThePendingSlot)
        {
            Apartment apartment;
            ScheduleCommandQueue queue;
            for (int attempt = 0; attempt < 10; ++attempt)
            {
                const auto response = queue.Submit(
                    { Command::ScheduleDisable, std::nullopt }, std::chrono::milliseconds(1));
                Assert::AreEqual(L"TIMEOUT", response.GetNamedObject(L"error").GetNamedString(L"code").c_str());
            }
            Assert::AreEqual(static_cast<DWORD>(WAIT_OBJECT_0), WaitForSingleObject(queue.Event(), 0));
            bool executed = false;
            queue.ProcessPending([&](const Request&) {
                executed = true;
                return MakeSuccess(JsonObject{});
            });
            Assert::IsFalse(executed);

            auto response = std::async(std::launch::async, [&]() {
                Apartment apartment;
                return queue.Submit({ Command::ScheduleEnable, L"FixedHours" });
            });
            Assert::AreEqual(static_cast<DWORD>(WAIT_OBJECT_0), WaitForSingleObject(queue.Event(), 5000));
            queue.ProcessPending([](const Request&) { return MakeSuccess(JsonObject{}); });
            Assert::IsTrue(response.get().GetNamedBoolean(L"success"));
        }

        TEST_METHOD (StartedTimeoutDoesNotLoseTheFollowingRequest)
        {
            Apartment apartment;
            for (const bool stopWhileRunning : { false, true })
            {
                ScheduleCommandQueue queue;
                wil::unique_handle started(CreateEventW(nullptr, TRUE, FALSE, nullptr));
                wil::unique_handle release(CreateEventW(nullptr, TRUE, FALSE, nullptr));
                Assert::IsTrue(started && release);
                auto worker = std::async(std::launch::async, [&]() {
                    Apartment apartment;
                    if (WaitForSingleObject(queue.Event(), 5000) == WAIT_OBJECT_0)
                    {
                        queue.ProcessPending([&](const Request&) {
                            SetEvent(started.get());
                            WaitForSingleObject(release.get(), 5000);
                            return MakeSuccess(JsonObject{});
                        });
                    }
                });
                std::future<JsonObject> firstResponse;
                std::future<JsonObject> nextResponse;
                const auto cleanup = wil::scope_exit([&]() {
                    SetEvent(release.get());
                    queue.Stop();
                });
                firstResponse = std::async(std::launch::async, [&]() {
                    Apartment apartment;
                    return queue.Submit({ Command::ScheduleDisable, std::nullopt }, std::chrono::seconds(1));
                });
                Assert::AreEqual(static_cast<DWORD>(WAIT_OBJECT_0), WaitForSingleObject(started.get(), 5000));
                const auto error = firstResponse.get().GetNamedObject(L"error");
                Assert::AreEqual(L"TIMEOUT", error.GetNamedString(L"code").c_str());
                Assert::AreEqual(GET_RESOURCE_STRING(IDS_SCHEDULE_STARTED_TIMEOUT).c_str(), error.GetNamedString(L"message").c_str());

                nextResponse = std::async(std::launch::async, [&]() {
                    Apartment apartment;
                    return queue.Submit({ Command::ScheduleEnable, L"FixedHours" }, std::chrono::seconds(5));
                });
                Assert::AreEqual(static_cast<DWORD>(WAIT_OBJECT_0), WaitForSingleObject(queue.Event(), 5000));
                if (stopWhileRunning)
                {
                    queue.Stop();
                }
                SetEvent(release.get());
                worker.get();
                bool executed = false;
                queue.ProcessPending([&](const Request&) {
                    executed = true;
                    return MakeSuccess(JsonObject{});
                });
                const auto response = nextResponse.get();
                Assert::AreEqual(!stopWhileRunning, executed);
                Assert::AreEqual(!stopWhileRunning, response.GetNamedBoolean(L"success"));
                if (stopWhileRunning)
                {
                    Assert::AreEqual(L"SERVICE_UNAVAILABLE", response.GetNamedObject(L"error").GetNamedString(L"code").c_str());
                }
            }
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
