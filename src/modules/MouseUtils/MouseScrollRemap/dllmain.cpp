#include "pch.h"
#include "../../../interface/powertoy_module_interface.h"
#include "../../../common/SettingsAPI/settings_objects.h"
#include "trace.h"
#include "../../../common/utils/process_path.h"
#include "../../../common/utils/resources.h"
#include "../../../common/logger/logger.h"
#include "../../../common/utils/logger_helper.h"
#include <atomic>
#include <windows.h>
#include "resource.h"

extern "C" IMAGE_DOS_HEADER __ImageBase;

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID /*lpReserved*/)
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
const static wchar_t* MODULE_NAME = L"MouseScrollRemap";
// Add a description that will we shown in the module settings page.
const static wchar_t* MODULE_DESC = L"Remap Shift+MouseWheel to horizontal scrolling";

// Forward declaration
class MouseScrollRemap;

// Global instance pointer for the mouse hook
static MouseScrollRemap* g_mouseScrollRemapInstance = nullptr;

// Implement the PowerToy Module Interface and all the required methods.
class MouseScrollRemap : public PowertoyModuleIface
{
private:
    // The PowerToy state.
    bool m_enabled = false;
    
    // Mouse hook
    HHOOK m_mouseHook = nullptr;
    std::atomic<bool> m_hookActive{ false };

public:
    // Constructor
    MouseScrollRemap()
    {
        LoggerHelpers::init_logger(MODULE_NAME, L"ModuleInterface", LogSettings::mouseScrollRemapLoggerName);
        g_mouseScrollRemapInstance = this; // Set global instance pointer
    };

    // Destroy the powertoy and free memory
    virtual void destroy() override
    {
        StopMouseHook();
        g_mouseScrollRemapInstance = nullptr; // Clear global instance pointer
        delete this;
    }

    // Return the localized display name of the powertoy
    virtual const wchar_t* get_name() override
    {
        return MODULE_NAME;
    }

    // Return the non localized key of the powertoy, this will be cached by the runner
    virtual const wchar_t* get_key() override
    {
        return MODULE_NAME;
    }

    // Return the configured status for the gpo policy for the module
    virtual powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
    {
        return powertoys_gpo::gpo_rule_configured_t::gpo_rule_configured_not_configured;
    }

    // Return JSON with the configuration options.
    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        PowerToysSettings::Settings settings(hinstance, get_name());

        settings.set_description(IDS_MOUSESCROLLREMAP_NAME);
        settings.set_icon_key(L"pt-mouse-scroll-remap");

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    // Signal from the Settings editor to call a custom action.
    // This can be used to spawn more complex editors.
    virtual void call_custom_action(const wchar_t* /*action*/) override {}

    // Called by the runner to pass the updated settings values as a serialized JSON.
    virtual void set_config(const wchar_t* config) override
    {
        // No specific settings to parse for now
    }

    // Enable the powertoy
    virtual void enable()
    {
        m_enabled = true;
        Trace::EnableMouseScrollRemap(true);
        
        StartMouseHook();
        Logger::info("MouseScrollRemap enabled - mouse hook started");
    }

    // Disable the powertoy
    virtual void disable()
    {
        m_enabled = false;
        Trace::EnableMouseScrollRemap(false);
        StopMouseHook();
        Logger::info("MouseScrollRemap disabled - mouse hook stopped");
    }

    // Returns if the powertoys is enabled
    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    // Returns whether the PowerToys should be enabled by default
    virtual bool is_enabled_by_default() const override
    {
        return false;
    }

private:
    void StartMouseHook()
    {
        if (m_mouseHook || m_hookActive)
        {
            Logger::info("MouseScrollRemap mouse hook already active");
            return;
        }
        
        m_mouseHook = SetWindowsHookEx(WH_MOUSE_LL, MouseHookProc, GetModuleHandle(nullptr), 0);
        if (m_mouseHook)
        {
            m_hookActive = true;
            Logger::info("MouseScrollRemap mouse hook started successfully");
        }
        else
        {
            DWORD error = GetLastError();
            Logger::error(L"Failed to install MouseScrollRemap mouse hook, error: {}", error);
        }
    }

    void StopMouseHook()
    {
        if (m_mouseHook)
        {
            UnhookWindowsHookEx(m_mouseHook);
            m_mouseHook = nullptr;
            m_hookActive = false;
            Logger::info("MouseScrollRemap mouse hook stopped");
        }
    }

    static LRESULT CALLBACK MouseHookProc(int nCode, WPARAM wParam, LPARAM lParam)
    {
        if (nCode >= 0 && g_mouseScrollRemapInstance && g_mouseScrollRemapInstance->m_hookActive)
        {
            if (wParam == WM_MOUSEWHEEL)
            {
                auto* pMouseStruct = reinterpret_cast<MSLLHOOKSTRUCT*>(lParam);
                
                // Check if Shift is pressed but Ctrl is NOT pressed
                // Using GetAsyncKeyState is acceptable here as this hook is not on a critical path
                // and only triggers when the user actively scrolls
                bool shiftPressed = (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;
                bool ctrlPressed = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
                
                if (shiftPressed && !ctrlPressed)
                {
                    // Extract the wheel delta correctly from the MSLLHOOKSTRUCT
                    // mouseData is a DWORD, wheel delta is in the high word
                    DWORD mouseData = pMouseStruct->mouseData;
                    
                    Logger::trace(L"MouseScrollRemap: Intercepted Shift+MouseWheel, converting to Shift+Ctrl+MouseWheel");
                    
                    // We need to inject Ctrl key press, mouse wheel, then Ctrl key release
                    // The Shift key is already pressed by the user, so the final result will be Shift+Ctrl+Wheel
                    
                    INPUT inputs[3] = {};
                    
                    // Ctrl down
                    inputs[0].type = INPUT_KEYBOARD;
                    inputs[0].ki.wVk = VK_CONTROL;
                    inputs[0].ki.dwFlags = 0;
                    
                    // Mouse wheel event - preserve the original mouseData format
                    inputs[1].type = INPUT_MOUSE;
                    inputs[1].mi.dx = 0;
                    inputs[1].mi.dy = 0;
                    inputs[1].mi.mouseData = mouseData;
                    inputs[1].mi.dwFlags = MOUSEEVENTF_WHEEL;
                    inputs[1].mi.time = 0;
                    inputs[1].mi.dwExtraInfo = 0;
                    
                    // Ctrl up
                    inputs[2].type = INPUT_KEYBOARD;
                    inputs[2].ki.wVk = VK_CONTROL;
                    inputs[2].ki.dwFlags = KEYEVENTF_KEYUP;
                    
                    // Send the input and check for success
                    UINT eventsSent = SendInput(3, inputs, sizeof(INPUT));
                    if (eventsSent != 3)
                    {
                        // If SendInput failed, log the error and don't block the original event
                        Logger::error(L"MouseScrollRemap: SendInput failed, sent {} of 3 events", eventsSent);
                        // Don't block the event if injection failed
                        return CallNextHookEx(nullptr, nCode, wParam, lParam);
                    }
                    
                    // Block the original Shift+MouseWheel event only if injection succeeded
                    return 1;
                }
            }
        }
        
        return CallNextHookEx(nullptr, nCode, wParam, lParam);
    }
};

// This essentially defines the PowerToy object which implements the PowerToyModuleIface.
extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new MouseScrollRemap();
}
