#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/utils/resources.h>
#include "Generated Files/resource.h"
#include <keyboardmanager/common/KeyboardManagerConstants.h>
#include <keyboardmanager/common/MappingConfiguration.h>
#include <common/utils/winapi_error.h>
#include <keyboardmanager/dll/trace.h>
#include <shellapi.h>
#include <common/utils/logger_helper.h>
#include <common/interop/shared_constants.h>
#include <thread>
#include <atomic>
#include <TlHelp32.h>
#include <future>
#include <iomanip>
#include <sstream>

BOOL APIENTRY DllMain(HMODULE /*hModule*/, DWORD ul_reason_for_call, LPVOID /*lpReserved*/)
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
        Trace::RegisterProvider();
        break;
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
        break;
    case DLL_PROCESS_DETACH:
        Trace::UnregisterProvider();
        break;
    }

    return TRUE;
}

namespace
{
    const wchar_t JSON_KEY_PROPERTIES[] = L"properties";
    const wchar_t JSON_KEY_WIN[] = L"win";
    const wchar_t JSON_KEY_ALT[] = L"alt";
    const wchar_t JSON_KEY_CTRL[] = L"ctrl";
    const wchar_t JSON_KEY_SHIFT[] = L"shift";
    const wchar_t JSON_KEY_CODE[] = L"code";
    const wchar_t JSON_KEY_ACTIVATION_SHORTCUT[] = L"ToggleShortcut";
    const wchar_t JSON_KEY_EDITOR_SHORTCUT[] = L"EditorShortcut";
    const wchar_t JSON_KEY_USE_NEW_EDITOR[] = L"useNewEditor";
    const wchar_t ACTION_ID_TOGGLE_ACTIVE[] = L"powertoys.keyboardManager.toggleActive";
    const wchar_t ACTION_ID_OPEN_EDITOR[] = L"powertoys.keyboardManager.openEditor";
    const wchar_t ACTION_ID_MAPPING_PREFIX[] = L"powertoys.keyboardManager.mapping.";
    const wchar_t ACTION_CATEGORY[] = L"Keyboard Manager";

    struct ActionInvokeResult
    {
        bool success = true;
        std::wstring error_code;
        std::wstring message;
    };

    struct handle_data
    {
        unsigned long process_id;
        HWND window_handle;
    };

    ActionInvokeResult action_error(std::wstring error_code, std::wstring message)
    {
        return ActionInvokeResult{ .success = false, .error_code = std::move(error_code), .message = std::move(message) };
    }

    bool is_keyboard_manager_custom_action(const KeyShortcutTextUnion& target)
    {
        if (target.index() != 1)
        {
            return false;
        }

        const auto& shortcut = std::get<Shortcut>(target);
        return shortcut.IsRunProgram() || shortcut.IsOpenURI();
    }

    BOOL CALLBACK enum_windows_callback_allow_non_visible(HWND handle, LPARAM l_param)
    {
        handle_data& data = *reinterpret_cast<handle_data*>(l_param);
        unsigned long process_id = 0;
        GetWindowThreadProcessId(handle, &process_id);

        if (data.process_id == process_id)
        {
            data.window_handle = handle;
            return FALSE;
        }

        return TRUE;
    }

    BOOL CALLBACK enum_windows_callback(HWND handle, LPARAM l_param)
    {
        handle_data& data = *reinterpret_cast<handle_data*>(l_param);
        unsigned long process_id = 0;
        GetWindowThreadProcessId(handle, &process_id);

        if (data.process_id != process_id || !(GetWindow(handle, GW_OWNER) == static_cast<HWND>(0) && IsWindowVisible(handle)))
        {
            return TRUE;
        }

        data.window_handle = handle;
        return FALSE;
    }

    HWND find_main_window(unsigned long process_id, bool allow_non_visible)
    {
        handle_data data{ .process_id = process_id, .window_handle = nullptr };
        EnumWindows(allow_non_visible ? enum_windows_callback_allow_non_visible : enum_windows_callback, reinterpret_cast<LPARAM>(&data));
        return data.window_handle;
    }

    std::vector<DWORD> get_processes_id_by_name(const std::wstring& process_name)
    {
        std::vector<DWORD> process_ids;
        HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snapshot == INVALID_HANDLE_VALUE)
        {
            return process_ids;
        }

        PROCESSENTRY32 process_entry{};
        process_entry.dwSize = sizeof(PROCESSENTRY32);
        if (Process32First(snapshot, &process_entry))
        {
            do
            {
                if (_wcsicmp(process_entry.szExeFile, process_name.c_str()) == 0)
                {
                    process_ids.push_back(process_entry.th32ProcessID);
                }
            } while (Process32Next(snapshot, &process_entry));
        }

