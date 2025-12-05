#include "pch.h"
#include <WinSafer.h>
#include <Sddl.h>
#include <sstream>
#include <aclapi.h>

#include "powertoy_module.h"
#include <common/interop/two_way_pipe_message_ipc.h>
#include <common/interop/shared_constants.h>
#include "tray_icon.h"
#include "general_settings.h"
#include "restart_elevated.h"
#include "UpdateUtils.h"
#include "centralized_kb_hook.h"
#include "Generated files/resource.h"
#include "hotkey_conflict_detector.h"

#include <common/utils/json.h>
#include <common/SettingsAPI/settings_helpers.cpp>
#include <common/version/version.h>
#include <common/version/helper.h>
#include <common/logger/logger.h>
#include <common/utils/resources.h>
#include <common/utils/elevation.h>
#include <common/utils/process_path.h>
#include <common/utils/timeutil.h>
#include <common/utils/winapi_error.h>
#include <common/updating/updateState.h>
#include <common/themes/windows_colors.h>
#include "settings_window.h"
#include "bug_report.h"

#define BUFSIZE 1024

TwoWayPipeMessageIPC* current_settings_ipc = NULL;
std::mutex ipc_mutex;
std::atomic_bool g_isLaunchInProgress = false;
std::atomic_bool isUpdateCheckThreadRunning = false;
HANDLE g_terminateSettingsEvent = CreateEventW(nullptr, false, false, CommonSharedConstants::TERMINATE_SETTINGS_SHARED_EVENT);

json::JsonObject get_power_toys_settings()
{
    json::JsonObject result;
    for (const auto& [name, powertoy] : modules())
    {
        try
        {
            result.SetNamedValue(name, powertoy.json_config());
        }
        catch (...)
        {
            Logger::error(L"get_power_toys_settings(): got malformed json for {} module", name);
        }
    }
    return result;
}

json::JsonObject get_all_settings()
{
    json::JsonObject result;

    result.SetNamedValue(L"general", get_general_settings().to_json());
    result.SetNamedValue(L"powertoys", get_power_toys_settings());
    return result;
}

std::optional<std::wstring> dispatch_json_action_to_module(const json::JsonObject& powertoys_configs)
{
    std::optional<std::wstring> result;
    for (const auto& powertoy_element : powertoys_configs)
    {
        const std::wstring name{ powertoy_element.Key().c_str() };
        // Currently, there is only one custom action in the general settings screen,
        // so it has to be the "restart as (non-)elevated" button.
        if (name == L"general")
        {
            try
            {
                const auto value = powertoy_element.Value().GetObjectW();
                const auto action = value.GetNamedString(L"action_name");
                if (action == L"restart_elevation")
                {
                    if (is_process_elevated())
                    {
                        schedule_restart_as_non_elevated();
                        PostQuitMessage(0);
                    }
                    else
                    {
                        schedule_restart_as_elevated(true);
                        PostQuitMessage(0);
                    }
                }
                else if (action == L"restart_maintain_elevation")
                {
                    // this was added to restart and maintain elevation, which is needed after settings are change from outside the normal process.
                    // since a normal PostQuitMessage(0) would usually cause this process to save its in memory settings to disk, we need to
                    // send a PostQuitMessage(1) and check for that on exit, and skip the settings-flush.
                    auto loaded = PTSettingsHelper::load_general_settings();

                    if (is_process_elevated())
                    {
                        schedule_restart_as_elevated(true);
                        PostQuitMessage(1);
                    }
                    else
                    {
                        schedule_restart_as_non_elevated(true);
                        PostQuitMessage(1);
                    }
                }
                else if (action == L"check_for_updates")
                {
                    bool expected_isUpdateCheckThreadRunning = false;
                    if (isUpdateCheckThreadRunning.compare_exchange_strong(expected_isUpdateCheckThreadRunning, true))
                    {
                        std::thread([]() {
                            CheckForUpdatesCallback();
                            isUpdateCheckThreadRunning.store(false);
                        }).detach();
                    }
                }
                else if (action == L"request_update_state_date")
                {
                    json::JsonObject json;

                    auto update_state = UpdateState::read();
                    if (update_state.githubUpdateLastCheckedDate)
                    {
                        const time_t date = *update_state.githubUpdateLastCheckedDate;
                        json.SetNamedValue(L"updateStateDate", json::value(std::to_wstring(date)));
                    }

                    result.emplace(json.Stringify());
                }
            }
            catch (...)
            {
            }
        }
        else if (modules().find(name) != modules().end())
        {
            const auto element = powertoy_element.Value().Stringify();
            modules().at(name)->call_custom_action(element.c_str());
        }
    }

    return result;
}

