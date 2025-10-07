#include "pch.h"
#include <ShellScalingApi.h>
#include <lmcons.h>
#include <filesystem>
#include <sstream>
#include "tray_icon.h"
#include "powertoy_module.h"
#include "trace.h"
#include "general_settings.h"
#include "restart_elevated.h"
#include "RestartManagement.h"
#include "Generated files/resource.h"
#include "settings_telemetry.h"

#include <common/comUtils/comUtils.h>
#include <common/display/dpi_aware.h>
#include <common/Telemetry/EtwTrace/EtwTrace.h>
#include <common/notifications/notifications.h>
#include <common/notifications/dont_show_again.h>
#include <common/updating/installer.h>
#include <common/updating/updating.h>
#include <common/updating/updateState.h>
#include <common/utils/appMutex.h>
#include <common/utils/elevation.h>
#include <common/utils/os-detect.h>
#include <common/utils/processApi.h>
#include <common/utils/resources.h>
#include <common/utils/clean_video_conference.h>

#include "UpdateUtils.h"
#include "ActionRunnerUtils.h"

#include <winrt/Windows.System.h>

#include <Psapi.h>
#include <RestartManager.h>
#include "centralized_kb_hook.h"
#include "centralized_hotkeys.h"

#if _DEBUG && _WIN64
#include "unhandled_exception_handler.h"
#endif
#include <common/logger/logger.h>
#include <common/SettingsAPI/settings_helpers.h>
#include <runner/settings_window.h>
#include <common/utils/process_path.h>
#include <common/utils/winapi_error.h>
#include <common/utils/window.h>
#include <common/version/version.h>
#include <common/utils/string_utils.h>
#include <common/utils/gpo.h>

// disabling warning 4458 - declaration of 'identifier' hides class member
// to avoid warnings from GDI files - can't add winRT directory to external code
// in the Cpp.Build.props
#pragma warning(push)
#pragma warning(disable : 4458)
#include <gdiplus.h>
#pragma warning(pop)

namespace
{
    const wchar_t PT_URI_PROTOCOL_SCHEME[] = L"powertoys://";
    const wchar_t POWER_TOYS_MODULE_LOAD_FAIL[] = L"Failed to load "; // Module name will be appended on this message and it is not localized.
}

void chdir_current_executable()
{
    // Change current directory to the path of the executable.
    WCHAR executable_path[MAX_PATH];
    GetModuleFileName(NULL, executable_path, MAX_PATH);
    PathRemoveFileSpec(executable_path);
    if (!SetCurrentDirectory(executable_path))
    {
        show_last_error_message(L"Change Directory to Executable Path", GetLastError(), L"PowerToys - runner");
    }
}

inline wil::unique_mutex_nothrow create_msi_mutex()
{
    return createAppMutex(POWERTOYS_MSI_MUTEX_NAME);
}

void open_menu_from_another_instance(std::optional<std::string> settings_window)
{
    const HWND hwnd_main = FindWindowW(L"PToyTrayIconWindow", nullptr);
    LPARAM msg = static_cast<LPARAM>(ESettingsWindowNames::Dashboard);
    if (settings_window.has_value() && settings_window.value() != "")
    {
        msg = static_cast<LPARAM>(ESettingsWindowNames_from_string(settings_window.value()));
    }
    PostMessageW(hwnd_main, WM_COMMAND, ID_SETTINGS_MENU_COMMAND, msg);
    SetForegroundWindow(hwnd_main); // Bring the settings window to the front
}