        CloseHandle(snapshot);
        return process_ids;
    }

    DWORD get_process_id_by_name(const std::wstring& process_name)
    {
        const auto process_ids = get_processes_id_by_name(process_name);
        return process_ids.empty() ? 0 : process_ids.front();
    }

    std::wstring get_file_name_from_path(const std::wstring& full_path)
    {
        const size_t found = full_path.find_last_of(L"\\/");
        return found == std::wstring::npos ? full_path : full_path.substr(found + 1);
    }

    void close_process_by_name(const std::wstring& file_name_part)
    {
        auto process_ids = get_processes_id_by_name(file_name_part);
        if (process_ids.empty())
        {
            return;
        }

        std::thread([file_name_part]() {
            auto retry_count = 10;
            auto local_process_ids = get_processes_id_by_name(file_name_part);
            while (!local_process_ids.empty() && retry_count-- > 0)
            {
                for (DWORD pid : local_process_ids)
                {
                    HWND hwnd = find_main_window(pid, false);
                    if (hwnd)
                    {
                        SendMessage(hwnd, WM_CLOSE, 0, 0);
                    }

                    Sleep(10);
                }

                local_process_ids = get_processes_id_by_name(file_name_part);
                if (!local_process_ids.empty())
                {
                    Sleep(100);
                }
            }
        }).detach();
    }

    void terminate_processes_by_name(const std::wstring& file_name_part)
    {
        for (DWORD pid : get_processes_id_by_name(file_name_part))
        {
            HANDLE process = OpenProcess(PROCESS_TERMINATE, FALSE, pid);
            if (process)
            {
                TerminateProcess(process, 0);
                CloseHandle(process);
            }
        }
    }

    bool hide_program(DWORD pid, const std::wstring& program_name, int retry_count)
    {
        Logger::trace(L"KeyboardManager action: hide {} pid={} retry={}", program_name, pid, retry_count);

        HWND hwnd = find_main_window(pid, false);
        if (hwnd == nullptr && retry_count < 20)
        {
            auto future = std::async(std::launch::async, [=] {
                std::this_thread::sleep_for(std::chrono::milliseconds(50));
                return hide_program(pid, program_name, retry_count + 1);
            });
        }

        hwnd = FindWindow(nullptr, nullptr);
        while (hwnd)
        {
            DWORD pid_for_hwnd = 0;
            GetWindowThreadProcessId(hwnd, &pid_for_hwnd);
            if (pid == pid_for_hwnd && IsWindowVisible(hwnd))
            {
                ShowWindow(hwnd, SW_HIDE);
            }

            hwnd = FindWindowEx(nullptr, hwnd, nullptr, nullptr);
        }

        return true;
    }

    bool show_program(DWORD pid, const std::wstring& program_name, bool is_new_process, bool minimize_if_visible, int retry_count)
    {
        Logger::trace(L"KeyboardManager action: show {} pid={} retry={}", program_name, pid, retry_count);

        HWND hwnd = find_main_window(pid, false);
        if (hwnd == nullptr)
        {
            if (retry_count < 20)
            {
                auto future = std::async(std::launch::async, [=] {
                    std::this_thread::sleep_for(std::chrono::milliseconds(50));
                    return show_program(pid, program_name, is_new_process, minimize_if_visible, retry_count + 1);
                });
            }
        }
        else
        {
            if (hwnd == GetForegroundWindow())
            {
                if (!is_new_process && minimize_if_visible)
                {
                    return ShowWindow(hwnd, SW_MINIMIZE);
                }

                return false;
            }

            if (IsIconic(hwnd) && !ShowWindow(hwnd, SW_RESTORE))
            {
                Logger::warn(L"KeyboardManager action: failed restoring {}", program_name);
            }

            INPUT inputs[1] = { { .type = INPUT_MOUSE } };
            SendInput(ARRAYSIZE(inputs), inputs, sizeof(INPUT));
            return SetForegroundWindow(hwnd);
        }

        if (is_new_process)
        {
            return true;
        }

        hwnd = FindWindow(nullptr, nullptr);
        while (hwnd)
        {
            DWORD pid_for_hwnd = 0;
            GetWindowThreadProcessId(hwnd, &pid_for_hwnd);
            if (pid == pid_for_hwnd)
            {
                const int length = GetWindowTextLength(hwnd);
                if (length > 0)
                {
                    ShowWindow(hwnd, SW_RESTORE);
                    if (SetForegroundWindow(hwnd))
                    {
                        return true;
                    }
                }
            }

            hwnd = FindWindowEx(nullptr, hwnd, nullptr, nullptr);
        }

        return false;
    }

    std::wstring expand_environment_string(const std::wstring& value)
    {
        if (value.empty())
        {
            return {};
        }

        const DWORD size = ExpandEnvironmentStrings(value.c_str(), nullptr, 0);
        if (size == 0)
        {
            return value;
        }

        std::wstring expanded(size, L'\0');
        if (ExpandEnvironmentStrings(value.c_str(), expanded.data(), size) == 0)
        {
            return value;
        }

        if (!expanded.empty() && expanded.back() == L'\0')
        {
            expanded.pop_back();
        }

        return expanded;
    }

    std::wstring build_command_line(const std::wstring& file, const std::wstring& params)
    {
        std::wstring command_line = L"\"" + file + L"\"";
        if (!params.empty())
        {
            command_line += L" " + params;
        }

        return command_line;
    }

    bool launch_process_same_user(const std::wstring& file, const std::wstring& params, const wchar_t* working_directory, bool show_window, DWORD& process_id)
    {
        auto command_line = build_command_line(file, params);

        STARTUPINFOW startup_info{};
        startup_info.cb = sizeof(startup_info);
        DWORD creation_flags = 0;
        if (!show_window)
        {
            startup_info.dwFlags = STARTF_USESHOWWINDOW;
            startup_info.wShowWindow = SW_HIDE;
            creation_flags = CREATE_NO_WINDOW;
        }

        PROCESS_INFORMATION process_info{};
        const auto launched = CreateProcessW(
            file.c_str(),
            command_line.data(),
            nullptr,
            nullptr,
            FALSE,
            creation_flags,
            nullptr,
            working_directory,
            &startup_info,
            &process_info);

        if (!launched)
        {
            Logger::error(L"KeyboardManager action: CreateProcessW failed. {}", get_last_error_or_default(GetLastError()));
            return false;
        }

        process_id = process_info.hProcess ? GetProcessId(process_info.hProcess) : 0;
        if (process_info.hThread)
        {
            CloseHandle(process_info.hThread);
        }

        if (process_info.hProcess)
        {
            CloseHandle(process_info.hProcess);
        }

        return process_id != 0;
    }

    bool launch_process_shell_execute(const wchar_t* verb, const std::wstring& file, const std::wstring& params, const wchar_t* working_directory, bool show_window, DWORD& process_id)
    {
        SHELLEXECUTEINFOW execute_info{};
        execute_info.cbSize = sizeof(execute_info);
        execute_info.fMask = SEE_MASK_NOCLOSEPROCESS;
        execute_info.lpVerb = verb;
        execute_info.lpFile = file.c_str();
        execute_info.lpParameters = params.empty() ? nullptr : params.c_str();
        execute_info.lpDirectory = working_directory;
        execute_info.nShow = show_window ? SW_SHOWDEFAULT : SW_HIDE;

        if (!ShellExecuteExW(&execute_info))
        {
            Logger::error(L"KeyboardManager action: ShellExecuteExW failed. {}", get_last_error_or_default(GetLastError()));
            return false;
        }

        process_id = execute_info.hProcess ? GetProcessId(execute_info.hProcess) : 0;
        if (execute_info.hProcess)
        {
            CloseHandle(execute_info.hProcess);
        }

        return process_id != 0;
    }

    ActionInvokeResult invoke_run_program_action(const Shortcut& shortcut)
    {
        const std::wstring full_expanded_file_path = expand_environment_string(shortcut.runProgramFilePath);
        const std::wstring file_name_part = get_file_name_from_path(full_expanded_file_path);

        const DWORD target_pid = get_process_id_by_name(file_name_part);
        if (target_pid != 0 && shortcut.alreadyRunningAction != Shortcut::ProgramAlreadyRunningAction::StartAnother)
        {
            if (shortcut.alreadyRunningAction == Shortcut::ProgramAlreadyRunningAction::EndTask)
            {
                terminate_processes_by_name(file_name_part);
            }
            else if (shortcut.alreadyRunningAction == Shortcut::ProgramAlreadyRunningAction::Close)
            {
                close_process_by_name(file_name_part);
            }
            else if (shortcut.alreadyRunningAction == Shortcut::ProgramAlreadyRunningAction::ShowWindow)
            {
                const auto process_ids = get_processes_id_by_name(file_name_part);
                for (DWORD pid : process_ids)
                {
                    show_program(pid, file_name_part, false, false, 0);
                }
            }

            return {};
        }

        if (GetFileAttributesW(full_expanded_file_path.c_str()) == INVALID_FILE_ATTRIBUTES)
        {
            return action_error(L"program_not_found", L"The program '" + file_name_part + L"' was not found.");
        }

        const std::wstring expanded_args = expand_environment_string(shortcut.runProgramArgs);
        const std::wstring expanded_start_dir = expand_environment_string(shortcut.runProgramStartInDir);
        const wchar_t* current_dir_ptr = expanded_start_dir.empty() ? nullptr : expanded_start_dir.c_str();

        if (current_dir_ptr && GetFileAttributesW(current_dir_ptr) == INVALID_FILE_ATTRIBUTES)
        {
            return action_error(L"invalid_start_directory", L"The configured start-in path is invalid.");
        }

        DWORD process_id = 0;
        const bool show_window = shortcut.startWindowType == Shortcut::StartWindowType::Normal;
        bool launched = false;
        if (shortcut.elevationLevel == Shortcut::ElevationLevel::Elevated)
        {
            launched = launch_process_shell_execute(L"runas", full_expanded_file_path, expanded_args, current_dir_ptr, show_window, process_id);
        }
        else if (shortcut.elevationLevel == Shortcut::ElevationLevel::DifferentUser)
        {
            launched = launch_process_shell_execute(L"runAsUser", full_expanded_file_path, expanded_args, current_dir_ptr, show_window, process_id);
        }
        else
        {
            launched = launch_process_same_user(full_expanded_file_path, expanded_args, current_dir_ptr, show_window, process_id);
        }

        if (!launched || process_id == 0)
        {
            return action_error(L"launch_failed", L"The application might not have started.");
        }

        if (shortcut.startWindowType == Shortcut::StartWindowType::Hidden)
        {
            hide_program(process_id, file_name_part, 0);
        }

        return {};
    }

    ActionInvokeResult invoke_open_uri_action(const Shortcut& shortcut)
    {
        std::wstring target = shortcut.uriToOpen;
        if (target.empty())
        {
            return action_error(L"invalid_uri", L"The configured path or URI is empty.");
        }

        if (!PathIsURLW(target.c_str()))
        {
            wchar_t url[2048]{};
            DWORD buffer_size = ARRAYSIZE(url);
            if (UrlCreateFromPathW(target.c_str(), url, &buffer_size, 0) != S_OK)
            {
                return action_error(L"invalid_uri", L"Could not understand the configured path or URI.");
            }

            target = url;
        }

        const auto result = reinterpret_cast<INT_PTR>(ShellExecuteW(nullptr, L"open", target.c_str(), nullptr, nullptr, SW_SHOWNORMAL));
        if (result <= 32)
        {
            return action_error(L"invoke_failed", L"Could not open the configured path or URI.");
        }

        return {};
    }

    ActionInvokeResult invoke_keyboard_manager_custom_action(const Shortcut& shortcut)
    {
        if (shortcut.IsRunProgram())
        {
            return invoke_run_program_action(shortcut);
        }

        if (shortcut.IsOpenURI())
        {
            return invoke_open_uri_action(shortcut);
        }

        return action_error(L"unsupported_action", L"Keyboard Manager only exposes Run Program and Open URL mappings as invokable actions.");
    }

    uint64_t fnv1a_hash(const std::wstring& value)
    {
        uint64_t hash = 1469598103934665603ull;
        for (wchar_t ch : value)
        {
            hash ^= static_cast<uint64_t>(ch);
            hash *= 1099511628211ull;
        }

        return hash;
    }

    std::wstring mapping_action_id(const Shortcut& source, const Shortcut& target, const std::wstring& app)
    {
        std::wstringstream identity;
        identity << source.ToHstringVK().c_str() << L'|' << source.exactMatch << L'|' << app << L'|' << static_cast<int>(target.operationType) << L'|';

        if (target.IsRunProgram())
        {
            identity << target.runProgramFilePath << L'|' << target.runProgramArgs << L'|' << target.runProgramStartInDir << L'|' << static_cast<int>(target.elevationLevel) << L'|' << static_cast<int>(target.alreadyRunningAction) << L'|' << static_cast<int>(target.startWindowType);
        }
        else if (target.IsOpenURI())
        {
            identity << target.uriToOpen;
        }

        std::wstringstream action_id;
        action_id << ACTION_ID_MAPPING_PREFIX << std::hex << std::setfill(L'0') << std::setw(16) << fnv1a_hash(identity.str());
        return action_id.str();
    }

    std::wstring mapping_action_display_name(const Shortcut& target)
    {
        if (target.IsRunProgram())
        {
            const auto expanded_path = expand_environment_string(target.runProgramFilePath);
            const auto file_name = get_file_name_from_path(expanded_path.empty() ? target.runProgramFilePath : expanded_path);
            return file_name.empty() ? L"Run Keyboard Manager program action" : (L"Run " + file_name);
        }

        if (target.IsOpenURI())
        {
            return L"Open " + target.uriToOpen;
        }

        return L"Keyboard Manager action";
    }

    std::wstring mapping_action_description(const Shortcut& source, const Shortcut& target, const std::wstring& app)
    {
        const std::wstring scope = app.empty() ? L"Global" : (L"App-specific (" + app + L")");
        const std::wstring target_description = target.IsRunProgram() ? target.runProgramFilePath : target.uriToOpen;
        return scope + L" Keyboard Manager shortcut " + source.ToHstringVK().c_str() + L" -> " + target_description;
    }

    void append_mapping_action(json::JsonArray& actions, const Shortcut& source, const Shortcut& target, const std::wstring& app)
    {
        json::JsonObject action;
        action.SetNamedValue(L"action_id", json::value(mapping_action_id(source, target, app)));
        action.SetNamedValue(L"display_name", json::value(mapping_action_display_name(target)));
        action.SetNamedValue(L"description", json::value(mapping_action_description(source, target, app)));
        action.SetNamedValue(L"category", json::value(ACTION_CATEGORY));
        action.SetNamedValue(L"argument_definitions", json::JsonArray{});
        actions.Append(action);
    }

    void append_mapping_actions(json::JsonArray& actions, const MappingConfiguration& config)
    {
        for (const auto& [source, remap] : config.osLevelShortcutReMap)
        {
            if (is_keyboard_manager_custom_action(remap.targetShortcut))
            {
                append_mapping_action(actions, source, std::get<Shortcut>(remap.targetShortcut), L"");
            }
        }

        for (const auto& [app, remaps] : config.appSpecificShortcutReMap)
        {
            for (const auto& [source, remap] : remaps)
            {
                if (is_keyboard_manager_custom_action(remap.targetShortcut))
                {
                    append_mapping_action(actions, source, std::get<Shortcut>(remap.targetShortcut), app);
                }
            }
        }
    }

    std::optional<Shortcut> find_mapping_action(const std::wstring& action_id)
    {
        MappingConfiguration config;
        if (!config.LoadSettings())
        {
            return std::nullopt;
        }

        for (const auto& [source, remap] : config.osLevelShortcutReMap)
        {
            if (!is_keyboard_manager_custom_action(remap.targetShortcut))
            {
                continue;
            }

            const auto& target = std::get<Shortcut>(remap.targetShortcut);
            if (mapping_action_id(source, target, L"") == action_id)
            {
                return target;
            }
        }

        for (const auto& [app, remaps] : config.appSpecificShortcutReMap)
        {
            for (const auto& [source, remap] : remaps)
            {
                if (!is_keyboard_manager_custom_action(remap.targetShortcut))
                {
                    continue;
                }

                const auto& target = std::get<Shortcut>(remap.targetShortcut);
                if (mapping_action_id(source, target, app) == action_id)
                {
                    return target;
                }
            }
        }

        return std::nullopt;
    }

    bool write_marshaled_json(const std::wstring& json, wchar_t* buffer, int* buffer_size)
    {
        if (!buffer_size)
        {
            return false;
        }

        const int required_size = static_cast<int>(json.size() + 1);
        if (!buffer || *buffer_size < required_size)
        {
            *buffer_size = required_size;
            return false;
        }

        wcscpy_s(buffer, *buffer_size, json.c_str());
        return true;
    }
}