void send_json_config_to_module(const std::wstring& module_key, const std::wstring& settings)
{
    auto moduleIt = modules().find(module_key);
    if (moduleIt != modules().end())
    {
        moduleIt->second->set_config(settings.c_str());

        moduleIt->second.remove_hotkey_records();
        moduleIt->second.update_hotkeys();
        moduleIt->second.UpdateHotkeyEx();
    }
}

void dispatch_json_config_to_modules(const json::JsonObject& powertoys_configs)
{
    for (const auto& powertoy_element : powertoys_configs)
    {
        const auto element = powertoy_element.Value().Stringify();
        send_json_config_to_module(powertoy_element.Key().c_str(), element.c_str());
    }
};

void dispatch_received_json(const std::wstring& json_to_parse)
{
    json::JsonObject j;
    const bool ok = json::JsonObject::TryParse(json_to_parse, j);
    if (!ok)
    {
        Logger::error(L"dispatch_received_json: got malformed json: {}", json_to_parse);
        return;
    }

    for (const auto& base_element : j)
    {
        const auto name = base_element.Key();
        const auto value = base_element.Value();

        if (name == L"general")
        {
            apply_general_settings(value.GetObjectW());
            const std::wstring settings_string{ get_all_settings().Stringify().c_str() };
            {
                std::unique_lock lock{ ipc_mutex };
                if (current_settings_ipc)
                    current_settings_ipc->send(settings_string);
            }
        }
        else if (name == L"powertoys")
        {
            dispatch_json_config_to_modules(value.GetObjectW());
            const std::wstring settings_string{ get_all_settings().Stringify().c_str() };
            {
                std::unique_lock lock{ ipc_mutex };
                if (current_settings_ipc)
                    current_settings_ipc->send(settings_string);
            }
        }
        else if (name == L"refresh")
        {
            const std::wstring settings_string{ get_all_settings().Stringify().c_str() };
            {
                std::unique_lock lock{ ipc_mutex };
                if (current_settings_ipc)
                    current_settings_ipc->send(settings_string);
            }
        }
        else if (name == L"action")
        {
            auto result = dispatch_json_action_to_module(value.GetObjectW());
            if (result.has_value())
            {
                {
                    std::unique_lock lock{ ipc_mutex };
                    if (current_settings_ipc)
                        current_settings_ipc->send(result.value());
                }
            }
        }
        else if (name == L"bugreport")
        {
            launch_bug_report();
        }
        else if (name == L"bug_report_status")
        {
            json::JsonObject result;
            result.SetNamedValue(L"bug_report_running", winrt::Windows::Data::Json::JsonValue::CreateBooleanValue(is_bug_report_running()));
            std::unique_lock lock{ ipc_mutex };
            if (current_settings_ipc)
                current_settings_ipc->send(result.Stringify().c_str());
        }
        else if (name == L"killrunner")
        {
            const auto pt_main_window = FindWindowW(pt_tray_icon_window_class, nullptr);
            if (pt_main_window != nullptr)
            {
                SendMessageW(pt_main_window, WM_CLOSE, 0, 0);
            }
        }
        else if (name == L"language")
        {
            constexpr const wchar_t* language_filename = L"\\language.json";
            const std::wstring save_file_location = PTSettingsHelper::get_root_save_folder_location() + language_filename;
            json::to_file(save_file_location, j);
        }
        else if (name == L"check_hotkey_conflict")
        {
            try
            {
                PowertoyModuleIface::Hotkey hotkey;
                hotkey.win = value.GetObjectW().GetNamedBoolean(L"win", false);
                hotkey.ctrl = value.GetObjectW().GetNamedBoolean(L"ctrl", false);
                hotkey.shift = value.GetObjectW().GetNamedBoolean(L"shift", false);
                hotkey.alt = value.GetObjectW().GetNamedBoolean(L"alt", false);
                hotkey.key = static_cast<unsigned char>(value.GetObjectW().GetNamedNumber(L"key", 0));

                std::wstring requestId = value.GetObjectW().GetNamedString(L"request_id", L"").c_str();

                auto& hkmng = HotkeyConflictDetector::HotkeyConflictManager::GetInstance();
                bool hasConflict = hkmng.HasConflict(hotkey);

                json::JsonObject response;
                response.SetNamedValue(L"response_type", json::JsonValue::CreateStringValue(L"hotkey_conflict_result"));
                response.SetNamedValue(L"request_id", json::JsonValue::CreateStringValue(requestId));
                response.SetNamedValue(L"has_conflict", json::JsonValue::CreateBooleanValue(hasConflict));

                if (hasConflict)
                {
                    auto conflicts = hkmng.GetAllConflicts(hotkey);
                    if (!conflicts.empty())
                    {
                        // Include all conflicts in the response
                        json::JsonArray allConflicts;
                        for (const auto& conflict : conflicts)
                        {
                            json::JsonObject conflictObj;
                            conflictObj.SetNamedValue(L"module", json::JsonValue::CreateStringValue(conflict.moduleName));
                            conflictObj.SetNamedValue(L"hotkeyID", json::JsonValue::CreateNumberValue(conflict.hotkeyID));
                            allConflicts.Append(conflictObj);
                        }
                        response.SetNamedValue(L"all_conflicts", allConflicts);
                    }
                }

                std::unique_lock lock{ ipc_mutex };
                if (current_settings_ipc)
                {
                    current_settings_ipc->send(response.Stringify().c_str());
                }
            }
            catch (...)
            {
                Logger::error(L"Failed to process hotkey conflict check request");
            }
        }
        else if (name == L"get_all_hotkey_conflicts")
        {
            try
            {
                auto& hkmng = HotkeyConflictDetector::HotkeyConflictManager::GetInstance();
                auto conflictsJson = hkmng.GetHotkeyConflictsAsJson();

                // Add response type identifier
                conflictsJson.SetNamedValue(L"response_type", json::JsonValue::CreateStringValue(L"all_hotkey_conflicts"));

                std::unique_lock lock{ ipc_mutex };
                if (current_settings_ipc)
                {
                    current_settings_ipc->send(conflictsJson.Stringify().c_str());
                }
            }
            catch (...)
            {
                Logger::error(L"Failed to process get all hotkey conflicts request");
            }
        }
    }
    return;
}

