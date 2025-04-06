// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"

#include <interface/powertoy_module_interface.h>

#include <common/logger/logger.h>
#include <common/utils/logger_helper.h>
#include <common/SettingsAPI/settings_helpers.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/utils/resources.h>
#include <common/utils/package.h>
#include <common/utils/process_path.h>
#include <common/interop/shared_constants.h>
#include <Psapi.h>
#include <TlHelp32.h>
#include <common/utils/winapi_error.h>

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
    bool m_enabled = false;

    std::wstring app_name;

    //contains the non localized key of the powertoy
    std::wstring app_key;

    HANDLE m_hTerminateEvent;

    void LaunchApp(const std::wstring& appPath, const std::wstring& commandLineArgs, bool elevated)
    {
        std::wstring dir = std::filesystem::path(appPath).parent_path();

        SHELLEXECUTEINFO sei = { 0 };
        sei.cbSize = sizeof(SHELLEXECUTEINFO);
        sei.hwnd = nullptr;
        sei.fMask = SEE_MASK_NOCLOSEPROCESS | SEE_MASK_NO_CONSOLE;
        sei.lpVerb = elevated ? L"runas" : L"open";
        sei.lpFile = appPath.c_str();
        sei.lpParameters = commandLineArgs.c_str();
        sei.lpDirectory = dir.c_str();
        sei.nShow = SW_SHOWNORMAL;

        if (!ShellExecuteEx(&sei))
        {
            std::wstring error = get_last_error_or_default(GetLastError());
            Logger::error(L"Failed to launch process. {}", error);
        }
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
    CmdPal()
    {
        app_name = L"CmdPal";
        app_key = L"CmdPal";
        LoggerHelpers::init_logger(app_key, L"ModuleInterface", "CmdPal");

        m_hTerminateEvent = CreateDefaultEvent(CommonSharedConstants::CMDPAL_EXIT_EVENT);
    }

    ~CmdPal()
    {
        if (m_enabled)
        {
        }
        m_enabled = false;
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

        m_enabled = true;

        try
        {
            if (!package::GetRegisteredPackage(L"Microsoft.CommandPalette", false).has_value())
            {
                Logger::info(L"CmdPal not installed. Installing...");

                std::wstring installationFolder = get_module_folderpath();
#if _DEBUG
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
        }
        catch (std::exception& e)
        {
            std::string errorMessage{ "Exception thrown while trying to install CmdPal package: " };
            errorMessage += e.what();
            Logger::error(errorMessage);
        }

#if _DEBUG
        LaunchApp(std::wstring{ L"shell:AppsFolder\\" } + L"Microsoft.CommandPalette.Dev_8wekyb3d8bbwe!App", L"RunFromPT", false);
#else
        LaunchApp(std::wstring{ L"shell:AppsFolder\\" } + L"Microsoft.CommandPalette_8wekyb3d8bbwe!App", L"RunFromPT", false);
#endif
    }

    virtual void disable()
    {
        Logger::trace("CmdPal::disable()");
        TerminateCmdPal();

        m_enabled = false;
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
        return m_enabled;
    }
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new CmdPal();
}