// Implement the PowerToy Module Interface and all the required methods.
class KeyboardManager : public PowertoyModuleIface
{
private:
    // The PowerToy state.
    bool m_enabled = false;
    bool m_active = false;

    // The PowerToy name that will be shown in the settings.
    const std::wstring app_name = GET_RESOURCE_STRING(IDS_KEYBOARDMANAGER);

    //contains the non localized key of the powertoy
    std::wstring app_key = KeyboardManagerConstants::ModuleName;

    // Hotkey for toggling the module
    Hotkey m_hotkey = { .key = 0 };

    // Hotkey for opening the editor
    Hotkey m_editorHotkey = { .key = 0 };

    // Whether to use the new WinUI3 editor
    bool m_useNewEditor = false;

    ULONGLONG m_lastHotkeyToggleTime = 0;

    HANDLE m_hProcess = nullptr;
    HANDLE m_hEditorProcess = nullptr;

    HANDLE m_hTerminateEngineEvent = nullptr;
    HANDLE m_open_new_editor_event_handle{ nullptr };
    HANDLE m_toggle_active_event_handle{ nullptr };
    std::thread m_toggle_thread;
    std::atomic<bool> m_toggle_thread_running{ false };


    void refresh_process_state()
    {
        if (m_hProcess && WaitForSingleObject(m_hProcess, 0) != WAIT_TIMEOUT)
        {
            CloseHandle(m_hProcess);
            m_hProcess = nullptr;
            m_active = false;
        }
    }