void dispatch_received_json_callback(PVOID data)
{
    std::wstring* msg = static_cast<std::wstring*>(data);
    dispatch_received_json(*msg);
    delete msg;
}

void receive_json_send_to_main_thread(const std::wstring& msg)
{
    std::wstring* copy = new std::wstring(msg);
    dispatch_run_on_main_ui_thread(dispatch_received_json_callback, copy);
}

// Try to run the Settings process with non-elevated privileges.
BOOL run_settings_non_elevated(LPCWSTR executable_path, LPWSTR executable_args, PROCESS_INFORMATION* process_info)
{
    HWND hwnd = GetShellWindow();
    if (!hwnd)
    {
        return false;
    }

    DWORD pid;
    GetWindowThreadProcessId(hwnd, &pid);

    winrt::handle process{ OpenProcess(PROCESS_CREATE_PROCESS, FALSE, pid) };
    if (!process)
    {
        return false;
    }

    SIZE_T size = 0;
    InitializeProcThreadAttributeList(nullptr, 1, 0, &size);
    auto pproc_buffer = std::unique_ptr<char[]>{ new (std::nothrow) char[size] };
    auto pptal = reinterpret_cast<PPROC_THREAD_ATTRIBUTE_LIST>(pproc_buffer.get());
    if (!pptal)
    {
        return false;
    }

    if (!InitializeProcThreadAttributeList(pptal, 1, 0, &size))
    {
        return false;
    }

    if (!UpdateProcThreadAttribute(pptal,
                                   0,
                                   PROC_THREAD_ATTRIBUTE_PARENT_PROCESS,
                                   &process,
                                   sizeof(process),
                                   nullptr,
                                   nullptr))
    {
        return false;
    }

    STARTUPINFOEX siex = { 0 };
    siex.lpAttributeList = pptal;
    siex.StartupInfo.cb = sizeof(siex);

    BOOL process_created = CreateProcessW(executable_path,
                                          executable_args,
                                          nullptr,
                                          nullptr,
                                          FALSE,
                                          EXTENDED_STARTUPINFO_PRESENT,
                                          nullptr,
                                          nullptr,
                                          &siex.StartupInfo,
                                          process_info);
    g_isLaunchInProgress = false;
    return process_created;
}

