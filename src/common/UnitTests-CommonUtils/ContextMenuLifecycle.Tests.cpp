// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include "pch.h"

#include <appmodel.h>
#include <array>
#include <memory>
#include <optional>
#include <utility>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace
{
    constexpr wchar_t test_package_family[] = L"PowerToys.ContextMenuLifecycle.Tests_publisher";
    constexpr wchar_t test_package_prefix[] = L"PowerToys.ContextMenuLifecycle.Tests_";
    constexpr wchar_t test_window_class[] = L"PowerToys.ContextMenuLifecycle.Tests.ServicingWindow";
    constexpr DWORD test_timeout_ms = 100;
    constexpr DWORD drain_timeout_ms = 2000;
    constexpr DWORD shutdown_grace_ms = 5000;
    constexpr DWORD watchdog_ms = 10000;
    int module_anchor = 0;

    struct monitor_thread_record
    {
        winrt::handle handle;
        DWORD id = 0;
    };

    struct test_control
    {
        winrt::handle creation_entered{ CreateEventW(nullptr, TRUE, FALSE, nullptr) };
        winrt::handle allow_creation{ CreateEventW(nullptr, TRUE, TRUE, nullptr) };
        winrt::handle termination_called{ CreateEventW(nullptr, TRUE, FALSE, nullptr) };
        std::atomic_bool packaged = true;
        std::atomic_bool reject_creation = false;
        std::atomic<DWORD> hook_error = ERROR_SUCCESS;
        std::atomic<HWND> window = nullptr;
        std::atomic<HANDLE> terminated_process = nullptr;
        std::atomic<UINT> termination_exit_code = ERROR_GEN_FAILURE;
        std::atomic_uint32_t termination_count = 0;
        std::atomic_uint64_t activity_at_termination = 0;
        std::atomic<ULONGLONG> termination_tick = 0;
        std::mutex threads_lock;
        std::vector<monitor_thread_record> threads;
    };

    std::mutex fixture_lock;
    test_control* current_control = nullptr;
    thread_local test_control* thread_control = nullptr;
    thread_local HHOOK creation_hook = nullptr;

    LONG WINAPI test_get_package_family_name(UINT32* length, PWSTR name)
    {
        if (!current_control->packaged.load())
        {
            return APPMODEL_ERROR_NO_PACKAGE;
        }

        constexpr UINT32 required_length = ARRAYSIZE(test_package_family);
        if (*length < required_length)
        {
            *length = required_length;
            return ERROR_INSUFFICIENT_BUFFER;
        }

        wcscpy_s(name, *length, test_package_family);
        *length = required_length;
        return ERROR_SUCCESS;
    }

    LRESULT CALLBACK creation_hook_proc(int code, WPARAM wparam, LPARAM lparam)
    {
        if (code != HCBT_CREATEWND)
        {
            return CallNextHookEx(creation_hook, code, wparam, lparam);
        }

        wchar_t class_name[ARRAYSIZE(test_window_class)]{};
        if (!GetClassNameW(reinterpret_cast<HWND>(wparam), class_name, ARRAYSIZE(class_name)) ||
            wcscmp(class_name, test_window_class) != 0)
        {
            return CallNextHookEx(creation_hook, code, wparam, lparam);
        }

        thread_control->window.store(reinterpret_cast<HWND>(wparam));
        SetEvent(thread_control->creation_entered.get());
        const DWORD wait_result = WaitForSingleObject(thread_control->allow_creation.get(), watchdog_ms);
        const bool reject = thread_control->reject_creation.load();
        UnhookWindowsHookEx(creation_hook);
        creation_hook = nullptr;
        if (wait_result != WAIT_OBJECT_0)
        {
            thread_control->hook_error.store(ERROR_TIMEOUT);
            SetLastError(ERROR_TIMEOUT);
            return 1;
        }

        // A CBT veto need not supply a last-error code. The helper must still
        // report a failed CreateWindowExW as failure when GetLastError() is zero.
        SetLastError(ERROR_SUCCESS);
        return reject ? 1 : 0;
    }

    struct thread_start
    {
        LPTHREAD_START_ROUTINE entry;
        void* parameter;
        test_control* control;
    };

    DWORD WINAPI test_monitor_thread(void* parameter)
    {
        const auto start = *static_cast<thread_start*>(parameter);
        delete static_cast<thread_start*>(parameter);
        thread_control = start.control;
        creation_hook = SetWindowsHookExW(WH_CBT, creation_hook_proc, nullptr, GetCurrentThreadId());
        if (!creation_hook)
        {
            thread_control->hook_error.store(GetLastError());
        }

        // monitor_thread exits with FreeLibraryAndExitThread, so the start
        // allocation is released before entering it and the hook removes itself.
        return start.entry(start.parameter);
    }

    HANDLE WINAPI test_create_thread(
        LPSECURITY_ATTRIBUTES attributes,
        SIZE_T stack_size,
        LPTHREAD_START_ROUTINE entry,
        LPVOID parameter,
        DWORD flags,
        LPDWORD thread_id)
    {
        auto start = std::make_unique<thread_start>(thread_start{ entry, parameter, current_control });
        DWORD id = 0;
        const HANDLE thread = CreateThread(attributes, stack_size, test_monitor_thread, start.get(), flags, &id);
        if (!thread)
        {
            return nullptr;
        }
        start.release();

        HANDLE retained = nullptr;
        if (!DuplicateHandle(GetCurrentProcess(), thread, GetCurrentProcess(), &retained, 0, FALSE, DUPLICATE_SAME_ACCESS))
        {
            current_control->hook_error.store(GetLastError());
        }

        {
            std::lock_guard lock(current_control->threads_lock);
            current_control->threads.push_back({ winrt::handle{ retained }, id });
        }
        if (thread_id)
        {
            *thread_id = id;
        }
        return thread;
    }

    BOOL WINAPI test_terminate_process(HANDLE process, UINT exit_code);
}

