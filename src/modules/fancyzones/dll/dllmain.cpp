#include "pch.h"

#include <common/SettingsAPI/settings_objects.h>
#include <common/debug_control.h>
#include <common/hooks/LowlevelKeyboardEvent.h>
#include <interface/powertoy_module_interface.h>
#include <lib/ZoneSet.h>

#include <lib/Generated Files/resource.h>
#include <lib/trace.h>
#include <lib/Settings.h>
#include <lib/FancyZones.h>
#include <lib/FancyZonesData.h>
#include <lib/FancyZonesWinHookEventIDs.h>
#include <lib/FancyZonesData.cpp>
#include <common/logger/logger.h>
#include <common/utils/logger_helper.h>
#include <common/utils/resources.h>
#include <common/utils/winapi_error.h>
#include <common/utils/window.h>

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
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

class FancyZonesModule : public PowertoyModuleIface
{
public:
    // Return the localized display name of the powertoy
    virtual PCWSTR get_name() override
    {
        return app_name.c_str();
    }

    // Return the non localized key of the powertoy, this will be cached by the runner
    virtual const wchar_t* get_key() override
    {
        return app_key.c_str();
    }

    // Return JSON with the configuration options.
    // These are the settings shown on the settings page along with their current values.
    virtual bool get_config(_Out_ PWSTR buffer, _Out_ int* buffer_size) override
    {
        return m_settings->GetConfig(buffer, buffer_size);
    }

    // Passes JSON with the configuration settings for the powertoy.
    // This is called when the user hits Save on the settings page.
    virtual void set_config(PCWSTR config) override
    {
        m_settings->SetConfig(config);
    }

    // Signal from the Settings editor to call a custom action.
    // This can be used to spawn more complex editors.
    virtual void call_custom_action(const wchar_t* action) override
    {
        m_settings->CallCustomAction(action);
    }

    // Enable the powertoy
    virtual void enable()
    {
        Logger::info("FancyZones enabling");

        if (!m_app)
        {
            InitializeWinhookEventIds();
            Trace::FancyZones::EnableFancyZones(true);
            m_app = MakeFancyZones(reinterpret_cast<HINSTANCE>(&__ImageBase), m_settings, std::bind(&FancyZonesModule::disable, this));
#if defined(DISABLE_LOWLEVEL_HOOKS_WHEN_DEBUGGED)
            const bool hook_disabled = IsDebuggerPresent();
#else
            const bool hook_disabled = false;
#endif
            if (!hook_disabled)
            {
                s_llKeyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, LowLevelKeyboardProc, GetModuleHandle(NULL), NULL);
                if (!s_llKeyboardHook)
                {
                    DWORD errorCode = GetLastError();
                    show_last_error_message(L"SetWindowsHookEx", errorCode, GET_RESOURCE_STRING(IDS_POWERTOYS_FANCYZONES).c_str());
                    auto errorMessage = get_last_error_message(errorCode);
                    Trace::FancyZones::Error(errorCode, errorMessage.has_value() ? errorMessage.value() : L"", L"enable.SetWindowsHookEx");
                }
            }

            std::array<DWORD, 6> events_to_subscribe = {
                EVENT_SYSTEM_MOVESIZESTART,
                EVENT_SYSTEM_MOVESIZEEND,
                EVENT_OBJECT_NAMECHANGE,
                EVENT_OBJECT_UNCLOAKED,
                EVENT_OBJECT_SHOW,
                EVENT_OBJECT_CREATE
            };
            for (const auto event : events_to_subscribe)
            {
                auto hook = SetWinEventHook(event, event, nullptr, WinHookProc, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
                if (hook)
                {
                    m_staticWinEventHooks.emplace_back(hook);
                }
                else
                {
                    MessageBoxW(NULL,
                                GET_RESOURCE_STRING(IDS_WINDOW_EVENT_LISTENER_ERROR).c_str(),
                                GET_RESOURCE_STRING(IDS_POWERTOYS_FANCYZONES).c_str(),
                                MB_OK | MB_ICONERROR);
                }
            }

            if (m_app)
            {
                m_app->Run();
            }
        }
    }

    // Disable the powertoy
    virtual void disable()
    {
        Logger::info("FancyZones disabling");

        Disable(true);
    }

    // Returns if the powertoy is enabled
    virtual bool is_enabled() override
    {
        return m_app != nullptr;
    }

    // Destroy the powertoy and free memory
    virtual void destroy() override
    {
        Disable(false);
        delete this;
    }