DWORD g_settings_process_id = 0;

void run_settings_window(bool show_oobe_window, bool show_scoobe_window, std::optional<std::wstring> settings_window, bool show_flyout = false, const std::optional<POINT>& flyout_position = std::nullopt)
{
    g_isLaunchInProgress = true;

    PROCESS_INFORMATION process_info = { 0 };
    HANDLE hToken = nullptr;

    // Arguments for calling the settings executable:
    // "C:\powertoys_path\PowerToysSettings.exe" powertoys_pipe settings_pipe powertoys_pid settings_theme
    // powertoys_pipe: PowerToys pipe server.
    // settings_pipe : Settings pipe server.
    // powertoys_pid : PowerToys process pid.
    // settings_theme: pass "dark" to start the settings window in dark mode

    // Arg 1: executable path.
    std::wstring executable_path = get_module_folderpath();

    executable_path.append(L"\\WinUI3Apps\\PowerToys.Settings.exe");

    // Args 2,3: pipe server. Generate unique names for the pipes, if getting a UUID is possible.
    std::wstring powertoys_pipe_name(L"\\\\.\\pipe\\powertoys_runner_");
    std::wstring settings_pipe_name(L"\\\\.\\pipe\\powertoys_settings_");
    UUID temp_uuid;
    wchar_t* uuid_chars = nullptr;
    if (UuidCreate(&temp_uuid) == RPC_S_UUID_NO_ADDRESS)
    {
        auto val = get_last_error_message(GetLastError());
        Logger::warn(L"UuidCreate cannot create guid. {}", val.has_value() ? val.value() : L"");
    }
    else if (UuidToString(&temp_uuid, reinterpret_cast<RPC_WSTR*>(&uuid_chars)) != RPC_S_OK)
    {
        auto val = get_last_error_message(GetLastError());
        Logger::warn(L"UuidToString cannot convert to string. {}", val.has_value() ? val.value() : L"");
    }

    if (uuid_chars != nullptr)
    {
        powertoys_pipe_name += std::wstring(uuid_chars);
        settings_pipe_name += std::wstring(uuid_chars);
        RpcStringFree(reinterpret_cast<RPC_WSTR*>(&uuid_chars));
        uuid_chars = nullptr;
    }

    // Arg 4: process pid.
    DWORD powertoys_pid = GetCurrentProcessId();

    GeneralSettings save_settings = get_general_settings();

    // Arg 5: settings theme.
    const std::wstring settings_theme_setting{ save_settings.theme };
    std::wstring settings_theme = L"system";
    if (settings_theme_setting == L"dark" || (settings_theme_setting == L"system" && WindowsColors::is_dark_mode()))
    {
        settings_theme = L"dark";
    }

    // Arg 6: elevated status
    bool isElevated{ save_settings.isElevated };
    std::wstring settings_elevatedStatus = isElevated ? L"true" : L"false";

    // Arg 7: is user an admin
    bool isAdmin{ save_settings.isAdmin };
    std::wstring settings_isUserAnAdmin = isAdmin ? L"true" : L"false";

    // Arg 8: should oobe window be shown
    std::wstring settings_showOobe = show_oobe_window ? L"true" : L"false";

    // Arg 9: should scoobe window be shown
    std::wstring settings_showScoobe = show_scoobe_window ? L"true" : L"false";

    // Arg 10: should flyout be shown
    std::wstring settings_showFlyout = show_flyout ? L"true" : L"false";

    // Arg 11: contains if there's a settings window argument. If true, will add one extra argument with the value to the call.
    std::wstring settings_containsSettingsWindow = settings_window.has_value() ? L"true" : L"false";

    // Arg 12: contains if there's flyout coordinates. If true, will add two extra arguments to the call containing the x and y coordinates.
    std::wstring settings_containsFlyoutPosition = flyout_position.has_value() ? L"true" : L"false";

    // Args 13, .... : Optional arguments depending on the options presented before. All by the same value.

    // create general settings file to initialize the settings file with installation configurations like :
    // 1. Run on start up.
    PTSettingsHelper::save_general_settings(save_settings.to_json());

    std::wstring executable_args = fmt::format(L"\"{}\" {} {} {} {} {} {} {} {} {} {} {}",
                                               executable_path,
                                               powertoys_pipe_name,
                                               settings_pipe_name,
                                               std::to_wstring(powertoys_pid),
                                               settings_theme,
                                               settings_elevatedStatus,
                                               settings_isUserAnAdmin,
                                               settings_showOobe,
                                               settings_showScoobe,
                                               settings_showFlyout,
                                               settings_containsSettingsWindow,
                                               settings_containsFlyoutPosition);

    if (settings_window.has_value())
    {
        executable_args.append(L" ");
        executable_args.append(settings_window.value());
    }

    if (flyout_position)
    {
        executable_args.append(L" ");
        executable_args.append(std::to_wstring(flyout_position.value().x));
        executable_args.append(L" ");
        executable_args.append(std::to_wstring(flyout_position.value().y));
    }

    BOOL process_created = false;

    // Commented out to fix #22659
    // Running settings non-elevated and modules elevated when PowerToys is running elevated results
    // in settings making changes in one file (non-elevated user dir) and modules are reading settings
    // from different (elevated user) dir
    //if (is_process_elevated())
    //{

    //    auto res = RunNonElevatedFailsafe(executable_path, executable_args, get_module_folderpath());
    //    process_created = res.has_value();
    //    if (process_created)
    //    {
    //        process_info.dwProcessId = res->processID;
    //        process_info.hProcess = res->processHandle.release();
    //        g_isLaunchInProgress = false;
    //    }
    //}

    if (FALSE == process_created)
    {
        // The runner is not elevated or we failed to create the process using the
        // attribute list from Windows Explorer (this happens when PowerToys is executed
        // as Administrator from a non-Administrator user or an error occur trying).
        // In the second case the Settings process will run elevated.
        STARTUPINFO startup_info = { sizeof(startup_info) };
        if (!CreateProcessW(executable_path.c_str(),
                            executable_args.data(),
                            nullptr,
                            nullptr,
                            FALSE,
                            0,
                            nullptr,
                            nullptr,
                            &startup_info,
                            &process_info))
        {
            goto LExit;
        }
        else
        {
            g_isLaunchInProgress = false;
        }
    }

    if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &hToken))
    {
        goto LExit;
    }

    {
        std::unique_lock lock{ ipc_mutex };
        current_settings_ipc = new TwoWayPipeMessageIPC(powertoys_pipe_name, settings_pipe_name, receive_json_send_to_main_thread);
        current_settings_ipc->start(hToken);

        // Register callback for bug report status changes
        BugReportManager::instance().register_callback([](bool isRunning) {
            json::JsonObject result;
            result.SetNamedValue(L"bug_report_running", winrt::Windows::Data::Json::JsonValue::CreateBooleanValue(isRunning));

            std::unique_lock lock{ ipc_mutex };
            if (current_settings_ipc)
                current_settings_ipc->send(result.Stringify().c_str());
        });
    }

    g_settings_process_id = process_info.dwProcessId;

    if (process_info.hProcess)
    {
        WaitForSingleObject(process_info.hProcess, INFINITE);
        if (WaitForSingleObject(process_info.hProcess, INFINITE) != WAIT_OBJECT_0)
        {
            show_last_error_message(L"Couldn't wait on the Settings Window to close.", GetLastError(), L"PowerToys - runner");
        }
    }
    else
    {
        auto val = get_last_error_message(GetLastError());
        Logger::error(L"Process handle is empty. {}", val.has_value() ? val.value() : L"");
    }

