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
    struct PositionPayload
    {
        LONG x;
        LONG y;
        LONG sequence;
    };

    wil::unique_handle quick_access_process;
    wil::unique_handle show_event;
    wil::unique_handle exit_event;
    wil::unique_handle position_mapping;
    std::wstring show_event_name;
    std::wstring exit_event_name;
    std::wstring position_mapping_name;
    std::wstring runner_pipe_name;
    std::wstring app_pipe_name;
    PositionPayload* position_payload = nullptr;
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

        if (position_payload)
        {
            UnmapViewOfFile(position_payload);
            position_payload = nullptr;
        }

        quick_access_process.reset();
        show_event.reset();
        exit_event.reset();
        position_mapping.reset();
        show_event_name.clear();
        exit_event_name.clear();
        position_mapping_name.clear();
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
        command_line += L"\" --position-map=\"";
        command_line += position_mapping_name;
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
        std::scoped_lock lock(quick_access_mutex);
        if (is_process_active_locked())
        {
            return;
        }

        reset_state_locked();

        show_event_name = build_event_name(L"_Show");
        exit_event_name = build_event_name(L"_Exit");
        position_mapping_name = build_event_name(L"_Position");

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

        position_mapping.reset(CreateFileMappingW(INVALID_HANDLE_VALUE, nullptr, PAGE_READWRITE, 0, sizeof(PositionPayload), position_mapping_name.c_str()));
        if (!position_mapping)
        {
            Logger::error(L"QuickAccessHost: failed to create position mapping. error={}.", GetLastError());
            reset_state_locked();
            return;
        }

        auto view = MapViewOfFile(position_mapping.get(), FILE_MAP_ALL_ACCESS, 0, 0, sizeof(PositionPayload));
        if (!view)
        {
            Logger::error(L"QuickAccessHost: failed to map position view. error={}.", GetLastError());
            reset_state_locked();
            return;
        }

        position_payload = static_cast<PositionPayload*>(view);
        position_payload->x = 0;
        position_payload->y = 0;
        position_payload->sequence = 0;

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

        BOOL created = CreateProcessW(exe_path.c_str(), command_line_buffer.data(), nullptr, nullptr, FALSE, 0, nullptr, nullptr, &startup_info, &process_info);
        if (!created)
        {
            Logger::error(L"QuickAccessHost: failed to launch Quick Access host. error={}.", GetLastError());
            reset_state_locked();
            return;
        }

        quick_access_process.reset(process_info.hProcess);
        CloseHandle(process_info.hThread);
    }

    void show(const POINT& position)
    {
        start();
        std::scoped_lock lock(quick_access_mutex);
        if (position_payload)
        {
            InterlockedIncrement(&position_payload->sequence);
            InterlockedExchange(&position_payload->x, position.x);
            InterlockedExchange(&position_payload->y, position.y);
            InterlockedIncrement(&position_payload->sequence);
        }

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
        std::unique_lock lock(quick_access_mutex);
        if (exit_event)
        {
            SetEvent(exit_event.get());
        }

        if (quick_access_process)
        {
            const DWORD wait_result = WaitForSingleObject(quick_access_process.get(), 2000);
            if (wait_result == WAIT_TIMEOUT)
            {
                Logger::warn(L"QuickAccessHost: Quick Access process did not exit in time, terminating.");
                if (!TerminateProcess(quick_access_process.get(), 0))
                {
                    Logger::error(L"QuickAccessHost: failed to terminate Quick Access process. error={}.", GetLastError());
                }
                else
                {
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
