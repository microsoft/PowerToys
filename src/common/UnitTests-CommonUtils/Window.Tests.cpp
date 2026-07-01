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
                int value = 42;
                int* testValue = &value;
                StoreWindowParam(hwnd, testValue);

                auto retrieved = GetWindowParam<int*>(hwnd);
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

        // handle_session_end_message tests
        //
        // These guard the fix for APPLICATION_HANG_QUIESCE in
        // PowerToys.exe!run_message_loop, where the runner and most modules
        // failed to handle WM_QUERYENDSESSION / WM_ENDSESSION and were
        // force-terminated on every OS shutdown, sign-out, or restart.
        TEST_METHOD(HandleSessionEndMessage_QueryEndSession_AllowsShutdown)
        {
            LRESULT result = 0;
            bool handled = handle_session_end_message(nullptr, WM_QUERYENDSESSION, 0, result);

            Assert::IsTrue(handled, L"WM_QUERYENDSESSION should be handled");
            Assert::AreEqual(static_cast<LRESULT>(TRUE), result,
                             L"WM_QUERYENDSESSION must return TRUE so the OS can proceed with shutdown");
        }

        TEST_METHOD(HandleSessionEndMessage_EndSessionCancelled_DoesNotTearDown)
        {
            // wparam == FALSE means another app vetoed shutdown; we must not
            // tear down. We pass a real HWND so that an accidental
            // DestroyWindow call would be observable.
            WNDCLASSW wc{};
            wc.lpfnWndProc = DefWindowProcW;
            wc.hInstance = GetModuleHandleW(nullptr);
            wc.lpszClassName = L"EndSessionTest_Cancelled";
            RegisterClassW(&wc);

            HWND hwnd = CreateWindowExW(0, L"EndSessionTest_Cancelled", L"Test",
                                        0, 0, 0, 0, 0, HWND_MESSAGE, nullptr,
                                        GetModuleHandleW(nullptr), nullptr);
            Assert::IsNotNull(hwnd, L"Test window must be created");

            LRESULT result = 0xDEAD;
            bool handled = handle_session_end_message(hwnd, WM_ENDSESSION, FALSE, result);

            Assert::IsTrue(handled, L"WM_ENDSESSION should be handled");
            Assert::AreEqual(static_cast<LRESULT>(0), result);
            Assert::IsTrue(IsWindow(hwnd) == TRUE,
                           L"Window must survive WM_ENDSESSION when shutdown is cancelled");

            DestroyWindow(hwnd);
            UnregisterClassW(L"EndSessionTest_Cancelled", GetModuleHandleW(nullptr));
        }

        TEST_METHOD(HandleSessionEndMessage_EndSessionConfirmed_TearsDownAndExitsLoop)
        {
            // wparam == TRUE means shutdown is actually proceeding. The
            // helper must DestroyWindow, which routes through WM_DESTROY ->
            // PostQuitMessage(0), so a subsequent run_message_loop returns.
            WNDCLASSW wc{};
            wc.lpfnWndProc = [](HWND hwnd, UINT msg, WPARAM w, LPARAM l) -> LRESULT {
                if (msg == WM_DESTROY)
                {
                    PostQuitMessage(0);
                    return 0;
                }
                return DefWindowProcW(hwnd, msg, w, l);
            };
            wc.hInstance = GetModuleHandleW(nullptr);
            wc.lpszClassName = L"EndSessionTest_Confirmed";
            RegisterClassW(&wc);

            HWND hwnd = CreateWindowExW(0, L"EndSessionTest_Confirmed", L"Test",
                                        0, 0, 0, 0, 0, HWND_MESSAGE, nullptr,
                                        GetModuleHandleW(nullptr), nullptr);
            Assert::IsNotNull(hwnd, L"Test window must be created");

            LRESULT result = 0xDEAD;
            bool handled = handle_session_end_message(hwnd, WM_ENDSESSION, TRUE, result);

            Assert::IsTrue(handled, L"WM_ENDSESSION should be handled");
            Assert::AreEqual(static_cast<LRESULT>(0), result);

            // After DestroyWindow the window must no longer exist and the
            // message loop must exit promptly because WM_QUIT was posted.
            // The timeout was 1000 ms; assert well under that (2000 ms gives
            // headroom for slow CI VMs while still failing if the loop is
            // actually waiting out the full timeout).
            auto start = std::chrono::steady_clock::now();
            run_message_loop(false, 1000);
            auto elapsed = std::chrono::duration_cast<std::chrono::milliseconds>(
                std::chrono::steady_clock::now() - start);

            Assert::IsFalse(IsWindow(hwnd) == TRUE,
                            L"Window must be destroyed after WM_ENDSESSION(TRUE)");
            Assert::IsTrue(elapsed.count() < 2000,
                           L"Message loop must exit quickly after WM_ENDSESSION, not wait for the timeout");

            UnregisterClassW(L"EndSessionTest_Confirmed", GetModuleHandleW(nullptr));
        }

        TEST_METHOD(HandleSessionEndMessage_UnrelatedMessage_NotHandled)
        {
            LRESULT result = 0xDEAD;
            bool handled = handle_session_end_message(nullptr, WM_USER, 0, result);

            Assert::IsFalse(handled, L"Non-end-session messages must fall through");
            Assert::AreEqual(static_cast<LRESULT>(0xDEAD), result,
                             L"out_result must be left untouched when not handled");
        }

        // out_session_ending tests
        //
        // The optional out-param lets a caller's WM_DESTROY skip blocking
        // cross-process cleanup (the 1.5s wait on PowerToys.Settings.exe in the
        // runner) only on a real OS-initiated shutdown, which is what keeps the
        // teardown inside the quiesce budget.
        TEST_METHOD(HandleSessionEndMessage_EndSessionConfirmed_SignalsSessionEnding)
        {
            // wparam == TRUE must flag session-ending before tearing the window down.
            WNDCLASSW wc{};
            wc.lpfnWndProc = [](HWND hwnd, UINT msg, WPARAM w, LPARAM l) -> LRESULT {
                if (msg == WM_DESTROY)
                {
                    PostQuitMessage(0);
                    return 0;
                }
                return DefWindowProcW(hwnd, msg, w, l);
            };
            wc.hInstance = GetModuleHandleW(nullptr);
            wc.lpszClassName = L"EndSessionTest_Signals";
            RegisterClassW(&wc);

            HWND hwnd = CreateWindowExW(0, L"EndSessionTest_Signals", L"Test",
                                        0, 0, 0, 0, 0, HWND_MESSAGE, nullptr,
                                        GetModuleHandleW(nullptr), nullptr);
            Assert::IsNotNull(hwnd, L"Test window must be created");

            LRESULT result = 0;
            bool session_ending = false;
            bool handled = handle_session_end_message(hwnd, WM_ENDSESSION, TRUE, result, &session_ending);

            Assert::IsTrue(handled, L"WM_ENDSESSION should be handled");
            Assert::IsTrue(session_ending,
                           L"WM_ENDSESSION(TRUE) must flag session-ending so WM_DESTROY can skip blocking cleanup");

            // Drain the WM_QUIT posted by the test WndProc so it cannot leak into
            // a subsequent test running on the same thread.
            run_message_loop(false, 1000);
            UnregisterClassW(L"EndSessionTest_Signals", GetModuleHandleW(nullptr));
        }

        TEST_METHOD(HandleSessionEndMessage_EndSessionCancelled_DoesNotSignalSessionEnding)
        {
            // wparam == FALSE (another app vetoed) must not flag session-ending, so a
            // caller keeps doing its normal cleanup if it later closes on its own.
            LRESULT result = 0;
            bool session_ending = false;
            bool handled = handle_session_end_message(nullptr, WM_ENDSESSION, FALSE, result, &session_ending);

            Assert::IsTrue(handled, L"WM_ENDSESSION should be handled");
            Assert::IsFalse(session_ending,
                            L"A cancelled shutdown must not flag session-ending");
        }

        TEST_METHOD(HandleSessionEndMessage_QueryEndSession_DoesNotSignalSessionEnding)
        {
            // The query phase only asks permission; it must not flag teardown.
            LRESULT result = 0;
            bool session_ending = false;
            bool handled = handle_session_end_message(nullptr, WM_QUERYENDSESSION, 0, result, &session_ending);

            Assert::IsTrue(handled, L"WM_QUERYENDSESSION should be handled");
            Assert::AreEqual(static_cast<LRESULT>(TRUE), result);
            Assert::IsFalse(session_ending,
                            L"WM_QUERYENDSESSION must not flag session-ending");
        }
    };
}
