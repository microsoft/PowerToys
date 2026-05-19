// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "PowerDisplayProcessManager.h"

#include <common/logger/logger.h>
#include <common/utils/winapi_error.h>
#include <common/interop/shared_constants.h>
#include <atlstr.h>

namespace
{
    std::optional<std::wstring> get_pipe_name(const std::wstring& prefix)
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

        const auto pipe_name = std::format(L"{}{}", prefix, std::wstring(uuid_chars));
        RpcStringFree(reinterpret_cast<RPC_WSTR*>(&uuid_chars));

        return pipe_name;
    }
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

void PowerDisplayProcessManager::send_message(const std::wstring& message_type, const std::wstring& message_arg)
{
    submit_task([this, message_type, message_arg] {
        if (!m_enabled)
        {
            Logger::warn(L"Ignoring '{}' message because PowerDisplay is disabled", message_type);
            return;
        }

        // If the process exited unexpectedly while the module is still enabled,
        // bring it back before delivering the message.
        if (!is_process_running())
        {
            refresh();

            if (!is_process_running())
            {
                Logger::warn(L"PowerDisplay process is not running; '{}' message was not delivered", message_type);
                return;
            }
        }

        send_named_pipe_message(message_type, message_arg);
    });
}

bool PowerDisplayProcessManager::is_running() const
{
    return is_process_running();
}

void PowerDisplayProcessManager::submit_task(std::function<void()> task)
{
    m_thread_executor.submit(OnThreadExecutor::task_t{ task });
}

bool PowerDisplayProcessManager::is_process_running() const
{
    return m_hProcess != 0 && WaitForSingleObject(m_hProcess, 0) == WAIT_TIMEOUT;
}

void PowerDisplayProcessManager::terminate_process()
{
    if (m_hProcess != 0)
    {
        TerminateProcess(m_hProcess, 1);
        CloseHandle(m_hProcess);
        m_hProcess = 0;
    }
}

HRESULT PowerDisplayProcessManager::start_process(const std::wstring& pipe_name)
{
    const unsigned long powertoys_pid = GetCurrentProcessId();

    // Pass both PID and pipe name as arguments
    const auto executable_args = std::format(L"{} {}", std::to_wstring(powertoys_pid), pipe_name);

    SHELLEXECUTEINFOW sei{ sizeof(sei) };
    sei.fMask = { SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI };
    sei.lpFile = L"WinUI3Apps\\PowerToys.PowerDisplay.exe";
    sei.nShow = SW_SHOWNORMAL;
    sei.lpParameters = executable_args.data();
    if (ShellExecuteExW(&sei))
    {
        Logger::trace("Successfully started PowerDisplay process");
        terminate_process();
        m_hProcess = sei.hProcess;
        start_exit_watch();
        return S_OK;
    }
    else
    {
        Logger::error(L"PowerDisplay process failed to start. {}", get_last_error_or_default(GetLastError()));
        return E_FAIL;
    }
}

PowerDisplayProcessManager::~PowerDisplayProcessManager()
{
    clear_exit_watch();
}

void PowerDisplayProcessManager::start_exit_watch()
{
    clear_exit_watch();

    if (!DuplicateHandle(GetCurrentProcess(), m_hProcess, GetCurrentProcess(),
                         &m_exitProcessHandle, SYNCHRONIZE, FALSE, 0))
    {
        Logger::error(L"DuplicateHandle for exit watch failed; crash auto-relaunch disabled");
        return;
    }

    if (!RegisterWaitForSingleObject(&m_exitWaitRegistration, m_exitProcessHandle,
                                     &PowerDisplayProcessManager::on_process_exited,
                                     this, INFINITE, WT_EXECUTEONLYONCE))
    {
        Logger::error(L"RegisterWaitForSingleObject failed; crash auto-relaunch disabled");
        CloseHandle(m_exitProcessHandle);
        m_exitProcessHandle = nullptr;
    }
}

void PowerDisplayProcessManager::clear_exit_watch()
{
    if (m_exitWaitRegistration)
    {
        // Block until any in-flight callback finishes — required so teardown is safe.
        UnregisterWaitEx(m_exitWaitRegistration, INVALID_HANDLE_VALUE);
        m_exitWaitRegistration = nullptr;
    }
    if (m_exitProcessHandle)
    {
        CloseHandle(m_exitProcessHandle);
        m_exitProcessHandle = nullptr;
    }
}

// Decide whether to auto-relaunch PowerDisplay.exe after it exits. Two skip conditions:
//   1. m_enabled is false — stop() flipped it before terminating, so the exit was
//      intentional.
//   2. exit code 0 — clean exit. Two sources, both must skip:
//      - Phase 0 auto-disable (Environment.Exit(0) after writing the flag). Relaunching
//        here would race with the AutoDisable listener and bring up a UI that's about
//        to be torn down.
//      - User-driven quit via tray/window close. Module is still enabled in settings
//        but the user explicitly closed the UI; relaunching would be surprising. The
//        existing "lazy restart on next send_message" handles that path.
// Only non-zero exits (FailFast=0xC0000409, TerminateProcess=1, unhandled exception, etc.)
// trigger a relaunch so the next instance's Phase 0 can detect the orphan lock.
VOID CALLBACK PowerDisplayProcessManager::on_process_exited(PVOID context, BOOLEAN /*timedOut*/)
{
    auto* self = static_cast<PowerDisplayProcessManager*>(context);

    if (!self->m_enabled.load())
    {
        return;
    }

    DWORD exitCode = 0;
    if (GetExitCodeProcess(self->m_exitProcessHandle, &exitCode) && exitCode == 0)
    {
        Logger::trace(L"PowerDisplay exited cleanly; not relaunching");
        return;
    }

    Logger::warn(L"PowerDisplay exited unexpectedly (code {}); relaunching so Phase 0 can run", exitCode);
    self->submit_task([self]() { self->refresh(); });
}