LExit:

    if (process_info.hProcess)
    {
        CloseHandle(process_info.hProcess);
    }

    if (process_info.hThread)
    {
        CloseHandle(process_info.hThread);
    }
    {
        std::unique_lock lock{ ipc_mutex };
        if (current_settings_ipc)
        {
            current_settings_ipc->end();
            delete current_settings_ipc;
            current_settings_ipc = nullptr;
        }
    }

    if (hToken)
    {
        CloseHandle(hToken);
    }

    g_settings_process_id = 0;
}

#define MAX_TITLE_LENGTH 100
void bring_settings_to_front()
{
    auto callback = [](HWND hwnd, LPARAM /*data*/) -> BOOL {
        DWORD processId;
        if (GetWindowThreadProcessId(hwnd, &processId) && processId == g_settings_process_id)
        {
            std::wstring windowTitle = L"PowerToys Settings";

            WCHAR title[MAX_TITLE_LENGTH];
            int len = GetWindowTextW(hwnd, title, MAX_TITLE_LENGTH);
            if (len <= 0)
            {
                return TRUE;
            }
            if (wcsncmp(title, windowTitle.c_str(), len) == 0)
            {
                auto lStyles = GetWindowLong(hwnd, GWL_STYLE);

                if (lStyles & WS_MAXIMIZE)
                {
                    ShowWindow(hwnd, SW_MAXIMIZE);
                }
                else
                {
                    ShowWindow(hwnd, SW_RESTORE);
                }

                SetForegroundWindow(hwnd);
                return FALSE;
            }
        }

        return TRUE;
    };

    EnumWindows(callback, 0);
}