    void toggle_engine()
    {
        refresh_process_state();
        if (m_active)
        {
            stop_engine();
        }
        else
        {
            start_engine();
        }
    }

    bool start_engine()
    {
        refresh_process_state();
        if (m_hProcess)
        {
            m_active = true;
            return true;
        }

        if (!m_hTerminateEngineEvent)
        {
            Logger::error(L"Cannot start keyboard manager engine because terminate event is not available");
            m_active = false;
            return false;
        }

        unsigned long powertoys_pid = GetCurrentProcessId();
        std::wstring executable_args = std::to_wstring(powertoys_pid);

        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = { SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI };
        sei.lpFile = L"KeyboardManagerEngine\\PowerToys.KeyboardManagerEngine.exe";
        sei.nShow = SW_SHOWNORMAL;
        sei.lpParameters = executable_args.data();
        if (ShellExecuteExW(&sei) == false)
        {
            Logger::error(L"Failed to start keyboard manager engine");
            auto message = get_last_error_message(GetLastError());
            if (message.has_value())
            {
                Logger::error(message.value());
            }

            m_active = false;
            return false;
        }

        m_hProcess = sei.hProcess;
        if (m_hProcess)
        {
            SetPriorityClass(m_hProcess, REALTIME_PRIORITY_CLASS);
            m_active = true;
            return true;
        }

        m_active = false;
        return false;
    }

