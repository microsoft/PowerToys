#include "pch.h"

#include <interface/powertoy_module_interface.h>

#include <common/interop/shared_constants.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/utils/EventWaiter.h>

#include "../ModuleConstants.h"
#include "trace.h"

extern "C" IMAGE_DOS_HEADER __ImageBase;

BOOL APIENTRY DllMain(HMODULE, DWORD reason, LPVOID)
{
    switch (reason)
    {
    case DLL_PROCESS_ATTACH:
        Trace::RegisterProvider();
        break;
    case DLL_PROCESS_DETACH:
        Trace::UnregisterProvider();
        break;
    }

    return TRUE;
}

namespace
{
    constexpr wchar_t MODULE_NAME[] = L"DEPiP";
    constexpr wchar_t MODULE_DESCRIPTION[] =
        L"Mirror any connected display in a movable PiP window.";
}

class DEPiPModuleInterface : public PowertoyModuleIface
{
public:
    DEPiPModuleInterface()
    {
        m_exitEvent = CreateDefaultEvent(DEPiPConstants::ExitEvent);
        m_reloadSettingsEvent = CreateDefaultEvent(DEPiPConstants::ReloadSettingsEvent);
        m_showEvent = CreateDefaultEvent(CommonSharedConstants::SHOW_DEPIP_EVENT);
        m_showEventWaiter.start(CommonSharedConstants::SHOW_DEPIP_EVENT, [this](DWORD error) {
            if (m_enabled && error == ERROR_SUCCESS)
            {
                launch();
            }
        });
    }

    void destroy() override
    {
        disable();
        if (m_exitEvent)
        {
            CloseHandle(m_exitEvent);
        }
        if (m_showEvent)
        {
            CloseHandle(m_showEvent);
        }
        if (m_reloadSettingsEvent)
        {
            CloseHandle(m_reloadSettingsEvent);
        }
        delete this;
    }

    const wchar_t* get_name() override
    {
        return MODULE_NAME;
    }

    const wchar_t* get_key() override
    {
        return MODULE_NAME;
    }

    bool get_config(wchar_t* buffer, int* bufferSize) override
    {
        HINSTANCE instance = reinterpret_cast<HINSTANCE>(&__ImageBase);
        PowerToysSettings::Settings settings(instance, get_name());
        settings.set_description(MODULE_DESCRIPTION);
        settings.set_overview_link(L"https://aka.ms/powertoys");
        return settings.serialize_to_buffer(buffer, bufferSize);
    }

    void set_config(const wchar_t* config) override
    {
        try
        {
            auto values = PowerToysSettings::PowerToyValues::from_json_string(config, get_key());
            values.save_to_settings_file();
            if (m_reloadSettingsEvent)
            {
                SetEvent(m_reloadSettingsEvent);
            }
        }
        catch (const std::exception& error)
        {
            OutputDebugStringA(error.what());
        }
    }

    void enable() override
    {
        m_enabled = true;
        Trace::Enable(true);
    }

    void disable() override
    {
        if (m_exitEvent)
        {
            SetEvent(m_exitEvent);
        }

        if (m_process)
        {
            WaitForSingleObject(m_process, 3000);
            CloseHandle(m_process);
            m_process = nullptr;
        }

        m_enabled = false;
        Trace::Enable(false);
    }

    bool is_enabled() override
    {
        if (m_process && WaitForSingleObject(m_process, 0) != WAIT_TIMEOUT)
        {
            CloseHandle(m_process);
            m_process = nullptr;
        }
        return m_enabled;
    }

    bool is_enabled_by_default() const override
    {
        return false;
    }

private:
    bool is_process_running()
    {
        return m_process && WaitForSingleObject(m_process, 0) == WAIT_TIMEOUT;
    }

    void bring_process_to_front()
    {
        auto enumWindows = [](HWND window, LPARAM parameter) -> BOOL {
            const HANDLE process = reinterpret_cast<HANDLE>(parameter);
            DWORD windowProcessId = 0;
            GetWindowThreadProcessId(window, &windowProcessId);
            if (GetProcessId(process) == windowProcessId)
            {
                SetForegroundWindow(window);
                return FALSE;
            }
            return TRUE;
        };

        EnumWindows(enumWindows, reinterpret_cast<LPARAM>(m_process));
    }

    void launch()
    {
        if (is_process_running())
        {
            bring_process_to_front();
            return;
        }

        if (m_process)
        {
            CloseHandle(m_process);
            m_process = nullptr;
        }

        if (m_exitEvent)
        {
            ResetEvent(m_exitEvent);
        }

        std::wstring executablePath(MAX_PATH, L'\0');
        const DWORD pathLength = SearchPathW(
            nullptr,
            DEPiPConstants::ExecutableName,
            nullptr,
            static_cast<DWORD>(executablePath.size()),
            executablePath.data(),
            nullptr);
        if (pathLength == 0 || pathLength >= executablePath.size())
        {
            return;
        }
        executablePath.resize(pathLength);

        std::wstring commandLine = L"\"" + executablePath + L"\"";
        STARTUPINFOW startupInfo{ sizeof(startupInfo) };
        PROCESS_INFORMATION processInfo{};
        if (!CreateProcessW(
                executablePath.c_str(),
                commandLine.data(),
                nullptr,
                nullptr,
                FALSE,
                CREATE_SUSPENDED,
                nullptr,
                nullptr,
                &startupInfo,
                &processInfo))
        {
            return;
        }

        AllowSetForegroundWindow(processInfo.dwProcessId);
        if (ResumeThread(processInfo.hThread) == static_cast<DWORD>(-1))
        {
            TerminateProcess(processInfo.hProcess, 1);
            CloseHandle(processInfo.hThread);
            CloseHandle(processInfo.hProcess);
            return;
        }
        CloseHandle(processInfo.hThread);
        m_process = processInfo.hProcess;
    }

    bool m_enabled = false;
    HANDLE m_exitEvent = nullptr;
    HANDLE m_showEvent = nullptr;
    HANDLE m_reloadSettingsEvent = nullptr;
    HANDLE m_process = nullptr;
    EventWaiter m_showEventWaiter;
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new DEPiPModuleInterface();
}
