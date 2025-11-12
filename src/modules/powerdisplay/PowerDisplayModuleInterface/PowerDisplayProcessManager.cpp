// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "PowerDisplayProcessManager.h"

#include <common/logger/logger.h>
#include <common/utils/winapi_error.h>
#include <common/interop/shared_constants.h>
#include <atlstr.h>
#include <format>

namespace
{
    /// <summary>
    /// Generate a pipe name with UUID suffix
    /// </summary>
    std::optional<std::wstring> get_pipe_uuid()
    {
        UUID temp_uuid;
        wchar_t* uuid_chars = nullptr;

        if (UuidCreate(&temp_uuid) == RPC_S_UUID_NO_ADDRESS)
        {
            const auto val = get_last_error_message(GetLastError());
            Logger::error(L"UuidCreate cannot create guid. {}", val.has_value() ? val.value() : L"");
            return std::nullopt;
        }
        else if (UuidToString(&temp_uuid, reinterpret_cast<RPC_WSTR*>(&uuid_chars)) != RPC_S_OK)
        {
            const auto val = get_last_error_message(GetLastError());
            Logger::error(L"UuidToString cannot convert to string. {}", val.has_value() ? val.value() : L"");
            return std::nullopt;
        }

        const auto uuid_str = std::wstring(uuid_chars);
        RpcStringFree(reinterpret_cast<RPC_WSTR*>(&uuid_chars));

        return uuid_str;
    }
}

PowerDisplayProcessManager::~PowerDisplayProcessManager()
{
    stop();
}

void PowerDisplayProcessManager::start()
{
    m_enabled = true;
    submit_task([this]() { refresh(); });
}

void PowerDisplayProcessManager::stop()
{
    m_enabled = false;
    submit_task([this]() { refresh(); });
}

void PowerDisplayProcessManager::send_message_to_powerdisplay(const std::wstring& message)
{
    submit_task([this, message]() {
        if (m_write_pipe)
        {
            try
            {
                const auto formatted = std::format(L"{}\r\n", message);
                
                // Convert wide string to UTF-8 for C# interop
                int utf8_size = WideCharToMultiByte(CP_UTF8, 0, formatted.c_str(), -1, nullptr, 0, nullptr, nullptr);
                if (utf8_size > 0)
                {
                    std::string utf8_msg(static_cast<size_t>(utf8_size) - 1, '\0'); // -1 to exclude null terminator
                    WideCharToMultiByte(CP_UTF8, 0, formatted.c_str(), -1, &utf8_msg[0], utf8_size, nullptr, nullptr);

                    const DWORD bytes_to_write = static_cast<DWORD>(utf8_msg.length());
                    DWORD bytes_written = 0;
                    
                    if (FAILED(m_write_pipe->Write(utf8_msg.c_str(), bytes_to_write, &bytes_written)))
                    {
                        Logger::error(L"Failed to write message to PowerDisplay pipe");
                    }
                    else
                    {
                        Logger::trace(L"Sent message to PowerDisplay: {}", message);
                    }
                }
            }
            catch (...)
            {
                Logger::error(L"Exception while sending message to PowerDisplay");
            }
        }
        else
        {
            Logger::warn(L"Cannot send message to PowerDisplay: pipe not connected");
        }
    });
}

void PowerDisplayProcessManager::submit_task(std::function<void()> task)
{
    m_thread_executor.submit(OnThreadExecutor::task_t{ task });
}

bool PowerDisplayProcessManager::is_process_running() const
{
    return m_hProcess != nullptr && WaitForSingleObject(m_hProcess, 0) == WAIT_TIMEOUT;
}

void PowerDisplayProcessManager::terminate_process()
{
    // Close pipe
    m_write_pipe.reset();

    // Terminate process
    if (m_hProcess != nullptr)
    {
        TerminateProcess(m_hProcess, 1);
        CloseHandle(m_hProcess);
        m_hProcess = nullptr;
    }

    Logger::trace(L"PowerDisplay process terminated");
}

HRESULT PowerDisplayProcessManager::start_process(const std::wstring& pipe_uuid)
{
    const unsigned long powertoys_pid = GetCurrentProcessId();

    // Pass both runner PID and pipe UUID to PowerDisplay.exe
    const auto executable_args = std::format(L"{} {}", std::to_wstring(powertoys_pid), pipe_uuid);

    SHELLEXECUTEINFOW sei{ sizeof(sei) };
    sei.fMask = SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI;
    sei.lpFile = L"WinUI3Apps\\PowerToys.PowerDisplay.exe";
    sei.nShow = SW_SHOWNORMAL;
    sei.lpParameters = executable_args.data();

    if (ShellExecuteExW(&sei))
    {
        Logger::trace(L"Successfully started PowerDisplay process with UUID: {}", pipe_uuid);
        terminate_process(); // Clean up old process if any
        m_hProcess = sei.hProcess;
        return S_OK;
    }
    else
    {
        Logger::error(L"PowerDisplay process failed to start. {}", get_last_error_or_default(GetLastError()));
        return E_FAIL;
    }
}