    void stop_engine()
    {
        refresh_process_state();
        if (!m_hProcess)
        {
            m_active = false;
            return;
        }

        SetEvent(m_hTerminateEngineEvent);
        auto waitResult = WaitForSingleObject(m_hProcess, 1500);
        if (waitResult == WAIT_TIMEOUT)
        {
            TerminateProcess(m_hProcess, 0);
            WaitForSingleObject(m_hProcess, 500);
        }

        CloseHandle(m_hProcess);
        m_hProcess = nullptr;
        ResetEvent(m_hTerminateEngineEvent);
        m_active = false;
    }

    void parse_hotkey(PowerToysSettings::PowerToyValues& settings)
    {
        auto settingsObject = settings.get_raw_json();
        if (settingsObject.GetView().Size())
        {
            try
            {
                auto jsonHotkeyObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES)
                                            .GetNamedObject(JSON_KEY_ACTIVATION_SHORTCUT);
                m_hotkey.win = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_WIN);
                m_hotkey.alt = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_ALT);
                m_hotkey.shift = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_SHIFT);
                m_hotkey.ctrl = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_CTRL);
                m_hotkey.key = static_cast<unsigned char>(jsonHotkeyObject.GetNamedNumber(JSON_KEY_CODE));
            }
            catch (...)
            {
                Logger::error("Failed to initialize Keyboard Manager toggle shortcut");
            }
        }

        if (!m_hotkey.key)
        {
            // Set default: Win+Shift+K
            m_hotkey.win = true;
            m_hotkey.shift = true;
            m_hotkey.ctrl = false;
            m_hotkey.alt = false;
            m_hotkey.key = 'K';
        }

        // Parse editor shortcut
        if (settingsObject.GetView().Size())
        {
            try
            {
                auto jsonEditorHotkeyObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES)
                                                  .GetNamedObject(JSON_KEY_EDITOR_SHORTCUT);
                m_editorHotkey.win = jsonEditorHotkeyObject.GetNamedBoolean(JSON_KEY_WIN);
                m_editorHotkey.alt = jsonEditorHotkeyObject.GetNamedBoolean(JSON_KEY_ALT);
                m_editorHotkey.shift = jsonEditorHotkeyObject.GetNamedBoolean(JSON_KEY_SHIFT);
                m_editorHotkey.ctrl = jsonEditorHotkeyObject.GetNamedBoolean(JSON_KEY_CTRL);
                m_editorHotkey.key = static_cast<unsigned char>(jsonEditorHotkeyObject.GetNamedNumber(JSON_KEY_CODE));
            }
            catch (...)
            {
                Logger::error("Failed to initialize Keyboard Manager editor shortcut");
            }
        }

        if (!m_editorHotkey.key)
        {
            // Set default: Win+Shift+Q
            m_editorHotkey.win = true;
            m_editorHotkey.shift = true;
            m_editorHotkey.ctrl = false;
            m_editorHotkey.alt = false;
            m_editorHotkey.key = 'Q';
        }

        // Parse useNewEditor setting
        if (settingsObject.GetView().Size())
        {
            try
            {
                auto propertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES);
                m_useNewEditor = propertiesObject.GetNamedBoolean(JSON_KEY_USE_NEW_EDITOR, false);
            }
            catch (...)
            {
                Logger::warn("Failed to parse useNewEditor setting, defaulting to false");
            }
        }
    }

    // Load the settings file.
    void init_settings()
    {
        try
        {
            // Load and parse the settings file for this PowerToy.
            PowerToysSettings::PowerToyValues settings =
                PowerToysSettings::PowerToyValues::load_from_settings_file(get_key());
            parse_hotkey(settings);
        }
        catch (std::exception&)
        {
            Logger::warn(L"An exception occurred while loading the settings file");
            // Error while loading from the settings file. Let default values stay as they are.
        }
    }

