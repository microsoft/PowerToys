#include "pch.h"
#include "quick_access_host.h"

#include <mutex>
#include <string>
#include <vector>

#include <common/logger/logger.h>
#include <common/utils/process_path.h>
#include <wil/resource.h>

namespace
{
    wil::unique_handle quick_access_process;
    wil::unique_handle show_event;
    wil::unique_handle exit_event;
    std::wstring show_event_name;
    std::wstring exit_event_name;
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
        quick_access_process.reset();
        show_event.reset();
        exit_event.reset();
        show_event_name.clear();
        exit_event_name.clear();
    }

    std::wstring build_event_name(const wchar_t* suffix)
    {
        return L"Local\\PowerToysQuickAccess_" + std::to_wstring(GetCurrentProcessId()) + suffix;
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
        std::unique_lock lock(quick_access_mutex);
        if (exit_event)
        {
            SetEvent(exit_event.get());
        }

        if (quick_access_process)
        {
            WaitForSingleObject(quick_access_process.get(), 2000);
        }

        reset_state_locked();
    }
}
