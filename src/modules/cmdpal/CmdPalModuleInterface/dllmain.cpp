// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"

#include <interface/powertoy_module_interface.h>

#include <atomic>
#include <common/logger/logger.h>
#include <common/utils/logger_helper.h>
#include <common/SettingsAPI/settings_helpers.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/utils/resources.h>
#include <common/utils/package.h>
#include <common/utils/process_path.h>
#include <common/utils/winapi_error.h>
#include <common/interop/shared_constants.h>
#include <Psapi.h>
#include <TlHelp32.h>
#include <thread>

HINSTANCE g_hInst_cmdPal = 0;

BOOL APIENTRY DllMain(HMODULE hInstance,
                      DWORD ul_reason_for_call,
                      LPVOID)
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
        g_hInst_cmdPal = hInstance;
        break;
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
    case DLL_PROCESS_DETACH:
        break;
    }
    return TRUE;
}

class CmdPal : public PowertoyModuleIface
{
private:
    std::wstring app_name;

    //contains the non localized key of the powertoy
    std::wstring app_key;

    HANDLE m_hTerminateEvent;

    // Track if this is the first call to enable
    bool firstEnableCall = true;

    static bool LaunchApp(const std::wstring& appPath, const std::wstring& commandLineArgs, bool elevated, bool silentFail)
    {
        std::wstring dir = std::filesystem::path(appPath).parent_path();

        SHELLEXECUTEINFO sei = { 0 };
        sei.cbSize = sizeof(SHELLEXECUTEINFO);
        sei.hwnd = nullptr;
        sei.fMask = SEE_MASK_NOCLOSEPROCESS | SEE_MASK_NO_CONSOLE;
        if (silentFail)
        {
            sei.fMask = sei.fMask | SEE_MASK_FLAG_NO_UI;
        }
        sei.lpVerb = elevated ? L"runas" : L"open";
        sei.lpFile = appPath.c_str();
        sei.lpParameters = commandLineArgs.c_str();
        sei.lpDirectory = dir.c_str();
        sei.nShow = SW_SHOWNORMAL;

        if (!ShellExecuteEx(&sei))
        {
            std::wstring error = get_last_error_or_default(GetLastError());
            Logger::error(L"Failed to launch process. {}", error);
            return false;
        }

        m_launched.store(true);
        return true;
    }

    std::vector<DWORD> GetProcessesIdByName(const std::wstring& processName)
    {
        std::vector<DWORD> processIds;
        HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);

        if (snapshot != INVALID_HANDLE_VALUE)
        {
            PROCESSENTRY32 processEntry;
            processEntry.dwSize = sizeof(PROCESSENTRY32);

            if (Process32First(snapshot, &processEntry))
            {
                do
                {
                    if (_wcsicmp(processEntry.szExeFile, processName.c_str()) == 0)
                    {
                        processIds.push_back(processEntry.th32ProcessID);
                    }
                } while (Process32Next(snapshot, &processEntry));
            }

            CloseHandle(snapshot);
        }

        return processIds;
    }

    void TerminateCmdPal()
    {
        auto processIds = GetProcessesIdByName(L"Microsoft.CmdPal.UI.exe");

        if (processIds.size() == 0)
        {
            Logger::trace(L"Nothing To PROCESS_TERMINATE");
            return;
        }

        for (DWORD pid : processIds)
        {
            HANDLE hProcess = OpenProcess(PROCESS_TERMINATE, FALSE, pid);

            if (hProcess != NULL)
            {
                SetEvent(m_hTerminateEvent);

                // Wait for 1.5 seconds for the process to end correctly and stop etw tracer
                WaitForSingleObject(hProcess, 1500);

                TerminateProcess(hProcess, 0);
                CloseHandle(hProcess);
            }
        }
    }