HRESULT PowerDisplayProcessManager::start_named_pipe_server(const std::wstring& pipe_name)
{
    m_write_pipe = nullptr;

    const constexpr DWORD BUFSIZE = 4096 * 4;

    const auto full_pipe_name = std::format(L"\\\\.\\pipe\\{}", pipe_name);

    const auto hPipe = CreateNamedPipe(
        full_pipe_name.c_str(),     // pipe name
        PIPE_ACCESS_OUTBOUND |      // write access
            FILE_FLAG_OVERLAPPED,   // overlapped mode
        PIPE_TYPE_MESSAGE |         // message type pipe
            PIPE_READMODE_MESSAGE | // message-read mode
            PIPE_WAIT,              // blocking mode
        1,                          // max. instances
        BUFSIZE,                    // output buffer size
        0,                          // input buffer size
        0,                          // client time-out
        NULL);                      // default security attribute

    if (hPipe == NULL || hPipe == INVALID_HANDLE_VALUE)
    {
        Logger::error(L"Error creating handle for named pipe");
        return E_FAIL;
    }

    // Create overlapped event to wait for client to connect to pipe.
    OVERLAPPED overlapped = { 0 };
    overlapped.hEvent = CreateEvent(nullptr, true, false, nullptr);
    if (!overlapped.hEvent)
    {
        Logger::error(L"Error creating overlapped event for named pipe");
        CloseHandle(hPipe);
        return E_FAIL;
    }

    const auto clean_up_and_fail = [&]() {
        CloseHandle(overlapped.hEvent);
        CloseHandle(hPipe);
        return E_FAIL;
    };

    if (!ConnectNamedPipe(hPipe, &overlapped))
    {
        const auto lastError = GetLastError();

        if (lastError != ERROR_IO_PENDING && lastError != ERROR_PIPE_CONNECTED)
        {
            Logger::error(L"Error connecting to named pipe");
            return clean_up_and_fail();
        }
    }

    // Wait for client.
    const constexpr DWORD client_timeout_millis = 5000;
    switch (WaitForSingleObject(overlapped.hEvent, client_timeout_millis))
    {
        case WAIT_OBJECT_0:
        {
            DWORD bytes_transferred = 0;
            if (GetOverlappedResult(hPipe, &overlapped, &bytes_transferred, FALSE))
            {
                CloseHandle(overlapped.hEvent);
                m_write_pipe = std::make_unique<CAtlFile>(hPipe);

                Logger::trace(L"PowerDisplay successfully connected to named pipe");

                return S_OK;
            }
            else
            {
                Logger::error(L"Error waiting for PowerDisplay to connect to named pipe");
                return clean_up_and_fail();
            }
        }

        case WAIT_TIMEOUT:
        case WAIT_FAILED:
        default:
            Logger::error(L"Error waiting for PowerDisplay to connect to named pipe");
            return clean_up_and_fail();
    }
}

void PowerDisplayProcessManager::refresh()
{
    if (m_enabled == is_process_running())
    {
        return;
    }

    if (m_enabled)
    {
        Logger::trace(L"Starting PowerDisplay process");

        const auto pipe_name = get_pipe_name(L"powertoys_power_display_");

        if (!pipe_name)
        {
            return;
        }

        if (start_process(pipe_name.value()) != S_OK)
        {
            return;
        }

        if (start_named_pipe_server(pipe_name.value()) != S_OK)
        {
            Logger::error(L"Named pipe initialization failed; terminating PowerDisplay process");
            terminate_process();
        }
    }
    else
    {
        Logger::trace(L"Exiting PowerDisplay process");

        send_named_pipe_message(CommonSharedConstants::POWER_DISPLAY_TERMINATE_APP_MESSAGE);
        WaitForSingleObject(m_hProcess, 5000);

        if (is_process_running())
        {
            Logger::error(L"PowerDisplay process failed to gracefully exit; terminating");
        }
        else
        {
            Logger::trace(L"PowerDisplay process successfully exited");
        }

        terminate_process();
    }
}

void PowerDisplayProcessManager::send_named_pipe_message(const std::wstring& message_type, const std::wstring& message_arg)
{
    if (m_write_pipe)
    {
        const auto message = message_arg.empty() ? std::format(L"{}\r\n", message_type) : std::format(L"{} {}\r\n", message_type, message_arg);

        const CString file_name(message.c_str());
        m_write_pipe->Write(file_name, file_name.GetLength() * sizeof(TCHAR));
    }
}
