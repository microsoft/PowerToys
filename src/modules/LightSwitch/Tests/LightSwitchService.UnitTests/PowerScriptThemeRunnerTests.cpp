// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <CppUnitTest.h>
#include "PowerScriptThemeRunner.h"
#include <future>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace std::chrono_literals;

namespace LightSwitchServiceUnitTests
{
    TEST_CLASS (PowerScriptThemeRunnerTests)
    {
    public:
        TEST_METHOD (InvocationIsAttributedUnattendedAndKeepsParametersAsLiteralArguments)
        {
            const LightSwitchScriptBinding binding{ L"theme-action", { { L"wallpaper", L"C:\\Pictures\\light image \"quoted\" & name.jpg" } } };
            const auto arguments = ThemeScriptArguments(binding);
            Assert::AreEqual(size_t{ 7 }, arguments.size());
            Assert::AreEqual(std::wstring(L"light-switch"), arguments[3]);
            Assert::AreEqual(std::wstring(L"--no-consent"), arguments[4]);
            Assert::AreEqual(std::wstring(L"wallpaper=C:\\Pictures\\light image \"quoted\" & name.jpg"), arguments[6]);
            Assert::IsTrue(ThemeScriptArguments({ L"\"bad-id", {} }).empty());
            Assert::IsTrue(ThemeScriptArguments({ L"theme-action", { { L"--consumer", L"command-line" } } }).empty());
        }

        TEST_METHOD (QueueIsNonblockingAndCoalescesPendingTransitions)
        {
            std::promise<void> started;
            auto startedFuture = started.get_future();
            std::promise<void> release;
            auto released = release.get_future().share();
            std::promise<std::wstring> last;
            auto lastFuture = last.get_future();
            PowerScriptThemeRunner runner([&](bool, const LightSwitchScriptBinding& binding, std::stop_token) {
                if (binding.id == L"first")
                {
                    started.set_value();
                    released.wait_for(5s);
                }
                else
                    last.set_value(binding.id);
            });
            runner.Queue(false, { L"first", {} });
            const auto ready = startedFuture.wait_for(5s);
            runner.Queue(true, { L"superseded", {} });
            runner.Queue(false, { L"latest", {} });
            release.set_value();
            Assert::IsTrue(ready == std::future_status::ready);
            Assert::IsTrue(lastFuture.wait_for(5s) == std::future_status::ready);
            Assert::AreEqual(std::wstring(L"latest"), lastFuture.get());
        }

        TEST_METHOD (ShutdownCancelsInFlightActionAndDropsPendingWork)
        {
            std::promise<void> started;
            auto startedFuture = started.get_future();
            std::promise<void> cancelled;
            auto cancelledFuture = cancelled.get_future();
            int runs = 0;
            {
                PowerScriptThemeRunner runner([&](bool, const LightSwitchScriptBinding&, std::stop_token stop) {
                    ++runs;
                    started.set_value();
                    std::mutex gate;
                    std::condition_variable_any changed;
                    std::unique_lock lock(gate);
                    changed.wait(lock, stop, [] { return false; });
                    cancelled.set_value();
                });
                runner.Queue(false, { L"first", {} });
                Assert::IsTrue(startedFuture.wait_for(5s) == std::future_status::ready);
                runner.Queue(true, { L"pending", {} });
            }
            Assert::IsTrue(cancelledFuture.wait_for(0s) == std::future_status::ready);
            Assert::AreEqual(1, runs);
        }
    };
}
