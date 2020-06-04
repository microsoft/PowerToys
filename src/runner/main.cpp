#include "pch.h"
#include <ShellScalingApi.h>
#include <lmcons.h>
#include <filesystem>
#include "tray_icon.h"
#include "powertoy_module.h"
#include "trace.h"
#include "general_settings.h"
#include "restart_elevated.h"
#include "resource.h"

#include <common/common.h>
#include <common/dpi_aware.h>

#include <common/winstore.h>
#include <common/notifications.h>

#include <common/updating/updating.h>

#include "update_state.h"
#include "update_utils.h"
#include "action_runner_utils.h"

#include <winrt/Windows.System.h>

#if _DEBUG && _WIN64
#include "unhandled_exception_handler.h"
#endif
#include <common/notifications/fancyzones_notifications.h>

extern "C" IMAGE_DOS_HEADER __ImageBase;

namespace localized_strings
{
    const wchar_t MSI_VERSION_IS_ALREADY_RUNNING[] = L"An older version of PowerToys is already running.";
    const wchar_t OLDER_MSIX_UNINSTALLED[] = L"An older MSIX version of PowerToys was uninstalled.";
}

namespace
{
    const wchar_t MSI_VERSION_MUTEX_NAME[] = L"Local\\PowerToyRunMutex";
    const wchar_t MSIX_VERSION_MUTEX_NAME[] = L"Local\\PowerToyMSIXRunMutex";

    const wchar_t PT_URI_PROTOCOL_SCHEME[] = L"powertoys://";
}

void chdir_current_executable()
{
    // Change current directory to the path of the executable.
    WCHAR executable_path[MAX_PATH];
    GetModuleFileName(NULL, executable_path, MAX_PATH);
    PathRemoveFileSpec(executable_path);
    if (!SetCurrentDirectory(executable_path))
    {
        show_last_error_message(L"Change Directory to Executable Path", GetLastError());
    }
}

wil::unique_mutex_nothrow create_runner_mutex(const bool msix_version)
{
    wchar_t username[UNLEN + 1];
    DWORD username_length = UNLEN + 1;
    GetUserNameW(username, &username_length);
    wil::unique_mutex_nothrow result{ CreateMutexW(nullptr, TRUE, (std::wstring(msix_version ? MSIX_VERSION_MUTEX_NAME : MSI_VERSION_MUTEX_NAME) + username).c_str()) };

    return GetLastError() == ERROR_ALREADY_EXISTS ? wil::unique_mutex_nothrow{} : std::move(result);
}

wil::unique_mutex_nothrow create_msi_mutex()
{
    return create_runner_mutex(false);
}

wil::unique_mutex_nothrow create_msix_mutex()
{
    return create_runner_mutex(true);
}

void open_menu_from_another_instance()
{
    HWND hwnd_main = FindWindow(L"PToyTrayIconWindow", NULL);
    PostMessage(hwnd_main, WM_COMMAND, ID_SETTINGS_MENU_COMMAND, NULL);
}

