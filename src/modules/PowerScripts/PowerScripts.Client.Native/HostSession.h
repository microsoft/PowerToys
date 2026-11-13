// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <Windows.h>
#include <shellapi.h>
#include <roapi.h>
#include <wil/resource.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.Data.Json.h>
#include <algorithm>
#include <array>
#include <cmath>
#include <filesystem>
#include <memory>
#include <limits>
#include <mutex>
#include <optional>
#include <string>
#include <thread>
#include <vector>

namespace PowerScripts
{
    struct HostResult
    {
        int ExitCode;
        std::wstring Error;
        std::wstring Output;
    };

    inline std::optional<std::vector<std::wstring>> ParseHotkeyInvocation(const std::wstring& arguments)
    {
        int count = 0;
        wil::unique_hlocal_ptr<PWSTR[]> parsed{ CommandLineToArgvW((L"host " + arguments).c_str(), &count) };
        if (!parsed || count < 6 || wcscmp(parsed[1], L"run") != 0 ||
            std::wstring_view{ parsed[2] }.find_first_not_of(L"abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.") != std::wstring_view::npos ||
            parsed[2][0] == L'\0')
        {
            return std::nullopt;
        }

        bool consumer = false;
        bool silent = false;
        for (int index = 3; index < count; ++index)
        {
            const std::wstring_view token{ parsed[index] };
            if (token == L"--consumer" && !consumer && index + 1 < count && wcscmp(parsed[index + 1], L"keyboard-manager") == 0)
            {
                consumer = true;
                ++index;
            }
            else if (token == L"--no-consent" && !silent)
            {
                silent = true;
            }
            else if (token == L"--set-base64" && index + 1 < count && !std::wstring_view{ parsed[index + 1] }.starts_with(L"--"))
            {
                while (index + 1 < count && !std::wstring_view{ parsed[index + 1] }.starts_with(L"--"))
                {
                    ++index;
                }
            }
            else
            {
                return std::nullopt;
            }
        }

        if (!consumer || !silent)
        {
            return std::nullopt;
        }

        return std::vector<std::wstring>{ parsed.get() + 1, parsed.get() + count };
    }

    // Uses the same private stdio framing as the managed client, with no named IPC endpoint.
    class HostSession
    {
    public:
        const std::wstring Path;
        const std::wstring Directory;

        HostSession(std::wstring path, std::wstring directory) :
            Path(std::move(path)), Directory(std::move(directory))
        {
        }

        ~HostSession()
        {
            // Signal only this Host, not an enclosing job that could capture the shared Linux daemon.
            if (m_stopEvent)
            {
                SetEvent(m_stopEvent.get());
            }
            m_input.reset();
            if (m_process)
            {
                WaitForSingleObject(m_process.get(), INFINITE);
            }
            if (m_drain.joinable())
            {
                m_drain.join();
            }
        }

        HostResult Invoke(const std::vector<std::wstring>& arguments)
        {
            using namespace winrt::Windows::Data::Json;
            if (!m_process)
            {
                Start();
            }

            JsonArray values;
            for (const auto& argument : arguments)
            {
                values.Append(JsonValue::CreateStringValue(argument));
            }
            JsonObject request;
            request.Insert(L"arguments", values);
            request.Insert(L"input", JsonValue::CreateNullValue());
            const auto payload = winrt::to_string(request.Stringify());
            THROW_HR_IF(E_INVALIDARG, payload.empty() || payload.size() > MaximumFrameBytes);
            const auto size = static_cast<uint32_t>(payload.size());
            Write(&size, sizeof(size));
            Write(payload.data(), size);

            uint32_t length = 0;
            Read(&length, sizeof(length));
            THROW_HR_IF_MSG(E_INVALIDARG, length == 0 || length > MaximumFrameBytes, "Invalid PowerScripts Host response size.");
            std::string response(length, '\0');
            Read(response.data(), length);
            const auto result = JsonObject::Parse(winrt::to_hstring(response));
            const auto code = result.GetNamedNumber(L"exitCode");
            THROW_HR_IF(E_INVALIDARG, !std::isfinite(code) || std::trunc(code) != code || code < (std::numeric_limits<int>::min)() || code > (std::numeric_limits<int>::max)());
            return { static_cast<int>(code), std::wstring{ result.GetNamedString(L"stderr") }, std::wstring{ result.GetNamedString(L"stdout") } };
        }

    private:
        static constexpr uint32_t MaximumFrameBytes = 128 * 1024 * 1024;
        wil::unique_handle m_process;
        wil::unique_handle m_stopEvent;
        wil::unique_handle m_input;
        wil::unique_handle m_output;
        std::thread m_drain;

        void Write(const void* data, DWORD length)
        {
            const auto* current = static_cast<const char*>(data);
            while (length > 0)
            {
                DWORD written = 0;
                THROW_IF_WIN32_BOOL_FALSE(WriteFile(m_input.get(), current, length, &written, nullptr));
                THROW_HR_IF(E_UNEXPECTED, written == 0);
                current += written;
                length -= written;
            }
        }

        void Read(void* data, DWORD length)
        {
            auto* current = static_cast<char*>(data);
            while (length > 0)
            {
                DWORD received = 0;
                THROW_IF_WIN32_BOOL_FALSE(ReadFile(m_output.get(), current, length, &received, nullptr));
                THROW_HR_IF_MSG(E_UNEXPECTED, received == 0, "PowerScripts Host exited before returning its result; the invocation was not retried.");
                current += received;
                length -= received;
            }
        }

