#include "pch.h"
#include <common/settings_objects.h>
#include <common/common.h>
#include <interface/powertoy_module_interface.h>
#include <interface/lowlevel_keyboard_event_data.h>
#include <interface/win_hook_event_data.h>
#include <lib/ZoneSet.h>

#include <lib/resource.h>
#include <lib/trace.h>
#include <lib/Settings.h>
#include <lib/FancyZones.h>
#include <lib/FancyZonesWinHookEventIDs.h>

extern "C" IMAGE_DOS_HEADER __ImageBase;

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
    // Return the display name of the powertoy, this will be cached
    virtual PCWSTR get_name() override
    {
        return app_name.c_str();
    }

    // Return array of the names of all events that this powertoy listens for, with
    // nullptr as the last element of the array. Nullptr can also be returned for empty list.
    virtual PCWSTR* get_events() override
    {
        return nullptr;
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
        if (!m_app)
        {
            InitializeWinhookEventIds();
            Trace::FancyZones::EnableFancyZones(true);
            m_app = MakeFancyZones(reinterpret_cast<HINSTANCE>(&__ImageBase), m_settings);

            s_llKeyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, LowLevelKeyboardProc, GetModuleHandle(NULL), NULL);
            if (!s_llKeyboardHook)
            {
                MessageBoxW(NULL, L"Cannot install keyboard listener.", L"PowerToys - FancyZones", MB_OK | MB_ICONERROR);
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
                    MessageBoxW(NULL, L"Cannot install Windows event listener.", L"PowerToys - FancyZones", MB_OK | MB_ICONERROR);
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
        Disable(true);
    }

    // Returns if the powertoy is enabled
    virtual bool is_enabled() override
    {
        return (m_app != nullptr);
    }

    // PowertoyModuleIface method, unused
    virtual intptr_t signal_event(const wchar_t* name, intptr_t data) override
    {
        return 0;
    }

    virtual void register_system_menu_helper(PowertoySystemMenuIface* helper) override {}
    virtual void signal_system_menu_action(const wchar_t* name) override {}

    // Destroy the powertoy and free memory
    virtual void destroy() override
    {
        Disable(false);
        delete this;
    }

    FancyZonesModule()
    {
        app_name = GET_RESOURCE_STRING(IDS_FANCYZONES);
        m_settings = MakeFancyZonesSettings(reinterpret_cast<HINSTANCE>(&__ImageBase), FancyZonesModule::get_name());
        JSONHelpers::FancyZonesDataInstance().LoadFancyZonesData();
        s_instance = this;
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

    static inline FancyZonesModule* s_instance;
    static inline HHOOK s_llKeyboardHook;

    std::vector<HWINEVENTHOOK> m_staticWinEventHooks;
    HWINEVENTHOOK m_objectLocationWinEventHook;

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
            Trace::VirtualDesktopChanged();
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
