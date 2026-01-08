#include "pch.h"
#include "quick_access_host.h"

#include <mutex>
#include <string>
#include <vector>
#include <rpc.h>
#include <new>
#include <memory>

#include <common/logger/logger.h>
#include <common/utils/process_path.h>
#include <common/interop/two_way_pipe_message_ipc.h>
#include <wil/resource.h>

extern void receive_json_send_to_main_thread(const std::wstring& msg);

namespace
{
    wil::unique_handle quick_access_process;
    wil::unique_handle quick_access_job;
    wil::unique_handle show_event;
    wil::unique_handle exit_event;
    std::wstring show_event_name;
    std::wstring exit_event_name;
    std::wstring runner_pipe_name;
    std::wstring app_pipe_name;
    std::unique_ptr<TwoWayPipeMessageIPC> quick_access_ipc;
    std::mutex quick_access_mutex;

    bool is_process_active_locked()
    {
        if (!quick_access_process)
        {
            return false;
        }

        DWORD exit_code = 0;
        if (!GetExitCodeProcess(quick_access_process.get(), &exit_code))
        {
            Logger::warn(L"QuickAccessHost: failed to read Quick Access process exit code. error={}.", GetLastError());
            return false;
        }

        return exit_code == STILL_ACTIVE;
    }

    void reset_state_locked()
    {
        if (quick_access_ipc)
        {
            quick_access_ipc->end();
            quick_access_ipc.reset();
        }

        quick_access_process.reset();
        quick_access_job.reset();
        show_event.reset();
        exit_event.reset();
        show_event_name.clear();
        exit_event_name.clear();
        runner_pipe_name.clear();
        app_pipe_name.clear();
    }

    std::wstring build_event_name(const wchar_t* suffix)
    {
        std::wstring name = L"Local\\PowerToysQuickAccess_";
        name += std::to_wstring(GetCurrentProcessId());
        if (suffix)
        {
            name += suffix;
        }
        return name;
    }

    std::wstring build_command_line(const std::wstring& exe_path)
    {
        std::wstring command_line = L"\"";
        command_line += exe_path;
        command_line += L"\" --show-event=\"";
        command_line += show_event_name;
        command_line += L"\" --exit-event=\"";
        command_line += exit_event_name;
        command_line += L"\"";
        if (!runner_pipe_name.empty())
        {
            command_line.append(L" --runner-pipe=\"");
            command_line += runner_pipe_name;
            command_line += L"\"";
        }
        if (!app_pipe_name.empty())
        {
            command_line.append(L" --app-pipe=\"");
            command_line += app_pipe_name;
            command_line += L"\"";
        }
        return command_line;
    }
}

namespace QuickAccessHost
{
    bool is_running()
    {
        std::scoped_lock lock(quick_access_mutex);
        return is_process_active_locked();
    }