void open_settings_window(std::optional<std::wstring> settings_window, bool show_flyout = false, const std::optional<POINT>& flyout_position)
{
    if (g_settings_process_id != 0)
    {
        if (show_flyout)
        {
            if (current_settings_ipc)
            {
                if (!flyout_position.has_value())
                {
                    current_settings_ipc->send(L"{\"ShowYourself\":\"flyout\"}");
                }
                else
                {
                    current_settings_ipc->send(fmt::format(L"{{\"ShowYourself\":\"flyout\", \"x_position\":{}, \"y_position\":{} }}", std::to_wstring(flyout_position.value().x), std::to_wstring(flyout_position.value().y)));
                }
            }
        }
        else
        {
            // nl instead of showing the window, send message to it (flyout might need to be hidden, main setting window activated)
            // bring_settings_to_front();
            if (current_settings_ipc)
            {
                if (settings_window.has_value())
                {
                    std::wstring msg = L"{\"ShowYourself\":\"" + settings_window.value() + L"\"}";
                    current_settings_ipc->send(msg);
                }
                else
                {
                    current_settings_ipc->send(L"{\"ShowYourself\":\"Dashboard\"}");
                }
            }
        }
    }
    else
    {
        if (!g_isLaunchInProgress)
        {
            std::thread([settings_window, show_flyout, flyout_position]() {
                run_settings_window(false, false, settings_window, show_flyout, flyout_position);
            }).detach();
        }
    }
}