// Package identity, the thread entry and the final process termination are
// substituted. Window dispatch, activity admission, the drain loop and its grace
// deadline execute the production code. The termination shim observes the exit
// request and stops only the monitor thread; these tests cannot prove that the
// host process actually exits or that package servicing completes.
#define GetCurrentPackageFamilyName test_get_package_family_name
#define CreateThread test_create_thread
#define TerminateProcess test_terminate_process
#include <context_menu_lifecycle.h>
#undef TerminateProcess
#undef CreateThread
#undef GetCurrentPackageFamilyName

namespace
{
    BOOL WINAPI test_terminate_process(HANDLE process, UINT exit_code)
    {
        thread_control->terminated_process.store(process);
        thread_control->termination_exit_code.store(exit_code);
        thread_control->activity_at_termination.store(context_menu_lifecycle::details::state().activity_state.load());
        thread_control->termination_tick.store(GetTickCount64());
        thread_control->termination_count.fetch_add(1);
        SetEvent(thread_control->termination_called.get());
        PostQuitMessage(0);
        return TRUE;
    }

    class lifecycle_fixture
    {
    public:
        lifecycle_fixture() :
            lock(fixture_lock),
            control(std::make_unique<test_control>())
        {
            Assert::IsNull(current_control, L"A previous monitor thread failed to shut down.");
            Assert::IsNotNull(control->creation_entered.get());
            Assert::IsNotNull(control->allow_creation.get());
            Assert::IsNotNull(control->termination_called.get());
            current_control = control.get();
        }

        ~lifecycle_fixture()
        {
            cleanup();
        }

        lifecycle_fixture(const lifecycle_fixture&) = delete;
        lifecycle_fixture& operator=(const lifecycle_fixture&) = delete;

        void prepare_creation(bool block, bool reject)
        {
            ResetEvent(control->creation_entered.get());
            control->reject_creation.store(reject);
            if (block)
            {
                ResetEvent(control->allow_creation.get());
            }
            else
            {
                SetEvent(control->allow_creation.get());
            }
        }

        void release_creation()
        {
            SetEvent(control->allow_creation.get());
        }

