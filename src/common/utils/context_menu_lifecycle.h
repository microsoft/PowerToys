#pragma once

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <appmodel.h>

#include <atomic>
#include <cstdint>
#include <cwchar>
#include <new>
#include <utility>

// Packaged COM surrogates do not own a top-level window, so package servicing has
// no WM_CLOSE target and eventually reports HANG_QUIESCE. This helper adds one
// process-lifetime window to a package-dedicated surrogate. An extra module
// reference keeps the window procedure valid after COM considers the server
// unloadable.
namespace context_menu_lifecycle
{
    using initialization_callback = void (*)(HRESULT) noexcept;

    struct activity_token
    {
        void* monitor = nullptr;
        bool accepted = true;

        explicit operator bool() const noexcept
        {
            return accepted;
        }
    };

    namespace details
    {
        struct monitor_state
        {
            HMODULE module = nullptr;
            PCWSTR window_class_name = nullptr;
            DWORD shutdown_grace_ms = 0;
            initialization_callback report_initialization = nullptr;
            // The worker and each waiting caller own a reference. A timeout
            // releases only that caller; the worker can still finish safely.
            std::atomic_uint32_t references = 1;
            HANDLE initialization_event = nullptr;
            std::atomic<HRESULT> initialization_result = E_PENDING;
            std::atomic_flag initialization_reported = ATOMIC_FLAG_INIT;
        };

        constexpr uint64_t shutdown_requested_bit = uint64_t{ 1 } << 63;
        constexpr uint64_t active_operations_mask = ~shutdown_requested_bit;

        struct initialization_parameters
        {
            const void* module_address = nullptr;
            PCWSTR package_family_name_prefix = nullptr;
            PCWSTR window_class_name = nullptr;
            DWORD shutdown_grace_ms = 0;
            initialization_callback report_initialization = nullptr;
        };

        struct lifecycle_state
        {
            SRWLOCK initialization_lock = SRWLOCK_INIT;
            bool package_checked = false;
            std::atomic_bool track_activity = false;
            // Tokens outlive individual initialization attempts. Never reset
            // this counter when a failed attempt is replaced.
            std::atomic_uint64_t activity_state = 0;
            // Protected by initialization_lock; kept alive by the worker.
            monitor_state* monitor = nullptr;
        };

        inline lifecycle_state& state()
        {
            static lifecycle_state value;
            return value;
        }

        class initialization_lock
        {
        public:
            initialization_lock() noexcept
            {
                AcquireSRWLockExclusive(&state().initialization_lock);
            }

            initialization_lock(const initialization_lock&) = delete;
            initialization_lock& operator=(const initialization_lock&) = delete;

            ~initialization_lock()
            {
                ReleaseSRWLockExclusive(&state().initialization_lock);
            }
        };

        inline void release_monitor(monitor_state* monitor) noexcept
        {
            if (monitor->references.fetch_sub(1, std::memory_order_acq_rel) == 1)
            {
                if (monitor->initialization_event)
                {
                    CloseHandle(monitor->initialization_event);
                }
                delete monitor;
            }
        }

        inline void report(monitor_state* monitor, HRESULT result) noexcept
        {
            if (!monitor->initialization_reported.test_and_set() && monitor->report_initialization)
            {
                monitor->report_initialization(result);
            }
        }

        inline HRESULT last_error_hresult() noexcept
        {
            const DWORD error = GetLastError();
            // A CBT hook can reject CreateWindowExW without setting LastError.
            return error == ERROR_SUCCESS ? E_FAIL : HRESULT_FROM_WIN32(error);
        }

        inline bool is_expected_package_host(PCWSTR package_family_name_prefix) noexcept
        {
            WCHAR package_family_name[PACKAGE_FAMILY_NAME_MAX_LENGTH + 1]{};
            UINT32 package_family_name_length = ARRAYSIZE(package_family_name);
            if (GetCurrentPackageFamilyName(&package_family_name_length, package_family_name) != ERROR_SUCCESS)
            {
                return false;
            }

            const size_t prefix_length = wcslen(package_family_name_prefix);
            return wcsncmp(package_family_name, package_family_name_prefix, prefix_length) == 0;
        }

        inline void terminate_after_active_operations(monitor_state* monitor) noexcept
        {
            auto& activity = state().activity_state;
            activity.fetch_or(shutdown_requested_bit, std::memory_order_acq_rel);
            const ULONGLONG deadline = GetTickCount64() + monitor->shutdown_grace_ms;
            while ((activity.load(std::memory_order_acquire) & active_operations_mask) != 0 &&
                   GetTickCount64() < deadline)
            {
                Sleep(50);
            }

            TerminateProcess(GetCurrentProcess(), ERROR_SUCCESS);
        }