void close_settings_window()
{
    if (g_settings_process_id != 0)
    {
        SetEvent(g_terminateSettingsEvent);
        wil::unique_handle proc{ OpenProcess(PROCESS_ALL_ACCESS, false, g_settings_process_id) };
        if (proc)
        {
            WaitForSingleObject(proc.get(), 1500);
            TerminateProcess(proc.get(), 0);
        }
    }
}

void open_oobe_window()
{
    std::thread([]() {
        run_settings_window(true, false, std::nullopt);
    }).detach();
}

void open_scoobe_window()
{
    std::thread([]() {
        run_settings_window(false, true, std::nullopt);
    }).detach();
}

std::string ESettingsWindowNames_to_string(ESettingsWindowNames value)
{
    switch (value)
    {
    case ESettingsWindowNames::Dashboard:
        return "Dashboard";
    case ESettingsWindowNames::Overview:
        return "Overview";
    case ESettingsWindowNames::AlwaysOnTop:
        return "AlwaysOnTop";
    case ESettingsWindowNames::Awake:
        return "Awake";
    case ESettingsWindowNames::ColorPicker:
        return "ColorPicker";
    case ESettingsWindowNames::CmdNotFound:
        return "CmdNotFound";
    case ESettingsWindowNames::LightSwitch:
        return "LightSwitch";
    case ESettingsWindowNames::FancyZones:
        return "FancyZones";
    case ESettingsWindowNames::FileLocksmith:
        return "FileLocksmith";
    case ESettingsWindowNames::Run:
        return "Run";
    case ESettingsWindowNames::ImageResizer:
        return "ImageResizer";
    case ESettingsWindowNames::KBM:
        return "KBM";
    case ESettingsWindowNames::MouseUtils:
        return "MouseUtils";
    case ESettingsWindowNames::MouseWithoutBorders:
        return "MouseWithoutBorders";
    case ESettingsWindowNames::Peek:
        return "Peek";
    case ESettingsWindowNames::PowerAccent:
        return "PowerAccent";
    case ESettingsWindowNames::PowerLauncher:
        return "PowerLauncher";
    case ESettingsWindowNames::PowerPreview:
        return "PowerPreview";
    case ESettingsWindowNames::PowerRename:
        return "PowerRename";
    case ESettingsWindowNames::FileExplorer:
        return "FileExplorer";
    case ESettingsWindowNames::ShortcutGuide:
        return "ShortcutGuide";
    case ESettingsWindowNames::Hosts:
        return "Hosts";
    case ESettingsWindowNames::MeasureTool:
        return "MeasureTool";
    case ESettingsWindowNames::PowerOCR:
        return "PowerOcr";
    case ESettingsWindowNames::Workspaces:
        return "Workspaces";
    case ESettingsWindowNames::RegistryPreview:
        return "RegistryPreview";
    case ESettingsWindowNames::CropAndLock:
        return "CropAndLock";
    case ESettingsWindowNames::EnvironmentVariables:
        return "EnvironmentVariables";
    case ESettingsWindowNames::AdvancedPaste:
        return "AdvancedPaste";
    case ESettingsWindowNames::NewPlus:
        return "NewPlus";
    case ESettingsWindowNames::CmdPal:
        return "CmdPal";
    case ESettingsWindowNames::ZoomIt:
        return "ZoomIt";
    default:
    {
        Logger::error(L"Can't convert ESettingsWindowNames value={} to string", static_cast<int>(value));
        assert(false);
    }
    }
    return "";
}