int runner(bool isProcessElevated, bool openSettings, std::string settingsWindow, bool openOobe, bool openScoobe, bool showRestartNotificationAfterUpdate)
{
    Logger::info("Runner is starting. Elevated={} openOobe={} openScoobe={} showRestartNotificationAfterUpdate={}", isProcessElevated, openOobe, openScoobe, showRestartNotificationAfterUpdate);
    DPIAware::EnableDPIAwarenessForThisProcess();

#if _DEBUG && _WIN64
//Global error handlers to diagnose errors.
//We prefer this not to show any longer until there's a bug to diagnose.
//init_global_error_handlers();
#endif
    Trace::RegisterProvider();
    start_tray_icon(isProcessElevated);
    set_tray_icon_visible(get_general_settings().showSystemTrayIcon);
    CentralizedKeyboardHook::Start();

    int result = -1;
    try
    {
        if (!openOobe && showRestartNotificationAfterUpdate)
        {
            std::thread{
                [] {
                    // Wait a bit, because Windows has a delay until it picks up toast notification registration in the registry
                    Sleep(10000);
                    Logger::info("Showing toast notification asking to restart PC");
                    notifications::show_toast(GET_RESOURCE_STRING(IDS_PT_VERSION_CHANGE_ASK_FOR_COMPUTER_RESTART).c_str(), L"PowerToys");
                }
            }.detach();
        }

        std::thread{ [] {
            PeriodicUpdateWorker();
        } }.detach();

        std::thread{ [] {
            if (updating::uninstall_previous_msix_version_async().get())
            {
                notifications::show_toast(GET_RESOURCE_STRING(IDS_OLDER_MSIX_UNINSTALLED).c_str(), L"PowerToys");
            }
        } }.detach();

        chdir_current_executable();

        // We deprecated a utility called Video Conference Mute, which registered itself as a video input device.
        // When running elevated, we try to clean up the device registration from previous installations.
        // This is done here too because a user-scope installer won't be able to remove the driver registration due to lack of permissions.
        if (isProcessElevated)
        {
            clean_video_conference();
        }

        // Load PowerToys DLLs

        std::vector<std::wstring_view> knownModules = {
            L"PowerToys.FancyZonesModuleInterface.dll",
            L"PowerToys.powerpreview.dll",
            L"WinUI3Apps/PowerToys.ImageResizerExt.dll",
            L"PowerToys.KeyboardManager.dll",
            L"PowerToys.Launcher.dll",
            L"WinUI3Apps/PowerToys.PowerRenameExt.dll",
            L"PowerToys.ShortcutGuideModuleInterface.dll",
            L"PowerToys.ColorPicker.dll",
            L"PowerToys.AwakeModuleInterface.dll",
            L"PowerToys.FindMyMouse.dll",
            L"PowerToys.MouseHighlighter.dll",
            L"PowerToys.MouseJump.dll",
            L"PowerToys.AlwaysOnTopModuleInterface.dll",
            L"PowerToys.MousePointerCrosshairs.dll",
            L"PowerToys.PowerAccentModuleInterface.dll",
            L"PowerToys.PowerOCRModuleInterface.dll",
            L"PowerToys.AdvancedPasteModuleInterface.dll",
            L"WinUI3Apps/PowerToys.FileLocksmithExt.dll",
            L"WinUI3Apps/PowerToys.RegistryPreviewExt.dll",
            L"WinUI3Apps/PowerToys.MeasureToolModuleInterface.dll",
            L"WinUI3Apps/PowerToys.NewPlus.ShellExtension.dll",
            L"WinUI3Apps/PowerToys.HostsModuleInterface.dll",
            L"WinUI3Apps/PowerToys.Peek.dll",
            L"WinUI3Apps/PowerToys.EnvironmentVariablesModuleInterface.dll",
            L"PowerToys.MouseWithoutBordersModuleInterface.dll",
            L"PowerToys.CropAndLockModuleInterface.dll",
            L"PowerToys.CmdNotFoundModuleInterface.dll",
            L"PowerToys.WorkspacesModuleInterface.dll",
            L"PowerToys.CmdPalModuleInterface.dll",
            L"PowerToys.ZoomItModuleInterface.dll",
            L"PowerToys.LightSwitchModuleInterface.dll",
        };

        for (auto moduleSubdir : knownModules)
        {
            try
            {
                auto pt_module = load_powertoy(moduleSubdir);
                modules().emplace(pt_module->get_key(), std::move(pt_module));
            }
            catch (...)
            {
                std::wstring errorMessage = POWER_TOYS_MODULE_LOAD_FAIL;
                errorMessage += moduleSubdir;
                
#ifdef _DEBUG
                // In debug mode, simply log the warning and continue execution.
                // This contrasts with the past approach where developers had to build all modules
                // without errors before debugging—slowing down quick clone-and-fix iterations.
                Logger::warn(L"Debug mode: {}", errorMessage);
#else
                // In release mode, show error dialog as before
                MessageBoxW(NULL,
                            errorMessage.c_str(),
                            L"PowerToys",
                            MB_OK | MB_ICONERROR);
#endif
            }
        }
        // Start initial powertoys
        start_enabled_powertoys();
        std::wstring product_version = get_product_version();
        Trace::EventLaunch(product_version, isProcessElevated);
        PTSettingsHelper::save_last_version_run(product_version);

        if (openSettings)
        {
            std::optional<std::wstring> window;
            if (!settingsWindow.empty())
            {
                window = winrt::to_hstring(settingsWindow);
            }
            open_settings_window(window, false);
        }

        if (openOobe)
        {
            PTSettingsHelper::save_oobe_opened_state();
            open_oobe_window();
        }
        else if (openScoobe)
        {
            open_scoobe_window();
        }

        settings_telemetry::init();
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
        else if (n_cmd_args == 2 && !wcscmp(cmdArg::UPDATE_REPORT_SUCCESS, cmd_arg_list[i]))
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
    const std::wstring_view cant_drag_elevated_disable = L"cant_drag_elevated_disable/";
    const std::wstring_view couldnt_toggle_powerpreview_modules_disable = L"couldnt_toggle_powerpreview_modules_disable/";
    const std::wstring_view open_settings = L"open_settings/";
    const std::wstring_view open_overview = L"open_overview/";
    const std::wstring_view update_now = L"update_now/";

    if (param == cant_drag_elevated_disable)
    {
        return notifications::disable_toast(notifications::ElevatedDontShowAgainRegistryPath) ? toast_notification_handler_result::exit_success : toast_notification_handler_result::exit_error;
    }
    else if (param.starts_with(update_now))
    {
        std::wstring args{ cmdArg::UPDATE_NOW_LAUNCH_STAGE1 };
        LaunchPowerToysUpdate(args.c_str());
        return toast_notification_handler_result::exit_success;
    }
    else if (param == couldnt_toggle_powerpreview_modules_disable)
    {
        return notifications::disable_toast(notifications::PreviewModulesDontShowAgainRegistryPath) ? toast_notification_handler_result::exit_success : toast_notification_handler_result::exit_error;
    }
    else if (param == open_settings)
    {
        open_menu_from_another_instance(std::nullopt);
        return toast_notification_handler_result::exit_success;
    }
    else if (param == open_overview)
    {
        open_menu_from_another_instance("Overview");
        return toast_notification_handler_result::exit_success;
    }
    else
    {
        return toast_notification_handler_result::exit_error;
    }
}

int WINAPI WinMain(HINSTANCE /*hInstance*/, HINSTANCE /*hPrevInstance*/, LPSTR lpCmdLine, int /*nCmdShow*/)
{
    Shared::Trace::ETWTrace trace{};
    trace.UpdateState(true);

    Gdiplus::GdiplusStartupInput gpStartupInput;
    ULONG_PTR gpToken;
    GdiplusStartup(&gpToken, &gpStartupInput, NULL);

    winrt::init_apartment();

    const wchar_t* securityDescriptor =
        L"O:BA" // Owner: Builtin (local) administrator
        L"G:BA" // Group: Builtin (local) administrator
        L"D:"
        L"(A;;0x7;;;PS)" // Access allowed on COM_RIGHTS_EXECUTE, _LOCAL, & _REMOTE for Personal self
        L"(A;;0x7;;;IU)" // Access allowed on COM_RIGHTS_EXECUTE for Interactive Users
        L"(A;;0x3;;;SY)" // Access allowed on COM_RIGHTS_EXECUTE, & _LOCAL for Local system
        L"(A;;0x7;;;BA)" // Access allowed on COM_RIGHTS_EXECUTE, _LOCAL, & _REMOTE for Builtin (local) administrator
        L"(A;;0x3;;;S-1-15-3-1310292540-1029022339-4008023048-2190398717-53961996-4257829345-603366646)" // Access allowed on COM_RIGHTS_EXECUTE, & _LOCAL for Win32WebViewHost package capability
        L"S:"
        L"(ML;;NX;;;LW)"; // Integrity label on No execute up for Low mandatory level
    initializeCOMSecurity(securityDescriptor);

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
        [[fallthrough]];
    case SpecialMode::ReportSuccessfulUpdate:
    {
        notifications::remove_toasts_by_tag(notifications::UPDATING_PROCESS_TOAST_TAG);
        notifications::remove_all_scheduled_toasts();
        notifications::show_toast(GET_RESOURCE_STRING(IDS_PT_UPDATE_MESSAGE_BOX_TEXT),
                                  L"PowerToys",
                                  notifications::toast_params{ notifications::UPDATING_PROCESS_TOAST_TAG });
        break;
    }

    case SpecialMode::None:
        // continue as usual
        break;
    }

    std::filesystem::path logFilePath(PTSettingsHelper::get_root_save_folder_location());
    logFilePath.append(LogSettings::runnerLogPath);
    Logger::init(LogSettings::runnerLoggerName, logFilePath.wstring(), PTSettingsHelper::get_log_settings_file_location());

    const std::string cmdLine{ lpCmdLine };
    Logger::info("Running powertoys with cmd args: {}", cmdLine);

    auto open_settings_it = cmdLine.find("--open-settings");
    const bool open_settings = open_settings_it != std::string::npos;
    // Check if opening specific settings window
    open_settings_it = cmdLine.find("--open-settings=");
    std::string settings_window;
    if (open_settings_it != std::string::npos)
    {
        std::string rest_of_cmd_line{ cmdLine, open_settings_it + std::string{ "--open-settings=" }.size() };
        std::istringstream iss(rest_of_cmd_line);
        iss >> settings_window;
    }

    // Check if another instance is already running.
    wil::unique_mutex_nothrow msi_mutex = create_msi_mutex();
    if (!msi_mutex)
    {
        open_menu_from_another_instance(settings_window);
        return 0;
    }

    bool openOobe = false;
    try
    {
        openOobe = !PTSettingsHelper::get_oobe_opened_state();
    }
    catch (const std::exception& e)
    {
        Logger::error("Failed to get or save OOBE state with an exception: {}", e.what());
    }

    bool openScoobe = false;
    bool showRestartNotificationAfterUpdate = false;
    try
    {
        std::wstring last_version_run = PTSettingsHelper::get_last_version_run();
        const auto product_version = get_product_version();
        openScoobe = product_version != last_version_run;
        showRestartNotificationAfterUpdate = openScoobe;
        Logger::info(L"Scoobe: product_version={} last_version_run={}", product_version, last_version_run);
    }
    catch (const std::exception& e)
    {
        Logger::error("Failed to get last version with an exception: {}", e.what());
    }

    int result = 0;
    try
    {
        // Singletons initialization order needs to be preserved, first events and
        // then modules to guarantee the reverse destruction order.
        modules();

        std::thread{ [] {
            auto state = UpdateState::read();
            if (state.state == UpdateState::upToDate)
            {
                updating::cleanup_updates();
            }
        } }.detach();

        auto general_settings = load_general_settings();

        // Apply the general settings but don't save it as the modules() variable has not been loaded yet
        apply_general_settings(general_settings, false);
        const bool elevated = is_process_elevated();
        const bool with_dont_elevate_arg = cmdLine.find("--dont-elevate") != std::string::npos;
        const bool run_elevated_setting = general_settings.GetNamedBoolean(L"run_elevated", false);
        const bool with_restartedElevated_arg = cmdLine.find("--restartedElevated") != std::string::npos;

        // Update scoobe behavior based on setting and gpo
        bool scoobeSettingDisabled = general_settings.GetNamedBoolean(L"show_whats_new_after_updates", true) == false;
        bool scoobeDisabledByGpo = powertoys_gpo::getDisableShowWhatsNewAfterUpdatesValue() == powertoys_gpo::gpo_rule_configured_enabled;
        if (openScoobe && (scoobeSettingDisabled || scoobeDisabledByGpo))
        {
            // Scoobe should show after an update, but is disabled by policy or setting
            Logger::info(L"Scoobe: Showing scoobe after updates is disabled by setting or by GPO.");
            openScoobe = false;
        }

        bool dataDiagnosticsDisabledByGpo = powertoys_gpo::getAllowDataDiagnosticsValue() == powertoys_gpo::gpo_rule_configured_disabled;
        if (dataDiagnosticsDisabledByGpo)
        {
            Logger::info(L"Data diagnostics: Data diagnostics is disabled by GPO.");
            PTSettingsHelper::save_data_diagnostics(false);
        }

        if (elevated && with_dont_elevate_arg && !run_elevated_setting)
        {
            Logger::info("Scheduling restart as non elevated");
            schedule_restart_as_non_elevated();
            result = 0;
        }
        else if (elevated || !run_elevated_setting || with_dont_elevate_arg || (!elevated && with_restartedElevated_arg))
        {
            // The condition (!elevated && with_restartedElevated_arg) solves issue #19307. Restart elevated loop detected, running non-elevated
            if (!elevated && with_restartedElevated_arg)
            {
                Logger::info("Restart as elevated failed. Running non-elevated.");
            }

            result = runner(elevated, open_settings, settings_window, openOobe, openScoobe, showRestartNotificationAfterUpdate);

            if (result == 0)
            {
                // Save settings on closing, if closed 'normal'
                PTSettingsHelper::save_general_settings(get_general_settings().to_json());
            }
        }
        else
        {
            Logger::info("Scheduling restart as elevated");
            schedule_restart_as_elevated(open_settings);
            result = 0;
        }
    }
    catch (std::runtime_error& err)
    {
        std::string err_what = err.what();
        MessageBoxW(nullptr, std::wstring(err_what.begin(), err_what.end()).c_str(), GET_RESOURCE_STRING(IDS_ERROR).c_str(), MB_OK | MB_ICONERROR);
        result = -1;
    }

    trace.Flush();
    trace.UpdateState(false);

    // We need to release the mutexes to be able to restart the application
    if (msi_mutex)
    {
        msi_mutex.reset(nullptr);
    }

    if (is_restart_scheduled())
    {
        modules().clear();
        if (!restart_if_scheduled())
        {
            // If it's not possible to restart non-elevated due to some condition in the user's configuration, user should start PowerToys manually.
            Logger::warn("Scheduled restart failed. Couldn't restart non-elevated. PowerToys exits here because retrying it would just mean failing in a loop.");
        }
    }
    stop_tray_icon();

    return result;
}