int runner(bool isProcessElevated)
{
    DPIAware::EnableDPIAwarenessForThisProcess();

#if _DEBUG && _WIN64
//Global error handlers to diagnose errors.
//We prefer this not not show any longer until there's a bug to diagnose.
//init_global_error_handlers();
#endif
    Trace::RegisterProvider();
    start_tray_icon();

    int result = -1;
    try
    {
        std::thread{ [] {
            github_update_worker();
        } }.detach();

        if (winstore::running_as_packaged())
        {
            std::thread{ [] {
                start_msi_uninstallation_sequence();
            } }.detach();
        }
        else
        {
            std::thread{ [] {
                if (updating::uninstall_previous_msix_version_async().get())
                {
                    notifications::show_toast(localized_strings::OLDER_MSIX_UNINSTALLED);
                }
            } }.detach();
        }

        notifications::register_background_toast_handler();

        chdir_current_executable();
        // Load Powertoys DLLS
        // For now only load known DLLs
        
        std::wstring baseModuleFolder = L"modules/";

        std::unordered_set<std::wstring> known_dlls = {
            L"ShortcutGuide.dll",
            L"fancyzones.dll",
            L"PowerRenameExt.dll",
            L"Microsoft.Launcher.dll",
            L"ImageResizerExt.dll",
            L"powerpreview.dll",
            L"KeyboardManager.dll"
        };

        // TODO(stefan): When all modules get their OutputDir delete this and simplify "search for .dll logic"
        std::unordered_set<std::wstring> module_folders = {
            L"",
            L"FileExplorerPreview/",
            L"FancyZones/",
            L"ImageResizer/",
            L"PowerRename/",
            L"ShortcutGuide/",
            L"KeyboardManager/"
        };

        for (std::wstring subfolderName : module_folders)
        {
            for (auto& file : std::filesystem::directory_iterator(baseModuleFolder + subfolderName))
            {
                if (file.path().extension() != L".dll")
                    continue;
                if (known_dlls.find(file.path().filename()) == known_dlls.end())
                    continue;
                try
                {
                    auto module = load_powertoy(file.path().wstring());
                    modules().emplace(module->get_name(), std::move(module));
                }
                catch (...)
                {
                }
            }
        }
        // Start initial powertoys
        start_initial_powertoys();

        Trace::EventLaunch(get_product_version(), isProcessElevated);

        result = run_message_loop();
    }
    catch (std::runtime_error& err)
    {
        std::string err_what = err.what();
        MessageBoxW(nullptr, std::wstring(err_what.begin(), err_what.end()).c_str(), GET_RESOURCE_STRING(IDS_ERROR).c_str(), MB_OK | MB_ICONERROR | MB_SETFOREGROUND);
        result = -1;
    }
    Trace::UnregisterProvider();
    return result;
}

// If the PT runner is launched as part of some action and manually by a user, e.g. being activated as a COM server
// for background toast notification handling, we should execute corresponding code flow instead of the main code flow.
enum class SpecialMode
{
    None,
    Win32ToastNotificationCOMServer,
    ToastNotificationHandler,
    ReportSuccessfulUpdate
};

SpecialMode should_run_in_special_mode(const int n_cmd_args, LPWSTR* cmd_arg_list)
{
    for (size_t i = 1; i < n_cmd_args; ++i)
    {
        if (!wcscmp(notifications::TOAST_ACTIVATED_LAUNCH_ARG, cmd_arg_list[i]))
        {
            return SpecialMode::Win32ToastNotificationCOMServer;
        }
        else if (n_cmd_args == 2 && !wcsncmp(PT_URI_PROTOCOL_SCHEME, cmd_arg_list[i], wcslen(PT_URI_PROTOCOL_SCHEME)))
        {
            return SpecialMode::ToastNotificationHandler;
        }
        else if (n_cmd_args == 2 && !wcscmp(UPDATE_REPORT_SUCCESS, cmd_arg_list[i]))
        {
            return SpecialMode::ReportSuccessfulUpdate;
        }
    }

    return SpecialMode::None;
}

int win32_toast_notification_COM_server_mode()
{
    notifications::run_desktop_app_activator_loop();
    return 0;
}

enum class toast_notification_handler_result
{
    exit_success,
    exit_error
};

toast_notification_handler_result toast_notification_handler(const std::wstring_view param)
{
    if (param == L"cant_drag_elevated_disable/")
    {
        return disable_cant_drag_elevated_warning() ? toast_notification_handler_result::exit_success : toast_notification_handler_result::exit_error;
    }
    else if (param == L"update_now/")
    {
        launch_action_runner(UPDATE_NOW_LAUNCH_STAGE1_CMDARG);
        return toast_notification_handler_result::exit_success;
    }
    else if (param == L"schedule_update/")
    {
        UpdateState::store([](UpdateState& state) {
            state.pending_update = true;
        });

        return toast_notification_handler_result::exit_success;
    }
    else
    {
        return toast_notification_handler_result::exit_error;
    }
}

