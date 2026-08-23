#pragma once

#include "ProtectedRuntimeControlProtocol.h"

#include <windows.h>

#include <stdexcept>
#include <string>
#include <string_view>

namespace powertoys::protected_runtime
{
    using control_command = protocol::control_command;

    struct control_reply
    {
        DWORD win32_status{};
        DWORD scm_state{};
        DWORD process_id{};
        DWORD lease_count{};
        std::wstring runtime_version;
        std::wstring active_engine_version;
        std::wstring detail;
    };

    class control_error : public std::runtime_error
    {
    public:
        control_error(const char* operation, DWORD error);
        [[nodiscard]] DWORD code() const noexcept;

    private:
        DWORD m_code;
    };

    [[nodiscard]] bool valid_release_id(std::wstring_view value) noexcept;
    [[nodiscard]] control_reply invoke(
        control_command command,
        std::wstring_view release_id = {});

#ifdef POWERTOYS_PROTECTED_RUNTIME_TEST_HOOKS
    struct pipe_inspection
    {
        DWORD maximum_instances{};
        ACCESS_MASK authenticated_users_rights{};
    };

    using pipe_inspection_callback = void (*)(
        const pipe_inspection& inspection,
        void* context);

    [[nodiscard]] control_reply invoke_with_test_hold(
        control_command command,
        std::wstring_view release_id,
        DWORD hold_milliseconds,
        bool hold_before_preface,
        pipe_inspection_callback callback,
        void* callback_context);
#endif
}