ESettingsWindowNames ESettingsWindowNames_from_string(std::string value)
{
    if (value == "Dashboard")
    {
        return ESettingsWindowNames::Dashboard;
    }
    else if (value == "Overview")
    {
        return ESettingsWindowNames::Overview;
    }
    else if (value == "AlwaysOnTop")
    {
        return ESettingsWindowNames::AlwaysOnTop;
    }
    else if (value == "Awake")
    {
        return ESettingsWindowNames::Awake;
    }
    else if (value == "ColorPicker")
    {
        return ESettingsWindowNames::ColorPicker;
    }
    else if (value == "CmdNotFound")
    {
        return ESettingsWindowNames::CmdNotFound;
    }
    else if (value == "LightSwitch")
    {
        return ESettingsWindowNames::LightSwitch;
    }
    else if (value == "FancyZones")
    {
        return ESettingsWindowNames::FancyZones;
    }
    else if (value == "FileLocksmith")
    {
        return ESettingsWindowNames::FileLocksmith;
    }
    else if (value == "Run")
    {
        return ESettingsWindowNames::Run;
    }
    else if (value == "ImageResizer")
    {
        return ESettingsWindowNames::ImageResizer;
    }
    else if (value == "KBM")
    {
        return ESettingsWindowNames::KBM;
    }
    else if (value == "MouseUtils")
    {
        return ESettingsWindowNames::MouseUtils;
    }
    else if (value == "MouseWithoutBorders")
    {
        return ESettingsWindowNames::MouseWithoutBorders;
    }
    else if (value == "Peek")
    {
        return ESettingsWindowNames::Peek;
    }
    else if (value == "PowerAccent")
    {
        return ESettingsWindowNames::PowerAccent;
    }
    else if (value == "PowerLauncher")
    {
        return ESettingsWindowNames::PowerLauncher;
    }
    else if (value == "PowerPreview")
    {
        return ESettingsWindowNames::PowerPreview;
    }
    else if (value == "PowerRename")
    {
        return ESettingsWindowNames::PowerRename;
    }
    else if (value == "FileExplorer")
    {
        return ESettingsWindowNames::FileExplorer;
    }
    else if (value == "ShortcutGuide")
    {
        return ESettingsWindowNames::ShortcutGuide;
    }
    else if (value == "Hosts")
    {
        return ESettingsWindowNames::Hosts;
    }
    else if (value == "MeasureTool")
    {
        return ESettingsWindowNames::MeasureTool;
    }
    else if (value == "PowerOcr")
    {
        return ESettingsWindowNames::PowerOCR;
    }
    else if (value == "Workspaces")
    {
        return ESettingsWindowNames::Workspaces;
    }
    else if (value == "RegistryPreview")
    {
        return ESettingsWindowNames::RegistryPreview;
    }
    else if (value == "CropAndLock")
    {
        return ESettingsWindowNames::CropAndLock;
    }
    else if (value == "EnvironmentVariables")
    {
        return ESettingsWindowNames::EnvironmentVariables;
    }
    else if (value == "AdvancedPaste")
    {
        return ESettingsWindowNames::AdvancedPaste;
    }
    else if (value == "NewPlus")
    {
        return ESettingsWindowNames::NewPlus;
    }
    else if (value == "CmdPal")
    {
        return ESettingsWindowNames::CmdPal;
    }
    else if (value == "ZoomIt")
    {
        return ESettingsWindowNames::ZoomIt;
    }
    else
    {
        Logger::error(L"Can't convert string value={} to ESettingsWindowNames", winrt::to_hstring(value));
        assert(false);
    }

    return ESettingsWindowNames::Dashboard;
}
