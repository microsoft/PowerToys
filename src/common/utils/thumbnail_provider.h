#pragma once

#include <Windows.h>
#include <objidl.h>

#include <cerrno>
#include <cwchar>
#include <filesystem>
#include <fstream>
#include <string>
#include <string_view>
#include <vector>

#include <wil/resource.h>

namespace thumbnail_provider
{
    // This is a coarse shell-extension safety ceiling, not a startup target. The executable
    // cold-start test covers every supported thumbnail provider under this default budget.
    constexpr DWORD default_timeout_ms = 30'000;
    constexpr DWORD minimum_timeout_ms = 1'000;
    constexpr DWORD maximum_timeout_ms = 300'000;
    constexpr wchar_t timeout_environment_variable[] = L"POWERTOYS_THUMBNAIL_PROVIDER_TIMEOUT_MS";

    inline DWORD parse_timeout(std::wstring_view value)
    {
        if (value.empty())
        {
            return default_timeout_ms;
        }

        std::wstring nullTerminatedValue{ value };
        wchar_t* end = nullptr;
        errno = 0;
        const auto parsed = std::wcstoul(nullTerminatedValue.c_str(), &end, 10);
        if (errno == ERANGE ||
            end == nullTerminatedValue.c_str() ||
            *end != L'\0' ||
            parsed < minimum_timeout_ms ||
            parsed > maximum_timeout_ms)
        {
            return default_timeout_ms;
        }

        return static_cast<DWORD>(parsed);
    }

    inline DWORD get_timeout_ms()
    {
        wchar_t value[32]{};
        const auto length = GetEnvironmentVariableW(timeout_environment_variable, value, ARRAYSIZE(value));
        if (length == 0 || length >= ARRAYSIZE(value))
        {
            return default_timeout_ms;
        }

        return parse_timeout(value);
    }

    inline void release_stream(IStream*& stream) noexcept
    {
        if (stream)
        {
            stream->Release();
            stream = nullptr;
        }
    }

    inline HRESULT copy_stream_to_file(IStream* stream, const std::filesystem::path& destination) noexcept
    {
        if (!stream)
        {
            return E_INVALIDARG;
        }

        try
        {
            std::ofstream file(destination, std::ios_base::out | std::ios_base::binary | std::ios_base::trunc);
            if (!file.is_open())
            {
                return HRESULT_FROM_WIN32(ERROR_OPEN_FAILED);
            }

            char buffer[4096];
            while (true)
            {
                ULONG bytesRead = 0;
                const auto result = stream->Read(buffer, ARRAYSIZE(buffer), &bytesRead);
                if (FAILED(result))
                {
                    return result;
                }

                if (bytesRead != 0)
                {
                    file.write(buffer, bytesRead);
                    if (!file.good())
                    {
                        return STG_E_WRITEFAULT;
                    }
                }

                if (result == S_FALSE)
                {
                    return S_OK;
                }

                if (bytesRead == 0)
                {
                    return HRESULT_FROM_WIN32(ERROR_READ_FAULT);
                }
            }
        }
        catch (...)
        {
            return E_FAIL;
        }
    }

    enum class launch_status
    {
        completed,
        timed_out,
        failed,
    };

    struct launch_result
    {
        launch_status status = launch_status::failed;
        DWORD error = ERROR_SUCCESS;
        DWORD exit_code = 0;
        DWORD process_id = 0;
    };

    inline launch_result launch_in_job(
        const std::filesystem::path& application,
        const std::wstring& arguments,
        DWORD timeoutMs = get_timeout_ms())
    {
        launch_result result;

        wil::unique_handle job{ CreateJobObjectW(nullptr, nullptr) };
        if (!job)
        {
            result.error = GetLastError();
            return result;
        }

        JOBOBJECT_EXTENDED_LIMIT_INFORMATION jobInformation{};
        jobInformation.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
        if (!SetInformationJobObject(
                job.get(),
                JobObjectExtendedLimitInformation,
                &jobInformation,
                sizeof(jobInformation)))
        {
            result.error = GetLastError();
            return result;
        }

        std::wstring commandLine = L"\"" + application.wstring() + L"\"";
        if (!arguments.empty())
        {
            commandLine += L" ";
            commandLine += arguments;
        }

        std::vector<wchar_t> mutableCommandLine(commandLine.begin(), commandLine.end());
        mutableCommandLine.push_back(L'\0');

        STARTUPINFOW startupInformation{ sizeof(startupInformation) };
        PROCESS_INFORMATION processInformation{};
        if (!CreateProcessW(
                application.c_str(),
                mutableCommandLine.data(),
                nullptr,
                nullptr,
                FALSE,
                CREATE_NO_WINDOW | CREATE_SUSPENDED,
                nullptr,
                nullptr,
                &startupInformation,
                &processInformation))
        {
            result.error = GetLastError();
            return result;
        }

        wil::unique_handle process{ processInformation.hProcess };
        wil::unique_handle thread{ processInformation.hThread };
        result.process_id = processInformation.dwProcessId;

        if (!AssignProcessToJobObject(job.get(), process.get()))
        {
            result.error = GetLastError();
            TerminateProcess(process.get(), result.error);
            WaitForSingleObject(process.get(), 5'000);
            return result;
        }

        if (ResumeThread(thread.get()) == static_cast<DWORD>(-1))
        {
            result.error = GetLastError();
            TerminateJobObject(job.get(), result.error);
            WaitForSingleObject(process.get(), 5'000);
            return result;
        }

        const auto waitResult = WaitForSingleObject(process.get(), timeoutMs);
        if (waitResult == WAIT_OBJECT_0)
        {
            if (!GetExitCodeProcess(process.get(), &result.exit_code))
            {
                result.error = GetLastError();
                return result;
            }

            result.status = launch_status::completed;
            return result;
        }

        if (waitResult == WAIT_TIMEOUT)
        {
            result.status = launch_status::timed_out;
            result.error = ERROR_TIMEOUT;
        }
        else
        {
            result.error = GetLastError();
        }

        TerminateJobObject(job.get(), result.error);
        WaitForSingleObject(process.get(), 5'000);
        return result;
    }
}