public:
    // Constructor
    KeyboardManager()
    {
        LoggerHelpers::init_logger(KeyboardManagerConstants::ModuleName, L"ModuleInterface", LogSettings::keyboardManagerLoggerName);

        std::filesystem::path oldLogPath(PTSettingsHelper::get_module_save_folder_location(app_key));
        oldLogPath.append("Logs");
        LoggerHelpers::delete_old_log_folder(oldLogPath);

        m_hTerminateEngineEvent = CreateDefaultEvent(CommonSharedConstants::TERMINATE_KBM_SHARED_EVENT);
        if (!m_hTerminateEngineEvent)
        {
            Logger::error(L"Failed to create terminate Engine event");
            auto message = get_last_error_message(GetLastError());
            if (message.has_value())
            {
                Logger::error(message.value());
            }
        }

        m_open_new_editor_event_handle = CreateDefaultEvent(CommonSharedConstants::OPEN_NEW_KEYBOARD_MANAGER_EVENT);
        m_toggle_active_event_handle = CreateDefaultEvent(CommonSharedConstants::TOGGLE_KEYBOARD_MANAGER_ACTIVE_EVENT);

        init_settings();
    };

    ~KeyboardManager()
    {
        StopOpenEditorListener();
        stop_engine();
        if (m_hTerminateEngineEvent)
        {
            CloseHandle(m_hTerminateEngineEvent);
            m_hTerminateEngineEvent = nullptr;
        }
        if (m_open_new_editor_event_handle)
        {
            CloseHandle(m_open_new_editor_event_handle);
            m_open_new_editor_event_handle = nullptr;
        }
        if (m_toggle_active_event_handle)
        {
            CloseHandle(m_toggle_active_event_handle);
            m_toggle_active_event_handle = nullptr;
        }
        if (m_hEditorProcess)
        {
            CloseHandle(m_hEditorProcess);
            m_hEditorProcess = nullptr;
        }
    }

    // Destroy the powertoy and free memory
    virtual void destroy() override
    {
        delete this;
    }

    // Return the localized display name of the powertoy
    virtual const wchar_t* get_name() override
    {
        return app_name.c_str();
    }

    // Return the non localized key of the powertoy, this will be cached by the runner
    virtual const wchar_t* get_key() override
    {
        return app_key.c_str();
    }

    // Return the configured status for the gpo policy for the module
    virtual powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
    {
        return powertoys_gpo::getConfiguredKeyboardManagerEnabledValue();
    }

    // Return JSON with the configuration options.
    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        // Create a Settings object.
        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.set_description(IDS_SETTINGS_DESCRIPTION);
        settings.set_overview_link(L"https://aka.ms/PowerToysOverview_KeyboardManager");

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    // Signal from the Settings editor to call a custom action.
    virtual void call_custom_action(const wchar_t* /*action*/) override
    {
    }

    virtual bool get_actions(wchar_t* buffer, int* buffer_size) override
    {
        json::JsonArray actions;

        json::JsonObject toggle_action;
        toggle_action.SetNamedValue(L"action_id", json::value(ACTION_ID_TOGGLE_ACTIVE));
        toggle_action.SetNamedValue(L"display_name", json::value(L"Toggle Keyboard Manager active state"));
        toggle_action.SetNamedValue(L"description", json::value(L"Turns the Keyboard Manager engine on or off."));
        toggle_action.SetNamedValue(L"category", json::value(L"Keyboard Manager"));
        toggle_action.SetNamedValue(L"argument_definitions", json::JsonArray{});
        actions.Append(toggle_action);

        json::JsonObject open_editor_action;
        open_editor_action.SetNamedValue(L"action_id", json::value(ACTION_ID_OPEN_EDITOR));
        open_editor_action.SetNamedValue(L"display_name", json::value(L"Open Keyboard Manager editor"));
        open_editor_action.SetNamedValue(L"description", json::value(L"Opens the new Keyboard Manager editor window."));
        open_editor_action.SetNamedValue(L"category", json::value(L"Keyboard Manager"));
        open_editor_action.SetNamedValue(L"argument_definitions", json::JsonArray{});
        actions.Append(open_editor_action);

        MappingConfiguration config;
        if (config.LoadSettings())
        {
            append_mapping_actions(actions, config);
        }

        return write_marshaled_json(actions.Stringify().c_str(), buffer, buffer_size);
    }

    virtual bool invoke_action(const wchar_t* action_id, const wchar_t* /*serialized_args*/, wchar_t* buffer, int* buffer_size) override
    {
        json::JsonObject result;
        result.SetNamedValue(L"success", json::JsonValue::CreateBooleanValue(true));

        std::wstring requested_action = action_id ? action_id : L"";
        if (requested_action == ACTION_ID_TOGGLE_ACTIVE)
        {
            toggle_engine();
        }
        else if (requested_action == ACTION_ID_OPEN_EDITOR)
        {
            if (!launch_editor())
            {
                result.SetNamedValue(L"success", json::JsonValue::CreateBooleanValue(false));
                result.SetNamedValue(L"error_code", json::value(L"launch_failed"));
                result.SetNamedValue(L"message", json::value(L"Failed to open the Keyboard Manager editor."));
            }
        }
        else if (requested_action.rfind(ACTION_ID_MAPPING_PREFIX, 0) == 0)
        {
            const auto mapped_action = find_mapping_action(requested_action);
            if (!mapped_action.has_value())
            {
                result.SetNamedValue(L"success", json::JsonValue::CreateBooleanValue(false));
                result.SetNamedValue(L"error_code", json::value(L"action_not_found"));
                result.SetNamedValue(L"message", json::value(L"The requested Keyboard Manager shortcut action no longer exists."));
            }
            else
            {
                const auto action_result = invoke_keyboard_manager_custom_action(*mapped_action);
                if (!action_result.success)
                {
                    result.SetNamedValue(L"success", json::JsonValue::CreateBooleanValue(false));
                    result.SetNamedValue(L"error_code", json::value(action_result.error_code));
                    result.SetNamedValue(L"message", json::value(action_result.message));
                }
            }
        }
        else
        {
            result.SetNamedValue(L"success", json::JsonValue::CreateBooleanValue(false));
            result.SetNamedValue(L"error_code", json::value(L"unsupported_action"));
            result.SetNamedValue(L"message", json::value(L"Keyboard Manager does not recognize the requested action."));
        }

        return write_marshaled_json(result.Stringify().c_str(), buffer, buffer_size);
    }

    // Called by the runner to pass the updated settings values as a serialized JSON.
    virtual void set_config(const wchar_t* config) override
    {
        try
        {
            // Parse the input JSON string.
            PowerToysSettings::PowerToyValues values =
                PowerToysSettings::PowerToyValues::from_json_string(config, get_key());
            parse_hotkey(values);

            // If you don't need to do any custom processing of the settings, proceed
            // to persists the values calling:
            values.save_to_settings_file();
        }
        catch (std::exception&)
        {
            // Improper JSON.
        }
    }

    // Enable the powertoy
    virtual void enable()
    {
        m_enabled = true;
        // Log telemetry
        Trace::EnableKeyboardManager(true);
        start_engine();
        StartOpenEditorListener();
    }

    // Disable the powertoy
    virtual void disable()
    {
        m_enabled = false;
        // Log telemetry
        Trace::EnableKeyboardManager(false);
        StopOpenEditorListener();
        stop_engine();
    }

    // Returns if the powertoys is enabled
    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    // Returns whether the PowerToys should be enabled by default
    virtual bool is_enabled_by_default() const override
    {
        return false;
    }

    // Return the invocation hotkeys for toggling and opening the editor
    virtual size_t get_hotkeys(Hotkey* hotkeys, size_t buffer_size) override
    {
        size_t count = 0;

        // Hotkey 0: toggle engine
        if (m_hotkey.key)
        {
            if (hotkeys && buffer_size > count)
            {
                hotkeys[count] = m_hotkey;
            }
            count++;
        }

        // Hotkey 1: open editor (only when using new editor)
        if (m_useNewEditor && m_editorHotkey.key)
        {
            if (hotkeys && buffer_size > count)
            {
                hotkeys[count] = m_editorHotkey;
            }
            count++;
        }

        return count;
    }

    void StartOpenEditorListener()
    {
        if (m_toggle_thread_running || (!m_open_new_editor_event_handle && !m_toggle_active_event_handle))
        {
            return;
        }

        m_toggle_thread_running = true;
        m_toggle_thread = std::thread([this]() {
            HANDLE handles[2]{};
            DWORD handle_count = 0;
            DWORD open_editor_index = MAXDWORD;
            DWORD toggle_active_index = MAXDWORD;

            if (m_open_new_editor_event_handle)
            {
                open_editor_index = handle_count;
                handles[handle_count++] = m_open_new_editor_event_handle;
            }

            if (m_toggle_active_event_handle)
            {
                toggle_active_index = handle_count;
                handles[handle_count++] = m_toggle_active_event_handle;
            }

            while (m_toggle_thread_running)
            {
                const DWORD wait_result = WaitForMultipleObjects(handle_count, handles, FALSE, 500);
                if (!m_toggle_thread_running)
                {
                    break;
                }

                if (open_editor_index != MAXDWORD && wait_result == (WAIT_OBJECT_0 + open_editor_index))
                {
                    launch_editor();
                }
                else if (toggle_active_index != MAXDWORD && wait_result == (WAIT_OBJECT_0 + toggle_active_index))
                {
                    toggle_engine();
                }
            }
        });
    }

    void StopOpenEditorListener()
    {
        if (!m_toggle_thread_running)
        {
            return;
        }

        m_toggle_thread_running = false;
        if (m_open_new_editor_event_handle)
        {
            SetEvent(m_open_new_editor_event_handle);
        }
        if (m_toggle_active_event_handle)
        {
            SetEvent(m_toggle_active_event_handle);
        }
        if (m_toggle_thread.joinable())
        {
            m_toggle_thread.join();
        }
    }

    bool launch_editor()
    {
        // Check if editor is already running
        if (m_hEditorProcess)
        {
            if (WaitForSingleObject(m_hEditorProcess, 0) == WAIT_TIMEOUT)
            {
                // Editor still running, bring it to front
                DWORD editorPid = GetProcessId(m_hEditorProcess);
                if (editorPid)
                {
                    AllowSetForegroundWindow(editorPid);

                    // Find the editor's main window and set it to foreground
                    EnumWindows([](HWND hwnd, LPARAM lParam) -> BOOL {
                        DWORD windowPid = 0;
                        GetWindowThreadProcessId(hwnd, &windowPid);
                        if (windowPid == static_cast<DWORD>(lParam) && IsWindowVisible(hwnd))
                        {
                            SetForegroundWindow(hwnd);
                            if (IsIconic(hwnd))
                            {
                                ShowWindow(hwnd, SW_RESTORE);
                            }
                            return FALSE; // Stop enumerating
                        }
                        return TRUE;
                    }, static_cast<LPARAM>(editorPid));
                }
                return true;
            }
            else
            {
                CloseHandle(m_hEditorProcess);
                m_hEditorProcess = nullptr;
            }
        }

        unsigned long powertoys_pid = GetCurrentProcessId();
        std::wstring executable_args = std::to_wstring(powertoys_pid);

        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = { SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI };
        sei.lpFile = L"WinUI3Apps\\PowerToys.KeyboardManagerEditorUI.exe";
        sei.nShow = SW_SHOWNORMAL;
        sei.lpParameters = executable_args.data();
        if (ShellExecuteExW(&sei) == false)
        {
            Logger::error(L"Failed to start new keyboard manager editor");
            auto message = get_last_error_message(GetLastError());
            if (message.has_value())
            {
                Logger::error(message.value());
            }
            return false;
        }

        m_hEditorProcess = sei.hProcess;
        
        // Log telemetry for editor launch
        if (m_hEditorProcess)
        {
            Trace::LaunchEditor(true); // true = launched via hotkey/event
        }
        
        return m_hEditorProcess != nullptr;
    }

    // Process the hotkey event
    virtual bool on_hotkey(size_t hotkeyId) override
    {
        if (!m_enabled)
        {
            return false;
        }

        constexpr ULONGLONG hotkeyToggleDebounceMs = 500;
        const auto now = GetTickCount64();
        if (now - m_lastHotkeyToggleTime < hotkeyToggleDebounceMs)
        {
            return true;
        }
        m_lastHotkeyToggleTime = now;

        if (hotkeyId == 0)
        {
            // Toggle engine on/off
            toggle_engine();
        }
        else if (hotkeyId == 1)
        {
            // Open the new editor (only in new editor mode)
            launch_editor();
        }

        return true;
    }
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new KeyboardManager();
}

