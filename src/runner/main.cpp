#include "pch.h"
#include <ShellScalingApi.h>
#include <lmcons.h>
#include <filesystem>
#include "tray_icon.h"
#include "powertoy_module.h"
#include "lowlevel_keyboard_event.h"
#include "trace.h"
#include "general_settings.h"
#include "restart_elevated.h"
#include "resource.h"

#include <common/common.h>
#include <common/dpi_aware.h>

#include <common/msi_to_msix_upgrade_lib/msi_to_msix_upgrade.h>
#include <common/winstore.h>
#include <common/notifications.h>

#if _DEBUG && _WIN64
#include "unhandled_exception_handler.h"
#endif

extern "C" IMAGE_DOS_HEADER __ImageBase;

namespace localized_strings
{
    const wchar_t MSI_VERSION_IS_ALREADY_RUNNING[] = L"An older version of PowerToys is already running.";
}

namespace
{
    const wchar_t MSI_VERSION_MUTEX_NAME[] = L"Local\\PowerToyRunMutex";
    const wchar_t MSIX_VERSION_MUTEX_NAME[] = L"Local\\PowerToyMSIXRunMutex";
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

bool start_msi_uninstallation_sequence()
{
    const auto package_path = get_msi_package_path();

    if (package_path.empty())
    {
        // No MSI version detected
        return true;
    }

    if (!offer_msi_uninstallation())
    {
        // User declined to uninstall or opted for "Don't show again"
        return false;
    }

    std::wstring action_runner_path{ winrt::Windows::ApplicationModel::Package::Current().InstalledLocation().Path() };
    action_runner_path += L"\\action_runner.exe";
    SHELLEXECUTEINFOW sei{ sizeof(sei) };
    sei.fMask = { SEE_MASK_FLAG_NO_UI | SEE_MASK_NOASYNC | SEE_MASK_NOCLOSEPROCESS };
    sei.lpFile = action_runner_path.c_str();
    sei.nShow = SW_SHOWNORMAL;
    sei.lpParameters = L"-uninstall_msi";
    ShellExecuteExW(&sei);
    WaitForSingleObject(sei.hProcess, INFINITE);
    DWORD exit_code = 0;
    GetExitCodeProcess(sei.hProcess, &exit_code);
    CloseHandle(sei.hProcess);
    return exit_code == 0;
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
        if (winstore::running_as_packaged())
        {
            notifications::register_background_toast_handler();
        }

        chdir_current_executable();
        // Load Powertyos DLLS
        // For now only load known DLLs
        std::unordered_set<std::wstring> known_dlls = {
            L"shortcut_guide.dll",
            L"fancyzones.dll",
            L"PowerRenameExt.dll"
        };
        for (auto& file : std::filesystem::directory_iterator(L"modules/"))
        {
            if (file.path().extension() != L".dll")
                continue;
            if (known_dlls.find(file.path().filename()) == known_dlls.end())
                continue;
            try
            {
                auto module = load_powertoy(file.path().wstring());
                modules().emplace(module.get_name(), std::move(module));
            }
            catch (...)
            {
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

int WINAPI WinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, LPSTR lpCmdLine, int nCmdShow)
{
    auto msix_mutex = create_runner_mutex(true);
    const bool msix_mutex_failed_to_lock = !msix_mutex;
    if (msix_mutex_failed_to_lock)
    {
        // The app is already running
        return 0;
    }

    auto msi_mutex = create_runner_mutex(false);
    const bool msi_mutex_already_taken = !msi_mutex;
    if (msi_mutex_already_taken)
    {
        const bool declined_uninstall = !start_msi_uninstallation_sequence();

        if (declined_uninstall)
        {
            // Warn and exit if msi version is already running
            notifications::show_toast(localized_strings::MSI_VERSION_IS_ALREADY_RUNNING);
            // Wait 1 second before exiting, since if we exit immediately, the toast notification becomes lost
            Sleep(1000);
            return 0;
        }
    }

    int result = 0;
    try
    {
        winrt::init_apartment();

        if (winstore::running_as_packaged())
        {
            notifications::register_background_toast_handler();

            std::thread{ [] {
                start_msi_uninstallation_sequence();
            } }.detach();
        }
        // Singletons initialization order needs to be preserved, first events and
        // then modules to guarantee the reverse destruction order.
        SystemMenuHelperInstace();
        powertoys_events();
        modules();

        auto general_settings = load_general_settings();
        int rvalue = 0;
        bool isProcessElevated = is_process_elevated();
        if (isProcessElevated ||
            general_settings.GetNamedBoolean(L"run_elevated", false) == false ||
            strcmp(lpCmdLine, "--dont-elevate") == 0)
        {
            result = runner(isProcessElevated);
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