        void wait_for_creation()
        {
            Assert::AreEqual(WAIT_OBJECT_0, WaitForSingleObject(control->creation_entered.get(), watchdog_ms));
            Assert::AreEqual(DWORD{ ERROR_SUCCESS }, control->hook_error.load());
        }

        void wait_for_thread(size_t index)
        {
            HANDLE thread = nullptr;
            {
                std::lock_guard threads_lock(control->threads_lock);
                Assert::IsTrue(index < control->threads.size());
                thread = control->threads[index].handle.get();
            }
            Assert::AreEqual(WAIT_OBJECT_0, WaitForSingleObject(thread, watchdog_ms));
        }

        size_t thread_count()
        {
            std::lock_guard threads_lock(control->threads_lock);
            return control->threads.size();
        }

        HRESULT ensure(DWORD timeout_ms = test_timeout_ms, DWORD grace_ms = shutdown_grace_ms)
        {
            const context_menu_lifecycle::details::initialization_parameters parameters{
                &module_anchor,
                test_package_prefix,
                test_window_class,
                grace_ms,
                nullptr
            };
            return context_menu_lifecycle::details::ensure_servicing_window(parameters, timeout_ms);
        }

        ULONGLONG request_shutdown(UINT message = WM_CLOSE)
        {
            const HWND window = control->window.load();
            Assert::IsTrue(IsWindow(window) != FALSE);
            const ULONGLONG requested_tick = GetTickCount64();
            Assert::IsTrue(PostMessageW(window, message, TRUE, 0) != FALSE);
            return requested_tick;
        }

        void wait_for_shutdown()
        {
            const ULONGLONG deadline = GetTickCount64() + watchdog_ms;
            while ((context_menu_lifecycle::details::state().activity_state.load() & context_menu_lifecycle::details::shutdown_requested_bit) == 0 &&
                   GetTickCount64() < deadline)
            {
                Sleep(1);
            }
            Assert::IsTrue((context_menu_lifecycle::details::state().activity_state.load() & context_menu_lifecycle::details::shutdown_requested_bit) != 0,
                           L"The window must process the shutdown request and close admission.");
        }

        LRESULT send_message(UINT message, WPARAM wparam)
        {
            const HWND window = control->window.load();
            Assert::IsTrue(IsWindow(window) != FALSE);
            DWORD_PTR result = 0;
            Assert::IsTrue(SendMessageTimeoutW(window, message, wparam, 0, SMTO_ABORTIFHUNG | SMTO_BLOCK, watchdog_ms, &result) != 0);
            return static_cast<LRESULT>(result);
        }

        void assert_not_terminated()
        {
            Assert::AreEqual(DWORD{ WAIT_TIMEOUT }, WaitForSingleObject(control->termination_called.get(), test_timeout_ms),
                             L"Shutdown must wait while accepted work remains within the grace period.");
        }

        ULONGLONG wait_for_termination(uint64_t remaining_activities, DWORD timeout_ms = drain_timeout_ms)
        {
            // Drained work must exit promptly instead of consuming the whole
            // grace period; the forced-expiry test supplies the longer watchdog.
            Assert::AreEqual(WAIT_OBJECT_0, WaitForSingleObject(control->termination_called.get(), timeout_ms));
            wait_for_thread(0);
            Assert::IsTrue(control->terminated_process.load() == GetCurrentProcess());
            Assert::AreEqual(UINT{ ERROR_SUCCESS }, control->termination_exit_code.load());
            Assert::AreEqual(uint32_t{ 1 }, control->termination_count.load());
            Assert::AreEqual(context_menu_lifecycle::details::shutdown_requested_bit | remaining_activities, control->activity_at_termination.load());
            return control->termination_tick.load();
        }

        void set_unpackaged()
        {
            control->packaged.store(false);
        }

        void finish()
        {
            const DWORD hook_error = control->hook_error.load();
            cleanup();
            Assert::IsTrue(cleanup_succeeded, L"Monitor threads must exit and release their initialization state.");
            Assert::AreEqual(DWORD{ ERROR_SUCCESS }, hook_error);
        }

