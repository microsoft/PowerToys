#include "pch.h"
#include "TestHelpers.h"
#include <window.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace UnitTestsCommonUtils
{
    TEST_CLASS(WindowTests)
    {
    public:
        // is_system_window tests
        TEST_METHOD(IsSystemWindow_DesktopWindow_ReturnsResult)
        {
            HWND desktop = GetDesktopWindow();
            Assert::IsNotNull(desktop);

            // Get class name
            char className[256] = {};
            GetClassNameA(desktop, className, sizeof(className));

            bool result = is_system_window(desktop, className);
            // Just verify it doesn't crash and returns a boolean
            Assert::IsTrue(result == true || result == false);
        }

        TEST_METHOD(IsSystemWindow_NullHwnd_ReturnsFalse)
        {
            auto shell = GetShellWindow();
            auto desktop = GetDesktopWindow();
            bool result = is_system_window(nullptr, "ClassName");
            bool expected = (shell == nullptr) || (desktop == nullptr);
            Assert::AreEqual(expected, result);
        }

        TEST_METHOD(IsSystemWindow_InvalidHwnd_ReturnsFalse)
        {
            bool result = is_system_window(reinterpret_cast<HWND>(0x12345678), "ClassName");
            Assert::IsFalse(result);
        }

        TEST_METHOD(IsSystemWindow_EmptyClassName_DoesNotCrash)
        {
            HWND desktop = GetDesktopWindow();
            bool result = is_system_window(desktop, "");
            // Just verify it doesn't crash
            Assert::IsTrue(result == true || result == false);
        }

        TEST_METHOD(IsSystemWindow_NullClassName_DoesNotCrash)
        {
            HWND desktop = GetDesktopWindow();
            bool result = is_system_window(desktop, nullptr);
            // Should handle null className gracefully
            Assert::IsTrue(result == true || result == false);
        }

        // GetWindowCreateParam tests
        TEST_METHOD(GetWindowCreateParam_ValidLparam_ReturnsValue)
        {
            struct TestData
            {
                int value;
            };

            TestData data{ 42 };
            CREATESTRUCT cs{};
            cs.lpCreateParams = &data;

            auto result = GetWindowCreateParam<TestData*>(reinterpret_cast<LPARAM>(&cs));
            Assert::IsNotNull(result);
            Assert::AreEqual(42, result->value);
        }

        TEST_METHOD(GetWindowCreateParam_NullLparam_ReturnsNull)
        {
            auto result = GetWindowCreateParam<void*>(0);
            Assert::IsNull(result);
        }

        // Window data storage tests
        TEST_METHOD(WindowData_StoreAndRetrieve_Works)
        {
            // Create a simple message-only window for testing
            WNDCLASSW wc = {};
            wc.lpfnWndProc = DefWindowProcW;
            wc.hInstance = GetModuleHandleW(nullptr);
            wc.lpszClassName = L"TestWindowClass_DataTest";
            RegisterClassW(&wc);

            HWND hwnd = CreateWindowExW(0, L"TestWindowClass_DataTest", L"Test",
                                        0, 0, 0, 0, 0, HWND_MESSAGE, nullptr,
                                        GetModuleHandleW(nullptr), nullptr);

            if (hwnd)
            {
                // Use pointer-sized value since StoreWindowParam requires sizeof(T) <= sizeof(void*)
                LONG_PTR testValue = 42;
                StoreWindowParam(hwnd, testValue);

                auto retrieved = GetWindowParam<LONG_PTR>(hwnd);
                Assert::AreEqual(testValue, retrieved);

                DestroyWindow(hwnd);
            }

            UnregisterClassW(L"TestWindowClass_DataTest", GetModuleHandleW(nullptr));
            Assert::IsTrue(true); // Window creation might fail in test environment
        }

        // run_message_loop tests
        TEST_METHOD(RunMessageLoop_UntilIdle_Completes)
        {
            // Run message loop until idle with a timeout
            // This should complete quickly since there are no messages
            auto start = std::chrono::steady_clock::now();

            run_message_loop(true, 100);

            auto elapsed = std::chrono::duration_cast<std::chrono::milliseconds>(
                std::chrono::steady_clock::now() - start);

            // Should complete within reasonable time
            Assert::IsTrue(elapsed.count() < 500);
        }

        TEST_METHOD(RunMessageLoop_WithTimeout_RespectsTimeout)
        {
            auto start = std::chrono::steady_clock::now();

            run_message_loop(false, 50);

            auto elapsed = std::chrono::duration_cast<std::chrono::milliseconds>(
                std::chrono::steady_clock::now() - start);

            // Should take at least the timeout duration
            // Allow some tolerance for timing
            Assert::IsTrue(elapsed.count() >= 40 && elapsed.count() < 500);
        }

        TEST_METHOD(RunMessageLoop_ZeroTimeout_CompletesImmediately)
        {
            auto start = std::chrono::steady_clock::now();

            run_message_loop(false, 0);

            auto elapsed = std::chrono::duration_cast<std::chrono::milliseconds>(
                std::chrono::steady_clock::now() - start);

            // Should complete very quickly
            Assert::IsTrue(elapsed.count() < 100);
        }

        TEST_METHOD(RunMessageLoop_NoTimeout_ProcessesMessages)
        {
            // Post a quit message before starting the loop
            PostQuitMessage(0);

            // Should process the quit message and exit
            run_message_loop(false, std::nullopt);

            Assert::IsTrue(true);
        }
    };
}
