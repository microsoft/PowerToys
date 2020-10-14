#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <common/settings_objects.h>
#include <common/LowlevelKeyboardEvent.h>
#include <common/LowlevelMouseEvent.h>
#include "trace.h"
#include "altdrag/lib/AltDrag/AltDrag.h"
#include "altdrag/lib/AltDrag/Settings.h"

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

// The PowerToy name that will be shown in the settings.
const static wchar_t* MODULE_NAME = L"AltDrag";
// Add a description that will we shown in the module settings page.
const static wchar_t* MODULE_DESC = L"This is the description";

// Implement the PowerToy Module Interface and all the required methods.
class AltDragModule : public PowertoyModuleIface
{
private:
    // The PowerToy state.
    bool m_enabled = false;

public:
    // Constructor
    AltDragModule()
    {

        app_name = L"AltDrag";
        m_settings = MakeAltDragSettings(reinterpret_cast<HINSTANCE>(&__ImageBase), AltDragModule::get_name());
        //FancyZonesDataInstance().LoadFancyZonesData();
        s_instance = this;
        //init_settings();
    };

    // Destroy the powertoy and free memory
    virtual void destroy() override
    {
        delete this;
    }

    // Return the display name of the powertoy, this will be cached by the runner
    virtual const wchar_t* get_name() override
    {
        return MODULE_NAME;
    }


    // Return JSON with the configuration options.
    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        return m_settings->GetConfig(buffer, buffer_size);
    }

    // Signal from the Settings editor to call a custom action.
    // This can be used to spawn more complex editors.
    virtual void call_custom_action(const wchar_t* action) override
    {
        m_settings->CallCustomAction(action);
    }

    // Called by the runner to pass the updated settings values as a serialized JSON.
    virtual void set_config(const wchar_t* config) override
    {
        m_settings->SetConfig(config);
    }

    // Enable the powertoy
    virtual void enable()
    {
        if (!m_app)
        {
            //Trace::FancyZones::EnableFancyZones(true);
            m_app = MakeAltDrag(reinterpret_cast<HINSTANCE>(&__ImageBase), m_settings, std::bind(&AltDragModule::disable, this));
            const bool hook_disabled = false;
            if (!hook_disabled)
            {
                s_llKeyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, LowLevelKeyboardProc, GetModuleHandle(NULL), NULL);
                s_llMouseHook = SetWindowsHookEx(WH_MOUSE_LL, LowLevelMouseProc, GetModuleHandle(NULL), NULL);
                if (!s_llKeyboardHook)
                {
                    DWORD errorCode = GetLastError();
                    //show_last_error_message(L"SetWindowsHookEx", errorCode, GET_RESOURCE_STRING(IDS_POWERTOYS_FANCYZONES).c_str());
                    auto errorMessage = get_last_error_message(errorCode);
                    //Trace::FancyZones::Error(errorCode, errorMessage.has_value() ? errorMessage.value() : L"", L"enable.SetWindowsHookEx");
                }
                if (!s_llMouseHook)
                {
                    DWORD errorCode = GetLastError();
                    //show_last_error_message(L"SetWindowsHookEx", errorCode, GET_RESOURCE_STRING(IDS_POWERTOYS_FANCYZONES).c_str());
                    auto errorMessage = get_last_error_message(errorCode);
                    //Trace::FancyZones::Error(errorCode, errorMessage.has_value() ? errorMessage.value() : L"", L"enable.SetWindowsHookEx");
                }
            }

            if (m_app)
            {
                m_app->Run();
            }
        }

        m_enabled = true;
    }

    // Disable the powertoy
    virtual void disable()
    {
        m_enabled = false;
        Disable(true);
    }


     void Disable(bool const traceEvent)
    {
        if (m_app)
        {
            if (traceEvent)
            {
                //Trace::FancyZones::EnableFancyZones(false);
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

            if (s_llMouseHook)
            {
                if (UnhookWindowsHookEx(s_llMouseHook))
                {
                    s_llMouseHook = nullptr;                
                }
            }
        }
    }
    intptr_t HandleKeyboardHookEvent(LowlevelKeyboardEvent* data) noexcept;
    intptr_t HandleMouseHookEvent(LowlevelMouseEvent* data) noexcept;

    winrt::com_ptr<IAltDrag> m_app;
    winrt::com_ptr<IAltDragSettings> m_settings;
    std::wstring app_name;

    static inline AltDragModule* s_instance;
    static inline HHOOK s_llKeyboardHook;
    static inline HHOOK s_llMouseHook;

    static LRESULT CALLBACK LowLevelKeyboardProc(int nCode, WPARAM wParam, LPARAM lParam)
    {
        LowlevelKeyboardEvent event;
        if (nCode == HC_ACTION)
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


    static LRESULT CALLBACK LowLevelMouseProc(int nCode, WPARAM wParam, LPARAM lParam)
    {
        LowlevelMouseEvent event;
        if (nCode == HC_ACTION)
        {
            event.lParam = reinterpret_cast<MSLLHOOKSTRUCT*>(lParam);
            event.wParam = wParam;
            if (s_instance)
            {
                if (s_instance->HandleMouseHookEvent(&event) == 1)
                {
                    return 1;
                }
            }
        }
        return CallNextHookEx(NULL, nCode, wParam, lParam);
    }

    // Returns if the powertoys is enabled
    virtual bool is_enabled() override
    {
        return m_enabled;
    }
};


intptr_t AltDragModule::HandleKeyboardHookEvent(LowlevelKeyboardEvent* data) noexcept
{
    return m_app.as<IAltDragCallback>()->OnKeyEvent(data);
}

intptr_t AltDragModule::HandleMouseHookEvent(LowlevelMouseEvent* data) noexcept
{
    return m_app.as<IAltDragCallback>()->OnMouseEvent(data);
}


extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new AltDragModule();
}