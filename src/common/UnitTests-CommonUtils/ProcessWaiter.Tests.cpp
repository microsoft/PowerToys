#include "pch.h"
#include "TestHelpers.h"
#include <ProcessWaiter.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace UnitTestsCommonUtils
{
    TEST_CLASS(ProcessWaiterTests)
    {
    public:
        TEST_METHOD(OnProcessTerminate_InvalidPid_DoesNotCrash)
        {
            std::atomic<bool> called{ false };

            // Use a very unlikely PID (negative value as string will fail conversion)
            OnProcessTerminate(L"invalid", [&called](DWORD) {
                called = true;
            });

            // Wait briefly
            std::this_thread::sleep_for(std::chrono::milliseconds(100));

            // Should not crash, callback may or may not be called depending on implementation
            Assert::IsTrue(true);
        }

        TEST_METHOD(OnProcessTerminate_NonExistentPid_DoesNotCrash)
        {
            std::atomic<bool> called{ false };

            // Use a PID that likely doesn't exist
            OnProcessTerminate(L"999999999", [&called](DWORD) {
                called = true;
            });

            // Wait briefly
            std::this_thread::sleep_for(std::chrono::milliseconds(100));

            // Should not crash
            Assert::IsTrue(true);
        }

        TEST_METHOD(OnProcessTerminate_ZeroPid_DoesNotCrash)
        {
            std::atomic<bool> called{ false };

            OnProcessTerminate(L"0", [&called](DWORD) {
                called = true;
            });

            std::this_thread::sleep_for(std::chrono::milliseconds(100));
            Assert::IsTrue(true);
        }

        TEST_METHOD(OnProcessTerminate_CurrentProcessPid_DoesNotTerminate)
        {
            std::atomic<bool> called{ false };

            // Use current process PID - it shouldn't terminate during test
            std::wstring pid = std::to_wstring(GetCurrentProcessId());

            OnProcessTerminate(pid, [&called](DWORD) {
                called = true;
            });

            // Wait briefly - current process should not terminate
            std::this_thread::sleep_for(std::chrono::milliseconds(200));

            // Callback should not have been called since process is still running
            Assert::IsFalse(called);
        }

        TEST_METHOD(OnProcessTerminate_EmptyCallback_DoesNotCrash)
        {
            // Test with an empty function
            OnProcessTerminate(L"999999999", std::function<void(DWORD)>());

            std::this_thread::sleep_for(std::chrono::milliseconds(100));
            Assert::IsTrue(true);
        }

        TEST_METHOD(OnProcessTerminate_MultipleCallsForSamePid_DoesNotCrash)
        {
            std::atomic<int> counter{ 0 };
            std::wstring pid = std::to_wstring(GetCurrentProcessId());

            // Multiple waits on same (running) process
            for (int i = 0; i < 5; ++i)
            {
                OnProcessTerminate(pid, [&counter](DWORD) {
                    counter++;
                });
            }

            std::this_thread::sleep_for(std::chrono::milliseconds(200));

            // None should have been called since process is running
            Assert::AreEqual(0, counter.load());
        }

        TEST_METHOD(OnProcessTerminate_NegativeNumberString_DoesNotCrash)
        {
            std::atomic<bool> called{ false };

            OnProcessTerminate(L"-1", [&called](DWORD) {
                called = true;
            });

            std::this_thread::sleep_for(std::chrono::milliseconds(100));
            Assert::IsTrue(true);
        }

        TEST_METHOD(OnProcessTerminate_LargeNumber_DoesNotCrash)
        {
            std::atomic<bool> called{ false };

            OnProcessTerminate(L"18446744073709551615", [&called](DWORD) {
                called = true;
            });

            std::this_thread::sleep_for(std::chrono::milliseconds(100));
            Assert::IsTrue(true);
        }
    };
}