    FancyZonesModule()
    {
        app_name = GET_RESOURCE_STRING(IDS_FANCYZONES);
        app_key = NonLocalizable::FancyZonesStr;
        const auto appFolder = PTSettingsHelper::get_module_save_folder_location(app_key);
        const std::filesystem::path logFolder = LoggerHelpers::get_log_folder_path(appFolder);
        
        std::filesystem::path logFilePath(logFolder);
        logFilePath.append(LogSettings::fancyZonesLogPath);
        Logger::init(LogSettings::fancyZonesLoggerName, logFilePath.wstring(), PTSettingsHelper::get_log_settings_file_location());
        
        std::filesystem::path oldLogFolder(appFolder);
        oldLogFolder.append(LogSettings::fancyZonesOldLogPath);
        LoggerHelpers::delete_old_log_folder(oldLogFolder);

        LoggerHelpers::delete_other_versions_log_folders(appFolder, logFolder);

        m_settings = MakeFancyZonesSettings(reinterpret_cast<HINSTANCE>(&__ImageBase), FancyZonesModule::get_name(), FancyZonesModule::get_key());
        FancyZonesDataInstance().LoadFancyZonesData();
        s_instance = this;

        // TODO: consider removing this call since the registry hasn't been used since 0.15
        DeleteFancyZonesRegistryData();
    }

private:
    void Disable(bool const traceEvent)
    {
        if (m_app)
        {
            if (traceEvent)
            {
                Trace::FancyZones::EnableFancyZones(false);
            }
            m_app->Destroy();
            m_app = nullptr;
            m_settings->ResetCallback();

            if (s_llKeyboardHook)
            {
                if (UnhookWindowsHookEx(s_llKeyboardHook))
                {
                    s_llKeyboardHook = nullptr;
                }
            }

            m_staticWinEventHooks.erase(std::remove_if(begin(m_staticWinEventHooks),
                                                       end(m_staticWinEventHooks),
                                                       [](const HWINEVENTHOOK hook) {
                                                           return UnhookWinEvent(hook);
                                                       }),
                                        end(m_staticWinEventHooks));
            if (m_objectLocationWinEventHook)
            {
                if (UnhookWinEvent(m_objectLocationWinEventHook))
                {
                    m_objectLocationWinEventHook = nullptr;
                }
            }
        }
    }

    intptr_t HandleKeyboardHookEvent(LowlevelKeyboardEvent* data) noexcept;
    void HandleWinHookEvent(WinHookEvent* data) noexcept;

    winrt::com_ptr<IFancyZones> m_app;
    winrt::com_ptr<IFancyZonesSettings> m_settings;
    std::wstring app_name;
    //contains the non localized key of the powertoy
    std::wstring app_key;

    static inline FancyZonesModule* s_instance = nullptr;
    static inline HHOOK s_llKeyboardHook = nullptr;

    std::vector<HWINEVENTHOOK> m_staticWinEventHooks;
    HWINEVENTHOOK m_objectLocationWinEventHook = nullptr;

    static LRESULT CALLBACK LowLevelKeyboardProc(int nCode, WPARAM wParam, LPARAM lParam)
    {
        LowlevelKeyboardEvent event;
        if (nCode == HC_ACTION && wParam == WM_KEYDOWN)
        {
            event.lParam = reinterpret_cast<KBDLLHOOKSTRUCT*>(lParam);
            event.wParam = wParam;
            if (s_instance)
            {
                if (s_instance->HandleKeyboardHookEvent(&event) == 1)
                {
                    return 1;
                }
            }
        }
        return CallNextHookEx(NULL, nCode, wParam, lParam);
    }

    static void CALLBACK WinHookProc(HWINEVENTHOOK winEventHook,
                                     DWORD event,
                                     HWND window,
                                     LONG object,
                                     LONG child,
                                     DWORD eventThread,
                                     DWORD eventTime)
    {
        WinHookEvent data{ event, window, object, child, eventThread, eventTime };
        if (s_instance)
        {
            s_instance->HandleWinHookEvent(&data);
        }
    }
};

intptr_t FancyZonesModule::HandleKeyboardHookEvent(LowlevelKeyboardEvent* data) noexcept
{
    return m_app.as<IFancyZonesCallback>()->OnKeyDown(data->lParam);
}

void FancyZonesModule::HandleWinHookEvent(WinHookEvent* data) noexcept
{
    auto fzCallback = m_app.as<IFancyZonesCallback>();
    switch (data->event)
    {
    case EVENT_SYSTEM_MOVESIZESTART:
    {
        fzCallback->HandleWinHookEvent(data);
        if (!m_objectLocationWinEventHook)
        {
            m_objectLocationWinEventHook = SetWinEventHook(EVENT_OBJECT_LOCATIONCHANGE,
                                                           EVENT_OBJECT_LOCATIONCHANGE,
                                                           nullptr,
                                                           WinHookProc,
                                                           0,
                                                           0,
                                                           WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
        }
    }
    break;

    case EVENT_SYSTEM_MOVESIZEEND:
    {
        if (UnhookWinEvent(m_objectLocationWinEventHook))
        {
            m_objectLocationWinEventHook = nullptr;
        }
        fzCallback->HandleWinHookEvent(data);
    }
    break;

    case EVENT_OBJECT_LOCATIONCHANGE:
    {
        fzCallback->HandleWinHookEvent(data);
    }
    break;

    case EVENT_OBJECT_NAMECHANGE:
    {
        // The accessibility name of the desktop window changes whenever the user
        // switches virtual desktops.
        if (data->hwnd == GetDesktopWindow())
        {
            m_app.as<IFancyZonesCallback>()->VirtualDesktopChanged();
        }
    }
    break;

    case EVENT_OBJECT_UNCLOAKED:
    case EVENT_OBJECT_SHOW:
    case EVENT_OBJECT_CREATE:
    {
        fzCallback->HandleWinHookEvent(data);
    }
    break;

    default:
        break;
    }
}

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new FancyZonesModule();
}