    private:
        void cleanup()
        {
            if (cleaned_up)
            {
                return;
            }
            cleaned_up = true;
            release_creation();

            cleanup_succeeded = true;
            for (const auto& thread : control->threads)
            {
                // A failed assertion may leave the monitor in its drain loop.
                // Its bounded grace period completes before this watchdog;
                // WM_QUIT also handles tests that never requested shutdown.
                if (thread.handle && WaitForSingleObject(thread.handle.get(), 0) == WAIT_TIMEOUT)
                {
                    PostThreadMessageW(thread.id, WM_QUIT, 0, 0);
                }
                if (!thread.handle || WaitForSingleObject(thread.handle.get(), watchdog_ms) != WAIT_OBJECT_0)
                {
                    cleanup_succeeded = false;
                }
            }

            if (cleanup_succeeded)
            {
                auto& state = context_menu_lifecycle::details::state();
                AcquireSRWLockExclusive(&state.initialization_lock);
                cleanup_succeeded = state.monitor == nullptr;
                if (cleanup_succeeded)
                {
                    state.package_checked = false;
                    state.track_activity.store(false);
                    state.activity_state.store(0);
                }
                ReleaseSRWLockExclusive(&state.initialization_lock);
            }

            if (cleanup_succeeded)
            {
                current_control = nullptr;
            }
            else
            {
                // Preserve hook data if a failed test leaves a worker alive.
                // Subsequent fixtures fail rather than touching its state.
                control.release();
            }
        }

        std::unique_lock<std::mutex> lock;
        std::unique_ptr<test_control> control;
        bool cleaned_up = false;
        bool cleanup_succeeded = false;
    };

    void verify_continuation_drain(bool release_parent_first)
    {
        lifecycle_fixture fixture;
        Assert::AreEqual(S_OK, fixture.ensure(watchdog_ms));

        std::optional<context_menu_lifecycle::activity_guard> parent{ std::in_place };
        Assert::IsTrue(static_cast<bool>(*parent));
        fixture.request_shutdown();
        fixture.wait_for_shutdown();

        // The continuation belongs to the already accepted operation even
        // though the window has now closed admission for unrelated operations.
        auto continuation_source = parent->continue_activity();
        Assert::IsTrue(static_cast<bool>(continuation_source));
        std::optional<context_menu_lifecycle::activity_guard> continuation{ std::move(continuation_source) };
        Assert::IsFalse(static_cast<bool>(continuation_source), L"Moving activity ownership must leave an inert source.");
        Assert::IsFalse(static_cast<bool>(continuation_source.continue_activity()), L"A moved-from guard cannot admit work.");
        Assert::AreEqual(context_menu_lifecycle::details::shutdown_requested_bit | uint64_t{ 2 },
                         context_menu_lifecycle::details::state().activity_state.load());

        context_menu_lifecycle::activity_guard rejected;
        Assert::IsFalse(static_cast<bool>(rejected));
        Assert::IsFalse(static_cast<bool>(rejected.continue_activity()), L"A rejected operation cannot acquire continuation ownership.");
        fixture.assert_not_terminated();

        if (release_parent_first)
        {
            parent.reset();
        }
        else
        {
            continuation.reset();
        }
        Assert::AreEqual(context_menu_lifecycle::details::shutdown_requested_bit | uint64_t{ 1 },
                         context_menu_lifecycle::details::state().activity_state.load());
        fixture.assert_not_terminated();

        parent.reset();
        continuation.reset();
        fixture.wait_for_termination(0);
        fixture.finish();
    }
}

