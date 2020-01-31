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

#if _DEBUG && _WIN64
#include "unhandled_exception_handler.h"
#endif

extern "C" IMAGE_DOS_HEADER __ImageBase;

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

int runner()
{
    DPIAware::EnableDPIAwarenessForThisProcess();

#if _DEBUG && _WIN64
//Global error handlers to diagnose errors.
//We prefer this not not show any longer until there's a bug to diagnose.
//init_global_error_handlers();
#endif
    Trace::RegisterProvider();
    winrt::init_apartment();
    start_tray_icon();
    int result;
    try
    {
        chdir_current_executable();
        // Load Powertyos DLLS
        // For now only load known DLLs
        std::unordered_set<std::wstring> known_dlls = {
            L"shortcut_guide.dll",
            L"fancyzones.dll",
            L"PowerRenameExt.dll",
            L"powerpreview.dll"
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

        Trace::EventLaunch(get_product_version());

        result = run_message_loop();
    }
    catch (std::runtime_error& err)
    {
        std::string err_what = err.what();
        MessageBoxW(NULL, std::wstring(err_what.begin(), err_what.end()).c_str(), L"Error", MB_OK | MB_ICONERROR);
        result = -1;
    }
    Trace::UnregisterProvider();
    return result;
}

int WINAPI WinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, LPSTR lpCmdLine, int nCmdShow)
{
    WCHAR username[UNLEN + 1];
    DWORD username_length = UNLEN + 1;
    GetUserNameW(username, &username_length);
    auto runner_mutex = CreateMutexW(NULL, TRUE, (std::wstring(L"Local\\PowerToyRunMutex") + username).c_str());
    if (runner_mutex == NULL || GetLastError() == ERROR_ALREADY_EXISTS)
    {
        // The app is already running
        return 0;
    }
    int result = 0;
    try
    {
        // Singletons initialization order needs to be preserved, first events and
        // then modules to guarantee the reverse destruction order.
        SystemMenuHelperInstace();
        powertoys_events();
        modules();

        auto general_settings = load_general_settings();
        int rvalue = 0;
        if (is_process_elevated() ||
            general_settings.GetNamedBoolean(L"run_elevated", false) == false ||
            strcmp(lpCmdLine, "--dont-elevate") == 0)
        {
            result = runner();
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
        MessageBoxW(NULL, std::wstring(err_what.begin(), err_what.end()).c_str(), GET_RESOURCE_STRING(IDS_ERROR).c_str(), MB_OK | MB_ICONERROR);
        result = -1;
    }
    ReleaseMutex(runner_mutex);
    CloseHandle(runner_mutex);
    if (is_restart_scheduled())
    {
        if (restart_if_scheduled() == false)
        {
            auto text = is_process_elevated() ? GET_RESOURCE_STRING(IDS_COULDNOT_RESTART_NONELEVATED) :
                                                GET_RESOURCE_STRING(IDS_COULDNOT_RESTART_ELEVATED);
            MessageBoxW(NULL, text.c_str(), GET_RESOURCE_STRING(IDS_ERROR).c_str(), MB_OK | MB_ICONERROR);
            result = -1;
        }
    }
    stop_tray_icon();
    return result;
}