        void Start()
        {
            SECURITY_ATTRIBUTES security{ sizeof(security), nullptr, TRUE };
            wil::unique_handle childInput;
            wil::unique_handle childOutput;
            wil::unique_handle childError;
            wil::unique_handle errors;
            THROW_IF_WIN32_BOOL_FALSE(CreatePipe(childInput.put(), m_input.put(), &security, 0));
            THROW_IF_WIN32_BOOL_FALSE(CreatePipe(m_output.put(), childOutput.put(), &security, 0));
            THROW_IF_WIN32_BOOL_FALSE(CreatePipe(errors.put(), childError.put(), &security, 0));
            THROW_IF_WIN32_BOOL_FALSE(SetHandleInformation(m_input.get(), HANDLE_FLAG_INHERIT, 0));
            THROW_IF_WIN32_BOOL_FALSE(SetHandleInformation(m_output.get(), HANDLE_FLAG_INHERIT, 0));
            THROW_IF_WIN32_BOOL_FALSE(SetHandleInformation(errors.get(), HANDLE_FLAG_INHERIT, 0));

            SIZE_T bytes = 0;
            InitializeProcThreadAttributeList(nullptr, 1, 0, &bytes);
            std::vector<BYTE> storage(bytes);
            auto attributes = reinterpret_cast<LPPROC_THREAD_ATTRIBUTE_LIST>(storage.data());
            THROW_IF_WIN32_BOOL_FALSE(InitializeProcThreadAttributeList(attributes, 1, 0, &bytes));
            const auto cleanup = wil::scope_exit([&] { DeleteProcThreadAttributeList(attributes); });
            m_stopEvent.reset(CreateEventW(&security, TRUE, FALSE, nullptr));
            THROW_LAST_ERROR_IF(!m_stopEvent);
            std::array<HANDLE, 4> inherited{ childInput.get(), childOutput.get(), childError.get(), m_stopEvent.get() };
            THROW_IF_WIN32_BOOL_FALSE(UpdateProcThreadAttribute(
                attributes, 0, PROC_THREAD_ATTRIBUTE_HANDLE_LIST, inherited.data(), sizeof(inherited), nullptr, nullptr));
            STARTUPINFOEXW startup{};
            startup.StartupInfo.cb = sizeof(startup);
            startup.StartupInfo.dwFlags = STARTF_USESTDHANDLES;
            startup.StartupInfo.hStdInput = childInput.get();
            startup.StartupInfo.hStdOutput = childOutput.get();
            startup.StartupInfo.hStdError = childError.get();
            startup.lpAttributeList = attributes;

            auto command = L"\"" + Path + L"\" session --owner " + std::to_wstring(GetCurrentProcessId()) +
                           L" --stop-event " + std::to_wstring(reinterpret_cast<uintptr_t>(m_stopEvent.get()));
            PROCESS_INFORMATION process{};
            THROW_IF_WIN32_BOOL_FALSE(CreateProcessW(
                Path.c_str(), command.data(), nullptr, nullptr, TRUE, CREATE_NO_WINDOW | EXTENDED_STARTUPINFO_PRESENT, nullptr, Directory.empty() ? nullptr : Directory.c_str(), &startup.StartupInfo, &process));
            m_process.reset(process.hProcess);
            wil::unique_handle thread{ process.hThread };
            THROW_IF_WIN32_BOOL_FALSE(SetHandleInformation(m_stopEvent.get(), HANDLE_FLAG_INHERIT, 0));
            // Drain startup diagnostics so a failed Host cannot block on its stderr pipe.
            m_drain = std::thread([stream = std::move(errors)] {
                std::array<char, 4096> buffer{};
                DWORD received = 0;
                while (ReadFile(stream.get(), buffer.data(), static_cast<DWORD>(buffer.size()), &received, nullptr) && received > 0)
                {
                }
            });
        }
    };

    class HostPool
    {
    public:
        HostPool()
        {
            // Keep cached WinRT JSON factories valid across per-call apartment teardown.
            THROW_IF_FAILED(CoIncrementMTAUsage(m_mta.put()));
        }

        ~HostPool()
        {
            winrt::clear_factory_cache();
        }

        HostResult Invoke(const std::wstring& path, const std::wstring& directory, const std::vector<std::wstring>& arguments)
        {
            const auto initialized = RoInitialize(RO_INIT_MULTITHREADED);
            if (initialized != RPC_E_CHANGED_MODE)
            {
                THROW_IF_FAILED(initialized);
            }
            const auto cleanup = wil::scope_exit([initialized] {
                if (SUCCEEDED(initialized))
                {
                    RoUninitialize();
                }
            });
            std::unique_ptr<HostSession> session;
            {
                std::scoped_lock lock(m_gate);
                const auto found = std::find_if(m_idle.begin(), m_idle.end(), [&](const auto& candidate) {
                    return _wcsicmp(candidate->Path.c_str(), path.c_str()) == 0 && _wcsicmp(candidate->Directory.c_str(), directory.c_str()) == 0;
                });
                if (found != m_idle.end())
                {
                    session = std::move(*found);
                    m_idle.erase(found);
                }
            }
            if (!session)
            {
                session = std::make_unique<HostSession>(path, directory);
            }

            // Failure destroys this session. Never replay a possibly delivered invocation.
            const auto result = session->Invoke(arguments);
            {
                std::scoped_lock lock(m_gate);
                if (m_idle.size() < 2)
                {
                    m_idle.push_back(std::move(session));
                }
            }
            return result;
        }

    private:
        wil::unique_mta_usage_cookie m_mta;
        std::mutex m_gate;
        std::vector<std::unique_ptr<HostSession>> m_idle;
    };
}
