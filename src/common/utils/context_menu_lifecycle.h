#pragma once

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <appmodel.h>

#include <atomic>
#include <cwchar>
#include <new>

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
            std::atomic_uint64_t activity_state = 0;
            HANDLE initialization_event = nullptr;
            HRESULT initialization_result = E_UNEXPECTED;
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

        inline std::atomic<monitor_state*>& state()
        {
            static std::atomic<monitor_state*> value = nullptr;
            return value;
        }

        inline INIT_ONCE& initialization_once()
        {
            static INIT_ONCE value = INIT_ONCE_STATIC_INIT;
            return value;
        }

        inline HRESULT& initialization_result()
        {
            static HRESULT value = E_UNEXPECTED;
            return value;
        }

        inline void report(monitor_state* state, HRESULT result) noexcept
        {
            if (state->report_initialization)
            {
                state->report_initialization(result);
            }
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

        inline void terminate_after_active_operations(monitor_state* state) noexcept
        {
            state->activity_state.fetch_or(shutdown_requested_bit, std::memory_order_acq_rel);
            const ULONGLONG deadline = GetTickCount64() + state->shutdown_grace_ms;
            while ((state->activity_state.load(std::memory_order_acquire) & active_operations_mask) != 0 &&
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
            const auto state = static_cast<monitor_state*>(parameter);
            WNDCLASSW window_class{};
            window_class.lpfnWndProc = window_proc;
            window_class.hInstance = state->module;
            window_class.lpszClassName = state->window_class_name;

            const ATOM window_class_atom = RegisterClassW(&window_class);
            if (!window_class_atom && GetLastError() != ERROR_CLASS_ALREADY_EXISTS)
            {
                state->initialization_result = HRESULT_FROM_WIN32(GetLastError());
                SetEvent(state->initialization_event);
                return 0;
            }

            const HWND window = CreateWindowExW(
                WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW,
                state->window_class_name,
                L"",
                WS_POPUP,
                0,
                0,
                0,
                0,
                nullptr,
                nullptr,
                state->module,
                state);
            if (!window)
            {
                const DWORD error = GetLastError();
                if (window_class_atom)
                {
                    UnregisterClassW(state->window_class_name, state->module);
                }
                state->initialization_result = HRESULT_FROM_WIN32(error);
                SetEvent(state->initialization_event);
                return 0;
            }

            state->initialization_result = S_OK;
            SetEvent(state->initialization_event);

            MSG message{};
            while (GetMessageW(&message, nullptr, 0, 0) > 0)
            {
                TranslateMessage(&message);
                DispatchMessageW(&message);
            }

            return 0;
        }

        inline BOOL CALLBACK initialize(PINIT_ONCE, void* parameter, void**)
        {
            const auto parameters = static_cast<initialization_parameters*>(parameter);
            if (!is_expected_package_host(parameters->package_family_name_prefix))
            {
                initialization_result() = S_FALSE;
                return TRUE;
            }

            HMODULE module = nullptr;
            if (!GetModuleHandleExW(
                    GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS,
                    reinterpret_cast<LPCWSTR>(parameters->module_address),
                    &module))
            {
                initialization_result() = HRESULT_FROM_WIN32(GetLastError());
                if (parameters->report_initialization)
                {
                    parameters->report_initialization(initialization_result());
                }
                SetLastError(HRESULT_CODE(initialization_result()));
                return FALSE;
            }

            auto monitor = new (std::nothrow) monitor_state{};
            if (!monitor)
            {
                initialization_result() = E_OUTOFMEMORY;
                if (parameters->report_initialization)
                {
                    parameters->report_initialization(initialization_result());
                }
                FreeLibrary(module);
                SetLastError(ERROR_OUTOFMEMORY);
                return FALSE;
            }

            monitor->module = module;
            monitor->window_class_name = parameters->window_class_name;
            monitor->shutdown_grace_ms = parameters->shutdown_grace_ms;
            monitor->report_initialization = parameters->report_initialization;
            monitor->initialization_event = CreateEventW(nullptr, TRUE, FALSE, nullptr);
            if (!monitor->initialization_event)
            {
                initialization_result() = HRESULT_FROM_WIN32(GetLastError());
                report(monitor, initialization_result());
                FreeLibrary(module);
                delete monitor;
                SetLastError(HRESULT_CODE(initialization_result()));
                return FALSE;
            }

            const HANDLE thread = CreateThread(nullptr, 0, monitor_thread, monitor, 0, nullptr);
            if (!thread)
            {
                initialization_result() = HRESULT_FROM_WIN32(GetLastError());
                report(monitor, initialization_result());
                CloseHandle(monitor->initialization_event);
                FreeLibrary(module);
                delete monitor;
                SetLastError(HRESULT_CODE(initialization_result()));
                return FALSE;
            }

            const DWORD wait_result = WaitForSingleObject(monitor->initialization_event, 5000);
            if (wait_result != WAIT_OBJECT_0)
            {
                initialization_result() = wait_result == WAIT_TIMEOUT ? HRESULT_FROM_WIN32(ERROR_TIMEOUT) : HRESULT_FROM_WIN32(GetLastError());
                report(monitor, initialization_result());
                state().store(monitor, std::memory_order_release);
                CloseHandle(thread);
                return TRUE;
            }

            initialization_result() = monitor->initialization_result;
            CloseHandle(monitor->initialization_event);
            monitor->initialization_event = nullptr;
            if (FAILED(initialization_result()))
            {
                WaitForSingleObject(thread, INFINITE);
                CloseHandle(thread);
                report(monitor, initialization_result());
                FreeLibrary(module);
                delete monitor;
                SetLastError(HRESULT_CODE(initialization_result()));
                return FALSE;
            }

            state().store(monitor, std::memory_order_release);
            CloseHandle(thread);
            report(monitor, S_OK);
            return TRUE;
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

        const BOOL initialized = InitOnceExecuteOnce(
            &details::initialization_once(),
            details::initialize,
            &parameters,
            nullptr);
        if (!initialized)
        {
            return details::initialization_result();
        }

        return details::initialization_result();
    }

    inline activity_token begin_activity() noexcept
    {
        if (const auto monitor = details::state().load(std::memory_order_acquire))
        {
            auto current_state = monitor->activity_state.load(std::memory_order_acquire);
            while ((current_state & details::shutdown_requested_bit) == 0)
            {
                if (monitor->activity_state.compare_exchange_weak(
                        current_state,
                        current_state + 1,
                        std::memory_order_acq_rel,
                        std::memory_order_acquire))
                {
                    return { monitor, true };
                }
            }

            return { nullptr, false };
        }

        return {};
    }

    inline void end_activity(activity_token token) noexcept
    {
        if (const auto monitor = static_cast<details::monitor_state*>(token.monitor))
        {
            monitor->activity_state.fetch_sub(1, std::memory_order_acq_rel);
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

        explicit operator bool() const noexcept
        {
            return static_cast<bool>(token);
        }

        ~activity_guard()
        {
            end_activity(token);
        }

    private:
        activity_token token;
    };
}
