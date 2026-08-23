#include "../../../../common/protected_runtime/ProtectedRuntimeControlClient.h"

#include <windows.h>

#include <algorithm>
#include <iostream>
#include <string>
#include <string_view>

namespace
{
    int print_reply(const powertoys::protected_runtime::control_reply& reply)
    {
        std::wcout << L"win32=" << reply.win32_status << L"\n";
        std::wcout << L"scmState=" << reply.scm_state << L"\n";
        std::wcout << L"processId=" << reply.process_id << L"\n";
        std::wcout << L"leaseCount=" << reply.lease_count << L"\n";
        std::wcout << L"runtimeVersion=" << reply.runtime_version << L"\n";
        std::wcout << L"activeEngineVersion=" << reply.active_engine_version << L"\n";
        std::wstring detail(reply.detail);
        for (size_t index = 0; index < detail.size(); ++index)
        {
            if (detail[index] == L'\r' || detail[index] == L'\n')
            {
                const bool pair =
                    detail[index] == L'\r' &&
                    index + 1 < detail.size() &&
                    detail[index + 1] == L'\n';
                detail.replace(index, pair ? 2 : 1, L"\\n");
                ++index;
            }
        }
        std::wcout << L"detail=" << detail << L"\n";
        return reply.win32_status == ERROR_SUCCESS ?
            ERROR_SUCCESS :
            static_cast<int>(reply.win32_status);
    }

    [[nodiscard]] DWORD parse_test_hold(std::wstring_view value)
    {
        if (value.empty() ||
            value.size() > 5 ||
            !std::all_of(
                value.begin(),
                value.end(),
                [](wchar_t character) {
                    return character >= L'0' && character <= L'9';
                }))
        {
            throw powertoys::protected_runtime::control_error(
                "test pipe hold milliseconds",
                ERROR_INVALID_PARAMETER);
        }
        const auto milliseconds = std::stoul(std::wstring(value));
        if (milliseconds == 0 || milliseconds > 30000)
        {
            throw powertoys::protected_runtime::control_error(
                "test pipe hold milliseconds range",
                ERROR_INVALID_PARAMETER);
        }
        return milliseconds;
    }

    void print_pipe_inspection(
        const powertoys::protected_runtime::pipe_inspection& inspection,
        void*)
    {
        std::wcout << L"testPipeConnected=true\n";
        std::wcout << L"testPipeMaximumInstances=" <<
            inspection.maximum_instances << L"\n";
        std::wcout << L"testPipeAuthenticatedUsersRights=" <<
            inspection.authenticated_users_rights << L"\n";
        std::wcout << L"testPipeInspectionReady=true\n";
        std::wcout.flush();
    }
}

int wmain(int argc, wchar_t** argv)
{
    try
    {
        using namespace powertoys::protected_runtime;
        if (argc == 4 &&
            (std::wstring_view(argv[1]) == L"--acquire" ||
             std::wstring_view(argv[1]) == L"--ensure") &&
            std::wstring_view(argv[2]) == L"--release-id")
        {
            return print_reply(invoke(control_command::acquire, argv[3]));
        }
        if (argc == 2 && std::wstring_view(argv[1]) == L"--status")
        {
            return print_reply(invoke(control_command::status));
        }
        if (argc == 2 && std::wstring_view(argv[1]) == L"--release")
        {
            return print_reply(invoke(control_command::release));
        }
        if (argc == 4 &&
            (std::wstring_view(argv[1]) == L"--test-hold-before-request" ||
             std::wstring_view(argv[1]) == L"--test-hold-before-preface") &&
            std::wstring_view(argv[3]) == L"--status")
        {
            return print_reply(invoke_with_test_hold(
                control_command::status,
                {},
                parse_test_hold(argv[2]),
                std::wstring_view(argv[1]) == L"--test-hold-before-preface",
                print_pipe_inspection,
                nullptr));
        }
        std::wcerr <<
            L"usage: --acquire|--ensure --release-id release-NNN | "
            L"--status | --release\n";
        return ERROR_INVALID_PARAMETER;
    }
    catch (const powertoys::protected_runtime::control_error& error)
    {
        std::wcerr << L"win32 error=" << error.code() <<
            L" operation=" << error.what() << L"\n";
        return static_cast<int>(error.code());
    }
    catch (...)
    {
        std::wcerr << L"unexpected protected-runtime client failure\n";
        return ERROR_UNHANDLED_EXCEPTION;
    }
}