        inline LRESULT CALLBACK window_proc(HWND window, UINT message, WPARAM wparam, LPARAM lparam)
        {
            if (message == WM_NCCREATE)
            {
                const auto create = reinterpret_cast<CREATESTRUCTW*>(lparam);
                SetWindowLongPtrW(window, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(create->lpCreateParams));
            }

            const auto state = reinterpret_cast<monitor_state*>(GetWindowLongPtrW(window, GWLP_USERDATA));
            switch (message)
            {
            case WM_QUERYENDSESSION:
                return TRUE;
            case WM_ENDSESSION:
                if (!wparam)
                {
                    return 0;
                }
                [[fallthrough]];
            case WM_CLOSE:
                if (state)
                {
                    terminate_after_active_operations(state);
                }
                else
                {
                    TerminateProcess(GetCurrentProcess(), ERROR_SUCCESS);
                }
                return 0;
            case WM_DESTROY:
                PostQuitMessage(0);
                return 0;
            default:
                return DefWindowProcW(window, message, wparam, lparam);
            }
        }

        inline DWORD WINAPI monitor_thread(void* parameter)
        {
            const auto monitor = static_cast<monitor_state*>(parameter);
            const HMODULE module = monitor->module;
            WNDCLASSW window_class{};
            window_class.lpfnWndProc = window_proc;
            window_class.hInstance = module;
            window_class.lpszClassName = monitor->window_class_name;

            const ATOM window_class_atom = RegisterClassW(&window_class);
            HRESULT result = S_OK;
            HWND window = nullptr;
            if (!window_class_atom && GetLastError() != ERROR_CLASS_ALREADY_EXISTS)
            {
                result = last_error_hresult();
            }
            else
            {
                window = CreateWindowExW(
                    WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW,
                    monitor->window_class_name,
                    L"",
                    WS_POPUP,
                    0,
                    0,
                    0,
                    0,
                    nullptr,
                    nullptr,
                    module,
                    monitor);
                if (!window)
                {
                    result = last_error_hresult();
                }
            }

            if (window)
            {
                monitor->initialization_result.store(S_OK, std::memory_order_release);
                SetEvent(monitor->initialization_event);
                report(monitor, S_OK);

                MSG message{};
                while (GetMessageW(&message, nullptr, 0, 0) > 0)
                {
                    TranslateMessage(&message);
                    DispatchMessageW(&message);
                }
                DestroyWindow(window);
            }

            if (window_class_atom)
            {
                UnregisterClassW(monitor->window_class_name, module);
            }
            {
                initialization_lock lock;
                // Remove the attempt only after its window/class are gone.
                // Existing waiters retain their own references and result.
                state().monitor = nullptr;
            }
            if (!window)
            {
                monitor->initialization_result.store(result, std::memory_order_release);
                SetEvent(monitor->initialization_event);
                report(monitor, result);
            }

            release_monitor(monitor);
            // The module reference belongs to this thread, independently of
            // the attempt's waiters. No DLL code may execute after releasing it.
            FreeLibraryAndExitThread(module, 0);
        }

        // Called with initialization_lock held. On success, the caller and the
        // worker each own one reference; only the worker owns the module pin.
        inline HRESULT start_monitor(const initialization_parameters& parameters, monitor_state*& monitor) noexcept
        {
            HMODULE module = nullptr;
            if (!GetModuleHandleExW(
                    GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS,
                    reinterpret_cast<LPCWSTR>(parameters.module_address),
                    &module))
            {
                return last_error_hresult();
            }

            monitor = new (std::nothrow) monitor_state{};
            if (!monitor)
            {
                FreeLibrary(module);
                return E_OUTOFMEMORY;
            }

            monitor->module = module;
            monitor->window_class_name = parameters.window_class_name;
            monitor->shutdown_grace_ms = parameters.shutdown_grace_ms;
            monitor->report_initialization = parameters.report_initialization;
            monitor->initialization_event = CreateEventW(nullptr, TRUE, FALSE, nullptr);
            if (!monitor->initialization_event)
            {
                const HRESULT result = last_error_hresult();
                FreeLibrary(module);
                release_monitor(monitor);
                monitor = nullptr;
                return result;
            }

            monitor->references.fetch_add(1, std::memory_order_relaxed);
            state().monitor = monitor;
            const HANDLE thread = CreateThread(nullptr, 0, monitor_thread, monitor, 0, nullptr);
            if (!thread)
            {
                const HRESULT result = last_error_hresult();
                state().monitor = nullptr;
                release_monitor(monitor);
                FreeLibrary(module);
                release_monitor(monitor);
                monitor = nullptr;
                return result;
            }
            CloseHandle(thread);
            return S_OK;
        }