int WINAPI WinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, LPSTR lpCmdLine, int nCmdShow)
{
    winrt::init_apartment();

    if (launch_pending_update())
    {
        return 0;
    }

    int n_cmd_args = 0;
    LPWSTR* cmd_arg_list = CommandLineToArgvW(GetCommandLineW(), &n_cmd_args);
    switch (should_run_in_special_mode(n_cmd_args, cmd_arg_list))
    {
    case SpecialMode::Win32ToastNotificationCOMServer:
        return win32_toast_notification_COM_server_mode();
    case SpecialMode::ToastNotificationHandler:
        switch (toast_notification_handler(cmd_arg_list[1] + wcslen(PT_URI_PROTOCOL_SCHEME)))
        {
        case toast_notification_handler_result::exit_error:
            return 1;
        case toast_notification_handler_result::exit_success:
            return 0;
        }
    case SpecialMode::ReportSuccessfulUpdate:
        notifications::show_toast(GET_RESOURCE_STRING(IDS_AUTOUPDATE_SUCCESS));
        break;

    case SpecialMode::None:
        // continue as usual
        break;
    }

    wil::unique_mutex_nothrow msi_mutex;
    wil::unique_mutex_nothrow msix_mutex;

    if (winstore::running_as_packaged())
    {
        msix_mutex = create_msix_mutex();
        if (!msix_mutex)
        {
            // The MSIX version is already running.
            open_menu_from_another_instance();
            return 0;
        }

        // Check if the MSI version is running, if not, hold the
        // mutex to prevent the old MSI versions to start.
        msi_mutex = create_msi_mutex();
        if (!msi_mutex)
        {
            // The MSI version is running, warn the user and offer to uninstall it.
            const bool declined_uninstall = !start_msi_uninstallation_sequence();
            if (declined_uninstall)
            {
                // Check again if the MSI version is still running.
                msi_mutex = create_msi_mutex();
                if (!msi_mutex)
                {
                    open_menu_from_another_instance();
                    return 0;
                }
            }
        }
        else
        {
            // Older MSI versions are not aware of the MSIX mutex, therefore
            // hold the MSI mutex to prevent an old instance to start.
        }
    }
    else
    {
        // Check if another instance of the MSI version is already running.
        msi_mutex = create_msi_mutex();
        if (!msi_mutex)
        {
            // The MSI version is already running.
            open_menu_from_another_instance();
            return 0;
        }

        // Check if an instance of the MSIX version is already running.
        // Note: this check should always be negative since the MSIX version
        // is holding both mutexes.
        msix_mutex = create_msix_mutex();
        if (!msix_mutex)
        {
            // The MSIX version is already running.
            open_menu_from_another_instance();
            return 0;
        }
        else
        {
            // The MSIX version isn't running, release the mutex.
            msix_mutex.reset(nullptr);
        }
    }

    int result = 0;
    try
    {
        // Singletons initialization order needs to be preserved, first events and
        // then modules to guarantee the reverse destruction order.
        SystemMenuHelperInstance();
        powertoys_events();
        modules();

        auto general_settings = load_general_settings();
        int rvalue = 0;
        const bool elevated = is_process_elevated();
        if ((elevated ||
             general_settings.GetNamedBoolean(L"run_elevated", false) == false ||
             strcmp(lpCmdLine, "--dont-elevate") == 0))
        {
            result = runner(elevated);
        }
        else
        {
            schedule_restart_as_elevated();
            result = 0;
        }
    }
    catch (std::runtime_error& err)
    {
        std::string err_what = err.what();
        MessageBoxW(nullptr, std::wstring(err_what.begin(), err_what.end()).c_str(), GET_RESOURCE_STRING(IDS_ERROR).c_str(), MB_OK | MB_ICONERROR);
        result = -1;
    }

    // We need to release the mutexes to be able to restart the application
    if (msi_mutex)
    {
        msi_mutex.reset(nullptr);
    }

    if (msix_mutex)
    {
        msix_mutex.reset(nullptr);
    }

    if (is_restart_scheduled())
    {
        if (restart_if_scheduled() == false)
        {
            auto text = is_process_elevated() ? GET_RESOURCE_STRING(IDS_COULDNOT_RESTART_NONELEVATED) :
                                                GET_RESOURCE_STRING(IDS_COULDNOT_RESTART_ELEVATED);
            MessageBoxW(nullptr, text.c_str(), GET_RESOURCE_STRING(IDS_ERROR).c_str(), MB_OK | MB_ICONERROR | MB_SETFOREGROUND);

            restart_same_elevation();
            result = -1;
        }
    }
    stop_tray_icon();
    return result;
}