public:
    static std::atomic<bool> m_enabled;
    static std::atomic<bool> m_launched;

    CmdPal()
    {
        app_name = L"CmdPal";
        app_key = L"CmdPal";
        LoggerHelpers::init_logger(app_key, L"ModuleInterface", "CmdPal");

        m_hTerminateEvent = CreateDefaultEvent(CommonSharedConstants::CMDPAL_EXIT_EVENT);
    }

    ~CmdPal()
    {
        CmdPal::m_enabled.store(false);
    }

    // Destroy the powertoy and free memory
    virtual void destroy() override
    {
        Logger::trace("CmdPal::destroy()");
        TerminateCmdPal();
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
        return powertoys_gpo::getConfiguredCmdPalEnabledValue();
    }

    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        // Create a Settings object.
        PowerToysSettings::Settings settings(hinstance, get_name());

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    virtual void call_custom_action(const wchar_t* /*action*/) override
    {
    }

    virtual void set_config(const wchar_t* config) override
    {
        try
        {
            // Parse the input JSON string.
            PowerToysSettings::PowerToyValues values =
                PowerToysSettings::PowerToyValues::from_json_string(config, get_key());

            // If you don't need to do any custom processing of the settings, proceed
            // to persists the values calling:
            values.save_to_settings_file();
            // Otherwise call a custom function to process the settings before saving them to disk:
            // save_settings();
        }
        catch (std::exception&)
        {
            // Improper JSON.
        }
    }

    virtual void enable()
    {
        Logger::trace("CmdPal::enable()");

        CmdPal::m_enabled.store(true);

        std::wstring packageName = L"Microsoft.CommandPalette";
        std::wstring launchPath = L"x-cmdpal://background";
#ifdef IS_DEV_BRANDING
        packageName = L"Microsoft.CommandPalette.Dev";
#endif

        if (!package::GetRegisteredPackage(packageName, false).has_value())
        {
            try
            {
                Logger::info(L"CmdPal not installed. Installing...");

                std::wstring installationFolder = get_module_folderpath();
#ifdef _DEBUG
                std::wstring archSubdir = L"x64";
#ifdef _M_ARM64
                archSubdir = L"ARM64";
#endif
                auto msix = package::FindMsixFile(installationFolder + L"\\WinUI3Apps\\CmdPal\\AppPackages\\Microsoft.CmdPal.UI_0.0.1.0_Debug_Test\\", false);
                auto dependencies = package::FindMsixFile(installationFolder + L"\\WinUI3Apps\\CmdPal\\AppPackages\\Microsoft.CmdPal.UI_0.0.1.0_Debug_Test\\Dependencies\\" + archSubdir + L"\\", true);
#else
                auto msix = package::FindMsixFile(installationFolder + L"\\WinUI3Apps\\CmdPal\\", false);
                auto dependencies = package::FindMsixFile(installationFolder + L"\\WinUI3Apps\\CmdPal\\Dependencies\\", true);
#endif

                if (!msix.empty())
                {
                    auto msixPath = msix[0];

                    if (!package::RegisterPackage(msixPath, dependencies))
                    {
                        Logger::error(L"Failed to install CmdPal package");
                    }
                }
            }
            catch (std::exception& e)
            {
                std::string errorMessage{ "Exception thrown while trying to install CmdPal package: " };
                errorMessage += e.what();
                Logger::error(errorMessage);
            }
        }

        if (!package::GetRegisteredPackage(packageName, false).has_value())
        {
            Logger::error("Cmdpal is not registered, quit..");
            return;
        }

        if (!firstEnableCall)
        {
            Logger::trace("Not first attempt, try to launch");
            LaunchApp(launchPath, L"", false /*no elevated*/, false /*error pop up*/);
        }
        else
        {
            // If first time enable, do retry launch.
            Logger::trace("First attempt, try to launch");
            std::thread launchThread(&CmdPal::RetryLaunch, launchPath);
            launchThread.detach();
        }

        firstEnableCall = false;
    }

    virtual void disable()
    {
        Logger::trace("CmdPal::disable()");
        TerminateCmdPal();

        CmdPal::m_enabled.store(false);
    }

    static void RetryLaunch(std::wstring path)
    {
        const int base_delay_milliseconds = 1000;
        int max_retry = 9; // 2**9 - 1 seconds. Control total wait time within 10 min.
        int retry = 0;
        do
        {
            auto launch_result = LaunchApp(path, L"", false, retry < max_retry);
            if (launch_result)
            {
                Logger::info(L"CmdPal launched successfully after {} retries.", retry);
                return;
            }
            else
            {
                Logger::error(L"Retry {} launch CmdPal launch failed.", retry);
            }

            // When we got max retry, we don't need to wait for the next retry.
            if (retry < max_retry)
            {
                int delay = base_delay_milliseconds * (1 << (retry));
                std::this_thread::sleep_for(std::chrono::milliseconds(delay));
            }
            ++retry;
        } while (retry <= max_retry && m_enabled.load() && !m_launched.load());

        if (!m_enabled.load() || m_launched.load())
        {
            Logger::error(L"Retry cancelled. CmdPal is disabled or already launched.");
        }
        else
        {
            Logger::error(L"CmdPal launch failed after {} attempts.", retry);
        }
    }

    virtual bool on_hotkey(size_t) override
    {
        return false;
    }

    virtual size_t get_hotkeys(Hotkey*, size_t) override
    {
        return 0;
    }

    virtual bool is_enabled() override
    {
        return CmdPal::m_enabled.load();
    }
};

std::atomic<bool> CmdPal::m_enabled{ false };
std::atomic<bool> CmdPal::m_launched{ false };

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new CmdPal();
}