        inline HRESULT ensure_servicing_window(const initialization_parameters& parameters, DWORD initialization_timeout_ms) noexcept
        {
            monitor_state* monitor = nullptr;
            HRESULT result = S_OK;
            {
                initialization_lock lock;
                auto& lifecycle = state();
                if (!lifecycle.package_checked)
                {
                    lifecycle.track_activity.store(is_expected_package_host(parameters.package_family_name_prefix), std::memory_order_release);
                    lifecycle.package_checked = true;
                }
                if (!lifecycle.track_activity.load(std::memory_order_acquire))
                {
                    return S_FALSE;
                }

                monitor = lifecycle.monitor;
                if (monitor)
                {
                    monitor->references.fetch_add(1, std::memory_order_relaxed);
                }
                else
                {
                    result = start_monitor(parameters, monitor);
                }
            }
            if (FAILED(result))
            {
                if (parameters.report_initialization)
                {
                    parameters.report_initialization(result);
                }
                return result;
            }

            // Do not hold initialization_lock while waiting. Concurrent
            // activations share this attempt, even after an earlier timeout.
            const DWORD wait_result = WaitForSingleObject(monitor->initialization_event, initialization_timeout_ms);
            if (wait_result == WAIT_OBJECT_0)
            {
                result = monitor->initialization_result.load(std::memory_order_acquire);
            }
            else
            {
                result = wait_result == WAIT_TIMEOUT ? HRESULT_FROM_WIN32(ERROR_TIMEOUT) : last_error_hresult();
                report(monitor, result);
            }
            release_monitor(monitor);
            return result;
        }
    }

    inline HRESULT ensure_servicing_window(
        const void* module_address,
        PCWSTR package_family_name_prefix,
        PCWSTR window_class_name,
        DWORD shutdown_grace_ms,
        initialization_callback report_initialization = nullptr) noexcept
    {
        // The string arguments are retained for the process lifetime and must
        // therefore point to static storage.
        details::initialization_parameters parameters{
            module_address,
            package_family_name_prefix,
            window_class_name,
            shutdown_grace_ms,
            report_initialization
        };

        return details::ensure_servicing_window(parameters, 5000);
    }

    inline activity_token begin_activity() noexcept
    {
        auto& lifecycle = details::state();
        if (lifecycle.track_activity.load(std::memory_order_acquire))
        {
            auto current_state = lifecycle.activity_state.load(std::memory_order_acquire);
            while ((current_state & details::shutdown_requested_bit) == 0)
            {
                if (lifecycle.activity_state.compare_exchange_weak(
                        current_state,
                        current_state + 1,
                        std::memory_order_acq_rel,
                        std::memory_order_acquire))
                {
                    return { &lifecycle.activity_state, true };
                }
            }

            return { nullptr, false };
        }

        return {};
    }

    inline void end_activity(activity_token token) noexcept
    {
        if (const auto activity = static_cast<std::atomic_uint64_t*>(token.monitor))
        {
            activity->fetch_sub(1, std::memory_order_acq_rel);
        }
    }

    class activity_guard
    {
    public:
        activity_guard() noexcept :
            token(begin_activity())
        {
        }

        activity_guard(const activity_guard&) = delete;
        activity_guard& operator=(const activity_guard&) = delete;

        activity_guard(activity_guard&& other) noexcept :
            token(std::exchange(other.token, activity_token{ nullptr, false }))
        {
        }

        // A continuation belongs to this already-admitted operation, so it may
        // outlive its parent even after shutdown closes admission. The live
        // parent keeps the count nonzero throughout this handoff.
        [[nodiscard]] activity_guard continue_activity() const noexcept
        {
            if (const auto activity = static_cast<std::atomic_uint64_t*>(token.monitor))
            {
                activity->fetch_add(1, std::memory_order_acq_rel);
            }
            return activity_guard(token);
        }

        explicit operator bool() const noexcept
        {
            return static_cast<bool>(token);
        }

        ~activity_guard()
        {
            end_activity(token);
        }

    private:
        explicit activity_guard(activity_token admitted_token) noexcept :
            token(admitted_token)
        {
        }

        activity_token token;
    };
}
