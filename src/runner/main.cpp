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
    LPARAM msg = static_cast<LPARAM>(ESettingsWindowNames::Overview);
    if (settings_window.has_value())
    {
        msg = static_cast<LPARAM>(ESettingsWindowNames_from_string(settings_window.value()));
    }
    PostMessageW(hwnd_main, WM_COMMAND, ID_SETTINGS_MENU_COMMAND, msg);
}

void debug_verify_launcher_assets()
{
    try
    {
        namespace fs = std::filesystem;
        const fs::path powertoysRoot = get_module_folderpath();
        constexpr std::array<std::string_view, 4> assetsToCheck = { "modules\\launcher\\Images\\app.dark.png",
                                                                    "modules\\launcher\\Images\\app.light.png",
                                                                    "modules\\launcher\\Images\\app_error.dark.png",
                                                                    "modules\\launcher\\Images\\app_error.light.png" };
        for (const auto asset : assetsToCheck)
        {
            const auto assetPath = powertoysRoot / asset;
            if (!fs::is_regular_file(assetPath))
            {
                Logger::error("{} couldn't be found.", assetPath.string());
            }
        }
    }
    catch (...)
    {
    }
}

int runner(bool isProcessElevated, bool openSettings, std::string settingsWindow, bool openOobe, bool openScoobe)
{
    Logger::info("Runner is starting. Elevated={}", isProcessElevated);
    DPIAware::EnableDPIAwarenessForThisProcess();

#if _DEBUG && _WIN64
//Global error handlers to diagnose errors.
//We prefer this not to show any longer until there's a bug to diagnose.
//init_global_error_handlers();
#endif
    Trace::RegisterProvider();
    start_tray_icon();
    CentralizedKeyboardHook::Start();

    int result = -1;
    try
    {
        debug_verify_launcher_assets();

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
        // Load Powertoys DLLs

        std::vector<std::wstring_view> knownModules = {
            L"modules/FancyZones/PowerToys.FancyZonesModuleInterface.dll",
            L"modules/FileExplorerPreview/PowerToys.powerpreview.dll",
            L"modules/ImageResizer/PowerToys.ImageResizerExt.dll",
            L"modules/KeyboardManager/PowerToys.KeyboardManager.dll",
            L"modules/Launcher/PowerToys.Launcher.dll",
            L"modules/PowerRename/PowerToys.PowerRenameExt.dll",
            L"modules/ShortcutGuide/ShortcutGuideModuleInterface/PowerToys.ShortcutGuideModuleInterface.dll",
            L"modules/ColorPicker/PowerToys.ColorPicker.dll",
            L"modules/Awake/PowerToys.AwakeModuleInterface.dll",
            L"modules/MouseUtils/PowerToys.FindMyMouse.dll",
            L"modules/MouseUtils/PowerToys.MouseHighlighter.dll",
            L"modules/AlwaysOnTop/PowerToys.AlwaysOnTopModuleInterface.dll",
            L"modules/MouseUtils/PowerToys.MousePointerCrosshairs.dll",
            L"modules/PowerAccent/PowerToys.PowerAccentModuleInterface.dll",
            L"modules/PowerOCR/PowerToys.PowerOCRModuleInterface.dll",
            L"modules/FileLocksmith/PowerToys.FileLocksmithExt.dll",
            L"modules/MeasureTool/PowerToys.MeasureToolModuleInterface.dll",
            L"modules/Hosts/PowerToys.HostsModuleInterface.dll",
        };
        const auto VCM_PATH = L"modules/VideoConference/PowerToys.VideoConferenceModule.dll";
        if (const auto mf = LoadLibraryA("mf.dll"))
        {
            FreeLibrary(mf);
            knownModules.emplace_back(VCM_PATH);
        }

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
                MessageBoxW(NULL,
                            errorMessage.c_str(),
                            L"PowerToys",
                            MB_OK | MB_ICONERROR);
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
    const std::wstring_view update_now = L"update_now/";

    if (param == cant_drag_elevated_disable)
    {
        return notifications::disable_toast(notifications::CantDragElevatedDontShowAgainRegistryPath) ? toast_notification_handler_result::exit_success : toast_notification_handler_result::exit_error;
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
    else
    {
        return toast_notification_handler_result::exit_error;
    }
}

void cleanup_updates()
{
    auto state = UpdateState::read();
    if (state.state != UpdateState::upToDate)
    {
        return;
    }

    auto update_dir = updating::get_pending_updates_path();
    if (std::filesystem::exists(update_dir))
    {
        // Msi and exe files
        for (const auto& entry : std::filesystem::directory_iterator(update_dir))
        {
            auto entryPath = entry.path().wstring();
            std::transform(entryPath.begin(), entryPath.end(), entryPath.begin(), ::towlower);

            if (entryPath.ends_with(L".msi") || entryPath.ends_with(L".exe"))
            {
                std::error_code err;
                std::filesystem::remove(entry, err);
                if (err.value())
                {
                    Logger::warn("Failed to delete installer file {}. {}", entry.path().string(), err.message());
                }
            }
        }
    }

    // Log files
    auto rootPath{ PTSettingsHelper::get_root_save_folder_location() };
    auto currentVersion = left_trim<wchar_t>(get_product_version(), L"v");
    if (std::filesystem::exists(rootPath))
    {
        for (const auto& entry : std::filesystem::directory_iterator(rootPath))
        {
            auto entryPath = entry.path().wstring();
            std::transform(entryPath.begin(), entryPath.end(), entryPath.begin(), ::towlower);
            if (entry.is_regular_file() && entryPath.ends_with(L".log") && entryPath.find(currentVersion) == std::string::npos)
            {
                std::error_code err;
                std::filesystem::remove(entry, err);
                if (err.value())
                {
                    Logger::warn("Failed to delete log file {}. {}", entry.path().string(), err.message());
                }
            }
        }
    }
}

int WINAPI WinMain(HINSTANCE /*hInstance*/, HINSTANCE /*hPrevInstance*/, LPSTR lpCmdLine, int /*nCmdShow*/)
{
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
    try
    {
        std::wstring last_version_run = PTSettingsHelper::get_last_version_run();
        openScoobe = last_version_run != get_product_version();
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
            cleanup_updates();
        } }.detach();

        auto general_settings = load_general_settings();

        // Apply the general settings but don't save it as the modules() variable has not been loaded yet
        apply_general_settings(general_settings, false);
        const bool elevated = is_process_elevated();
        const bool with_dont_elevate_arg = cmdLine.find("--dont-elevate") != std::string::npos;
        const bool run_elevated_setting = general_settings.GetNamedBoolean(L"run_elevated", false);

        if (elevated && with_dont_elevate_arg && !run_elevated_setting)
        {
            Logger::info("Scheduling restart as non elevated");
            schedule_restart_as_non_elevated();
            result = 0;
        }
        else if (elevated || !run_elevated_setting || with_dont_elevate_arg)
        {
            result = runner(elevated, open_settings, settings_window, openOobe, openScoobe);

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

    // We need to release the mutexes to be able to restart the application
    if (msi_mutex)
    {
        msi_mutex.reset(nullptr);
    }

    if (is_restart_scheduled())
    {
        if (!restart_if_scheduled())
        {
            // If it's not possible to restart non-elevated due to some condition in the user's configuration, user should start PowerToys manually.
            Logger::warn("Scheduled restart failed. Couldn't restart non-elevated. PowerToys exits here because retrying it would just mean failing in a loop.");
        }
    }
    stop_tray_icon();
    return result;
}