HRESULT PowerDisplayProcessManager::start_command_pipe(const std::wstring& pipe_uuid)
{
    const constexpr DWORD BUFSIZE = 4096 * 4;
    const constexpr DWORD client_timeout_millis = 5000;

    // Create single-direction pipe: ModuleInterface writes commands to PowerDisplay
    const std::wstring pipe_name = std::format(L"\\\\.\\pipe\\powertoys_powerdisplay_{}", pipe_uuid);

    HANDLE hWritePipe = CreateNamedPipe(
        pipe_name.c_str(),
        PIPE_ACCESS_OUTBOUND | FILE_FLAG_OVERLAPPED,
        PIPE_TYPE_MESSAGE | PIPE_READMODE_MESSAGE | PIPE_WAIT,
        1,       // max instances
        BUFSIZE, // out buffer size
        0,       // in buffer size (not used for outbound)
        0,       // client timeout
        NULL     // default security
    );

    if (hWritePipe == NULL || hWritePipe == INVALID_HANDLE_VALUE)
    {
        Logger::error(L"Error creating pipe for PowerDisplay");
        return E_FAIL;
    }

    // Create overlapped event for waiting for client to connect
    OVERLAPPED overlapped = { 0 };
    overlapped.hEvent = CreateEvent(nullptr, TRUE, FALSE, nullptr);

    if (!overlapped.hEvent)
    {
        Logger::error(L"Error creating overlapped event for PowerDisplay pipe");
        CloseHandle(hWritePipe);
        return E_FAIL;
    }

    // Connect pipe
    if (!ConnectNamedPipe(hWritePipe, &overlapped))
    {
        const auto lastError = GetLastError();
        if (lastError != ERROR_IO_PENDING && lastError != ERROR_PIPE_CONNECTED)
        {
            Logger::error(L"Error connecting pipe");
            CloseHandle(overlapped.hEvent);
            CloseHandle(hWritePipe);
            return E_FAIL;
        }
    }

    // Wait for pipe to connect (with timeout)
    DWORD wait_result = WaitForSingleObject(overlapped.hEvent, client_timeout_millis);
    CloseHandle(overlapped.hEvent);

    if (wait_result == WAIT_OBJECT_0 || wait_result == WAIT_TIMEOUT)
    {
        // Check if actually connected
        DWORD bytes_transferred = 0;
        if (GetOverlappedResult(hWritePipe, &overlapped, &bytes_transferred, FALSE) || GetLastError() == ERROR_PIPE_CONNECTED)
        {
            m_write_pipe = std::make_unique<CAtlFile>(hWritePipe);
            Logger::trace(L"PowerDisplay pipe connected successfully: {}", pipe_name);
            return S_OK;
        }
    }

    Logger::error(L"Timeout waiting for PowerDisplay to connect to pipe");
    CloseHandle(hWritePipe);
    return E_FAIL;
}

void PowerDisplayProcessManager::refresh()
{
    if (m_enabled == is_process_running())
    {
        // Already in correct state
        return;
    }

    if (m_enabled)
    {
        // Start PowerDisplay process
        Logger::trace(L"Starting PowerDisplay process");

        const auto pipe_uuid = get_pipe_uuid();
        if (!pipe_uuid)
        {
            Logger::error(L"Failed to generate pipe UUID");
            return;
        }

        // FIX BUG #1: Start process FIRST, then create pipes
        // This ensures PowerDisplay.exe is running when pipes try to connect
        if (start_process(pipe_uuid.value()) != S_OK)
        {
            Logger::error(L"Failed to start PowerDisplay process");
            return;
        }

        // Now create pipes and wait for PowerDisplay to connect
        if (start_command_pipe(pipe_uuid.value()) != S_OK)
        {
            Logger::error(L"Failed to initialize command pipes, terminating process");
            terminate_process();
        }
    }
    else
    {
        // Stop PowerDisplay process
        Logger::trace(L"Stopping PowerDisplay process");

        // Send terminate message
        send_message_to_powerdisplay(L"{\"action\":\"terminate\"}");

        // Wait for graceful exit
        if (m_hProcess != nullptr)
        {
            WaitForSingleObject(m_hProcess, 2000);
        }

        if (is_process_running())
        {
            Logger::warn(L"PowerDisplay process failed to gracefully exit, terminating");
        }
        else
        {
            Logger::trace(L"PowerDisplay process successfully exited");
        }

        terminate_process();
    }
}