namespace UnitTestsCommonUtils
{
    TEST_CLASS (ContextMenuLifecycleTests)
    {
    public:
        TEST_METHOD (ImmediateWindowFailureCanBeRetried)
        {
            lifecycle_fixture fixture;
            fixture.prepare_creation(false, true);
            Assert::IsTrue(FAILED(fixture.ensure(watchdog_ms)), L"A CBT veto with no last error must not report success.");
            fixture.wait_for_creation();
            fixture.wait_for_thread(0);

            fixture.prepare_creation(false, false);
            Assert::AreEqual(S_OK, fixture.ensure(watchdog_ms));
            Assert::AreEqual(size_t{ 2 }, fixture.thread_count());
            fixture.finish();
        }

        TEST_METHOD (LateWindowFailureCanBeRetriedWithoutLosingActivity)
        {
            lifecycle_fixture fixture;
            fixture.prepare_creation(true, true);
            Assert::AreEqual(HRESULT_FROM_WIN32(ERROR_TIMEOUT), fixture.ensure());
            fixture.wait_for_creation();

            {
                context_menu_lifecycle::activity_guard activity;
                Assert::IsTrue(static_cast<bool>(activity));
                Assert::AreEqual(uint64_t{ 1 }, context_menu_lifecycle::details::state().activity_state.load());

                fixture.release_creation();
                fixture.wait_for_thread(0);
                fixture.prepare_creation(false, false);
                Assert::AreEqual(S_OK, fixture.ensure(watchdog_ms));
                Assert::AreEqual(size_t{ 2 }, fixture.thread_count());
                Assert::AreEqual(uint64_t{ 1 }, context_menu_lifecycle::details::state().activity_state.load(), L"Retrying initialization must retain operations started during the previous attempt.");
            }

            Assert::AreEqual(uint64_t{ 0 }, context_menu_lifecycle::details::state().activity_state.load());
            fixture.finish();
        }

        TEST_METHOD (LateWindowSuccessIsSharedByConcurrentCallers)
        {
            lifecycle_fixture fixture;
            fixture.prepare_creation(true, false);
            Assert::AreEqual(HRESULT_FROM_WIN32(ERROR_TIMEOUT), fixture.ensure());
            fixture.wait_for_creation();

            std::array<std::future<HRESULT>, 4> callers;
            for (auto& caller : callers)
            {
                caller = std::async(std::launch::async, [&fixture] { return fixture.ensure(); });
            }
            for (auto& caller : callers)
            {
                Assert::AreEqual(HRESULT_FROM_WIN32(ERROR_TIMEOUT), caller.get());
            }
            Assert::AreEqual(size_t{ 1 }, fixture.thread_count(), L"A pending attempt must not spawn another monitor.");

            {
                context_menu_lifecycle::activity_guard activity;
                for (auto& caller : callers)
                {
                    caller = std::async(std::launch::async, [&fixture] { return fixture.ensure(watchdog_ms); });
                }
                fixture.release_creation();
                for (auto& caller : callers)
                {
                    Assert::AreEqual(S_OK, caller.get());
                }
                Assert::AreEqual(uint64_t{ 1 }, context_menu_lifecycle::details::state().activity_state.load());
            }
            Assert::AreEqual(S_OK, fixture.ensure());
            Assert::AreEqual(size_t{ 1 }, fixture.thread_count());
            Assert::AreEqual(uint64_t{ 0 }, context_menu_lifecycle::details::state().activity_state.load());
            fixture.finish();
        }

        TEST_METHOD (CloseRejectsNewActivityAndWaitsForEveryAcceptedActivity)
        {
            lifecycle_fixture fixture;
            Assert::AreEqual(S_OK, fixture.ensure(watchdog_ms));
            std::optional<context_menu_lifecycle::activity_guard> first{ std::in_place };
            std::optional<context_menu_lifecycle::activity_guard> second{ std::in_place };
            Assert::IsTrue(static_cast<bool>(*first));
            Assert::IsTrue(static_cast<bool>(*second));

            fixture.request_shutdown();
            fixture.wait_for_shutdown();
            Assert::AreEqual(context_menu_lifecycle::details::shutdown_requested_bit | uint64_t{ 2 },
                             context_menu_lifecycle::details::state().activity_state.load());
            context_menu_lifecycle::activity_guard rejected;
            Assert::IsFalse(static_cast<bool>(rejected), L"Shutdown must reject new operations.");
            fixture.assert_not_terminated();

            first.reset();
            Assert::AreEqual(context_menu_lifecycle::details::shutdown_requested_bit | uint64_t{ 1 },
                             context_menu_lifecycle::details::state().activity_state.load());
            fixture.assert_not_terminated();

            second.reset();
            fixture.wait_for_termination(0);
            fixture.finish();
        }

        TEST_METHOD (CloseTerminatesAtGraceDeadlineWithActivityStillRunning)
        {
            lifecycle_fixture fixture;
            constexpr DWORD short_grace_ms = 250;
            Assert::AreEqual(S_OK, fixture.ensure(watchdog_ms, short_grace_ms));
            std::optional<context_menu_lifecycle::activity_guard> activity{ std::in_place };
            Assert::IsTrue(static_cast<bool>(*activity));

            const ULONGLONG requested_tick = fixture.request_shutdown();
            fixture.wait_for_shutdown();
            const ULONGLONG terminated_tick = fixture.wait_for_termination(1, watchdog_ms);
            Assert::IsTrue(terminated_tick - requested_tick >= short_grace_ms,
                           L"Active operations must receive the configured grace period before forced termination.");

            // The termination shim allows this guard to be released. A real
            // TerminateProcess call would end the host without draining it.
            activity.reset();
            fixture.finish();
        }

        TEST_METHOD (CancelledEndSessionKeepsAcceptingActivity)
        {
            lifecycle_fixture fixture;
            Assert::AreEqual(S_OK, fixture.ensure(watchdog_ms));
            Assert::AreEqual(LRESULT{ TRUE }, fixture.send_message(WM_QUERYENDSESSION, 0));
            Assert::AreEqual(LRESULT{ 0 }, fixture.send_message(WM_ENDSESSION, FALSE));

            {
                context_menu_lifecycle::activity_guard activity;
                Assert::IsTrue(static_cast<bool>(activity));
                Assert::AreEqual(uint64_t{ 1 }, context_menu_lifecycle::details::state().activity_state.load());
                fixture.assert_not_terminated();
            }
            Assert::AreEqual(uint64_t{ 0 }, context_menu_lifecycle::details::state().activity_state.load());
            fixture.finish();
        }

        TEST_METHOD (ConfirmedEndSessionWaitsForAcceptedActivity)
        {
            lifecycle_fixture fixture;
            Assert::AreEqual(S_OK, fixture.ensure(watchdog_ms));
            std::optional<context_menu_lifecycle::activity_guard> activity{ std::in_place };
            Assert::IsTrue(static_cast<bool>(*activity));

            fixture.request_shutdown(WM_ENDSESSION);
            fixture.wait_for_shutdown();
            context_menu_lifecycle::activity_guard rejected;
            Assert::IsFalse(static_cast<bool>(rejected));
            fixture.assert_not_terminated();

            activity.reset();
            fixture.wait_for_termination(0);
            fixture.finish();
        }

        TEST_METHOD (ContinuationAfterShutdownOutlivesItsParent)
        {
            verify_continuation_drain(true);
        }

        TEST_METHOD (ParentOutlivesItsContinuationAfterShutdown)
        {
            verify_continuation_drain(false);
        }

        TEST_METHOD (UnpackagedHostSkipsInitialization)
        {
            lifecycle_fixture fixture;
            fixture.set_unpackaged();
            Assert::AreEqual(S_FALSE, fixture.ensure());
            Assert::AreEqual(S_FALSE, fixture.ensure());
            Assert::AreEqual(size_t{ 0 }, fixture.thread_count());

            context_menu_lifecycle::activity_guard activity;
            Assert::IsTrue(static_cast<bool>(activity));
            auto continuation_source = activity.continue_activity();
            Assert::IsTrue(static_cast<bool>(continuation_source));
            context_menu_lifecycle::activity_guard continuation{ std::move(continuation_source) };
            Assert::IsTrue(static_cast<bool>(continuation));
            Assert::IsFalse(static_cast<bool>(continuation_source));
            Assert::IsFalse(static_cast<bool>(continuation_source.continue_activity()));
            Assert::AreEqual(uint64_t{ 0 }, context_menu_lifecycle::details::state().activity_state.load());
            fixture.finish();
        }
    };
}