    void start()
    {
        Logger::info(L"QuickAccessHost::start() called");
        std::scoped_lock lock(quick_access_mutex);
        if (is_process_active_locked())
        {
            Logger::info(L"QuickAccessHost::start: process already active");
            return;
        }

        reset_state_locked();

        show_event_name = build_event_name(L"_Show");
        exit_event_name = build_event_name(L"_Exit");

        show_event.reset(CreateEventW(nullptr, FALSE, FALSE, show_event_name.c_str()));
        if (!show_event)
        {
            Logger::error(L"QuickAccessHost: failed to create show event. error={}.", GetLastError());
            reset_state_locked();
            return;
        }

        exit_event.reset(CreateEventW(nullptr, FALSE, FALSE, exit_event_name.c_str()));
        if (!exit_event)
        {
            Logger::error(L"QuickAccessHost: failed to create exit event. error={}.", GetLastError());
            reset_state_locked();
            return;
        }

        runner_pipe_name = L"\\\\.\\pipe\\powertoys_quick_access_runner_";
        app_pipe_name = L"\\\\.\\pipe\\powertoys_quick_access_ui_";
        UUID temp_uuid;
        wchar_t* uuid_chars = nullptr;
        if (UuidCreate(&temp_uuid) == RPC_S_UUID_NO_ADDRESS)
        {
            Logger::warn(L"QuickAccessHost: failed to create UUID for pipe names. error={}.", GetLastError());
        }
        else if (UuidToString(&temp_uuid, reinterpret_cast<RPC_WSTR*>(&uuid_chars)) != RPC_S_OK)
        {
            Logger::warn(L"QuickAccessHost: failed to convert UUID to string. error={}.", GetLastError());
        }

        if (uuid_chars != nullptr)
        {
            runner_pipe_name += std::wstring(uuid_chars);
            app_pipe_name += std::wstring(uuid_chars);
            RpcStringFree(reinterpret_cast<RPC_WSTR*>(&uuid_chars));
            uuid_chars = nullptr;
        }
        else
        {
            const std::wstring fallback_suffix = std::to_wstring(GetTickCount64());
            runner_pipe_name += fallback_suffix;
            app_pipe_name += fallback_suffix;
        }

        HANDLE token_handle = nullptr;
        if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &token_handle))
        {
            Logger::error(L"QuickAccessHost: failed to open process token. error={}.", GetLastError());
            reset_state_locked();
            return;
        }

        wil::unique_handle token(token_handle);
        quick_access_ipc.reset(new (std::nothrow) TwoWayPipeMessageIPC(runner_pipe_name, app_pipe_name, receive_json_send_to_main_thread));
        if (!quick_access_ipc)
        {
            Logger::error(L"QuickAccessHost: failed to allocate IPC instance.");
            reset_state_locked();
            return;
        }

        try
        {
            quick_access_ipc->start(token.get());
        }
        catch (...)
        {
            Logger::error(L"QuickAccessHost: failed to start IPC server for Quick Access.");
            reset_state_locked();
            return;
        }

        const std::wstring exe_path = get_module_folderpath() + L"\\WinUI3Apps\\PowerToys.QuickAccess.exe";
        if (GetFileAttributesW(exe_path.c_str()) == INVALID_FILE_ATTRIBUTES)
        {
            Logger::warn(L"QuickAccessHost: missing Quick Access executable at {}", exe_path);
            reset_state_locked();
            return;
        }

        const std::wstring command_line = build_command_line(exe_path);
        std::vector<wchar_t> command_line_buffer(command_line.begin(), command_line.end());
        command_line_buffer.push_back(L'\0');
        STARTUPINFOW startup_info{};
        startup_info.cb = sizeof(startup_info);
        PROCESS_INFORMATION process_info{};

        BOOL created = CreateProcessW(exe_path.c_str(), command_line_buffer.data(), nullptr, nullptr, FALSE, CREATE_SUSPENDED, nullptr, nullptr, &startup_info, &process_info);
        if (!created)
        {
            Logger::error(L"QuickAccessHost: failed to launch Quick Access host. error={}.", GetLastError());
            reset_state_locked();
            return;
        }

        quick_access_process.reset(process_info.hProcess);

        // Assign to job object to ensure the process is killed if the runner exits unexpectedly (e.g. debugging stop)
        quick_access_job.reset(CreateJobObjectW(nullptr, nullptr));
        if (quick_access_job)
        {
            JOBOBJECT_EXTENDED_LIMIT_INFORMATION jeli = { 0 };
            jeli.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
            if (!SetInformationJobObject(quick_access_job.get(), JobObjectExtendedLimitInformation, &jeli, sizeof(jeli)))
            {
                Logger::warn(L"QuickAccessHost: failed to set job object information. error={}", GetLastError());
            }
            else
            {
                if (!AssignProcessToJobObject(quick_access_job.get(), quick_access_process.get()))
                {
                    Logger::warn(L"QuickAccessHost: failed to assign process to job object. error={}", GetLastError());
                }
            }
        }
        else
        {
            Logger::warn(L"QuickAccessHost: failed to create job object. error={}", GetLastError());
        }

        ResumeThread(process_info.hThread);
        CloseHandle(process_info.hThread);
    }

    void show()
    {
        start();
        std::scoped_lock lock(quick_access_mutex);

        if (show_event)
        {
            if (!SetEvent(show_event.get()))
            {
                Logger::warn(L"QuickAccessHost: failed to signal show event. error={}.", GetLastError());
            }
        }
    }

    void stop()
    {
        Logger::info(L"QuickAccessHost::stop() called");
        std::unique_lock lock(quick_access_mutex);
        if (exit_event)
        {
            SetEvent(exit_event.get());
        }

        if (quick_access_process)
        {
            const DWORD wait_result = WaitForSingleObject(quick_access_process.get(), 2000);
            Logger::info(L"QuickAccessHost::stop: WaitForSingleObject result={}", wait_result);
            if (wait_result == WAIT_TIMEOUT)
            {
                Logger::warn(L"QuickAccessHost: Quick Access process did not exit in time, terminating.");
                if (!TerminateProcess(quick_access_process.get(), 0))
                {
                    Logger::error(L"QuickAccessHost: failed to terminate Quick Access process. error={}.", GetLastError());
                }
                else
                {
                    Logger::info(L"QuickAccessHost: TerminateProcess succeeded.");
                    WaitForSingleObject(quick_access_process.get(), 5000);
                }
            }
            else if (wait_result == WAIT_FAILED)
            {
                Logger::error(L"QuickAccessHost: failed while waiting for Quick Access process. error={}.", GetLastError());
            }
        }

        reset_state_locked();
    }
}
