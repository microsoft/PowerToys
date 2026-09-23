// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include "pch.h"

#include <appmodel.h>
#include <array>
#include <memory>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace
{
    constexpr wchar_t test_package_family[] = L"PowerToys.ContextMenuLifecycle.Tests_publisher";
    constexpr wchar_t test_package_prefix[] = L"PowerToys.ContextMenuLifecycle.Tests_";
    constexpr wchar_t test_window_class[] = L"PowerToys.ContextMenuLifecycle.Tests.ServicingWindow";
    constexpr DWORD test_timeout_ms = 100;
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
        std::atomic_bool packaged = true;
        std::atomic_bool reject_creation = false;
        std::atomic<DWORD> hook_error = ERROR_SUCCESS;
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
}

// Only package identity and the thread entry are substituted. Window creation,
// event signalling, waits and synchronization execute the production Win32 code.
#define GetCurrentPackageFamilyName test_get_package_family_name
#define CreateThread test_create_thread
#include <context_menu_lifecycle.h>
#undef CreateThread
#undef GetCurrentPackageFamilyName

namespace
{
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

        HRESULT ensure(DWORD timeout_ms = test_timeout_ms)
        {
            const context_menu_lifecycle::details::initialization_parameters parameters{
                &module_anchor,
                test_package_prefix,
                test_window_class,
                1000,
                nullptr
            };
            return context_menu_lifecycle::details::ensure_servicing_window(parameters, timeout_ms);
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
                // WM_CLOSE deliberately terminates the host process. WM_QUIT
                // exercises the monitor's normal thread/resource cleanup instead.
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

        TEST_METHOD (UnpackagedHostSkipsInitialization)
        {
            lifecycle_fixture fixture;
            fixture.set_unpackaged();
            Assert::AreEqual(S_FALSE, fixture.ensure());
            Assert::AreEqual(S_FALSE, fixture.ensure());
            Assert::AreEqual(size_t{ 0 }, fixture.thread_count());

            context_menu_lifecycle::activity_guard activity;
            Assert::IsTrue(static_cast<bool>(activity));
            Assert::AreEqual(uint64_t{ 0 }, context_menu_lifecycle::details::state().activity_state.load());
            fixture.finish();
        }
    };
}
