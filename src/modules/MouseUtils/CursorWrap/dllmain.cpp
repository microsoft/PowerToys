#include "pch.h"
#include "../../../interface/powertoy_module_interface.h"
#include "../../../common/SettingsAPI/settings_objects.h"
#include "trace.h"
#include "../../../common/utils/process_path.h"
#include "../../../common/utils/resources.h"
#include "../../../common/logger/logger.h"
#include "../../../common/utils/logger_helper.h"
#include <atomic>
#include <thread>
#include <vector>
#include "resource.h"

// Disable C26451 arithmetic overflow warning for this file since the operations are safe in this context
#pragma warning(disable: 26451)

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

// Non-Localizable strings
namespace
{
    const wchar_t JSON_KEY_PROPERTIES[] = L"properties";
    const wchar_t JSON_KEY_VALUE[] = L"value";
    const wchar_t JSON_KEY_ACTIVATION_SHORTCUT[] = L"activation_shortcut";
    const wchar_t JSON_KEY_AUTO_ACTIVATE[] = L"auto_activate";
}

// The PowerToy name that will be shown in the settings.
const static wchar_t* MODULE_NAME = L"CursorWrap";
// Add a description that will we shown in the module settings page.
const static wchar_t* MODULE_DESC = L"<no description>";

// Mouse hook data structure
struct MonitorInfo
{
    RECT rect;
    bool isPrimary;
};

// Forward declaration
class CursorWrap;

// Global instance pointer for the mouse hook
static CursorWrap* g_cursorWrapInstance = nullptr;

// Implement the PowerToy Module Interface and all the required methods.
class CursorWrap : public PowertoyModuleIface
{
private:
    // The PowerToy state.
    bool m_enabled = false;
    bool m_autoActivate = false;
    
    // Mouse hook
    HHOOK m_mouseHook = nullptr;
    std::atomic<bool> m_hookActive{ false };
    
    // Monitor information
    std::vector<MonitorInfo> m_monitors;
    
    // Hotkey
    Hotkey m_activationHotkey{};

public:
    // Constructor
    CursorWrap()
    {
        LoggerHelpers::init_logger(MODULE_NAME, L"ModuleInterface", LogSettings::cursorWrapLoggerName);
        init_settings();
        UpdateMonitorInfo();
        g_cursorWrapInstance = this; // Set global instance pointer
    };

    // Destroy the powertoy and free memory
    virtual void destroy() override
    {
        StopMouseHook();
        g_cursorWrapInstance = nullptr; // Clear global instance pointer
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
        return powertoys_gpo::getConfiguredCursorWrapEnabledValue();
    }

    // Return JSON with the configuration options.
    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        PowerToysSettings::Settings settings(hinstance, get_name());

        settings.set_description(IDS_CURSORWRAP_NAME);
        settings.set_icon_key(L"pt-cursor-wrap");
        
// Create HotkeyObject from the Hotkey struct for the settings
        auto hotkey_object = PowerToysSettings::HotkeyObject::from_settings(
            m_activationHotkey.win,
            m_activationHotkey.ctrl,
            m_activationHotkey.alt,
            m_activationHotkey.shift,
            m_activationHotkey.key);

        settings.add_hotkey(JSON_KEY_ACTIVATION_SHORTCUT, IDS_CURSORWRAP_NAME, hotkey_object);
        settings.add_bool_toggle(JSON_KEY_AUTO_ACTIVATE, IDS_CURSORWRAP_NAME, m_autoActivate);

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    // Signal from the Settings editor to call a custom action.
    // This can be used to spawn more complex editors.
    virtual void call_custom_action(const wchar_t* /*action*/) override {}

    // Called by the runner to pass the updated settings values as a serialized JSON.
    virtual void set_config(const wchar_t* config) override
    {
        try
        {
            // Parse the input JSON string.
            PowerToysSettings::PowerToyValues values =
                PowerToysSettings::PowerToyValues::from_json_string(config, get_key());

            parse_settings(values);
        }
        catch (std::exception&)
        {
            Logger::error("Invalid json when trying to parse CursorWrap settings json.");
        }
    }

    // Enable the powertoy
    virtual void enable()
    {
        m_enabled = true;
        Trace::EnableCursorWrap(true);
        
        if (m_autoActivate)
        {
            StartMouseHook();
        }
    }

    // Disable the powertoy
    virtual void disable()
    {
        m_enabled = false;
        Trace::EnableCursorWrap(false);
        StopMouseHook();
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

    // Legacy hotkey support
    virtual size_t get_hotkeys(Hotkey* buffer, size_t buffer_size) override
    {
        if (buffer && buffer_size >= 1)
        {
            buffer[0] = m_activationHotkey;
        }
        return 1;
    }

    virtual bool on_hotkey(size_t hotkeyId) override
    {
        if (!m_enabled || hotkeyId != 0)
        {
            return false;
        }

        // Toggle cursor wrapping
        if (m_hookActive)
        {
            StopMouseHook();
        }
        else
        {
            StartMouseHook();
        }
        
        return true;
    }

private:
    // Load the settings file.
    void init_settings()
    {
        try
        {
            // Load and parse the settings file for this PowerToy.
            PowerToysSettings::PowerToyValues settings =
                PowerToysSettings::PowerToyValues::load_from_settings_file(CursorWrap::get_key());
            parse_settings(settings);
        }
        catch (std::exception&)
        {
            Logger::error("Invalid json when trying to load the CursorWrap settings json from file.");
        }
    }

    void parse_settings(PowerToysSettings::PowerToyValues& settings)
    {
        auto settingsObject = settings.get_raw_json();
        if (settingsObject.GetView().Size())
        {
            try
            {
                // Parse activation HotKey
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_ACTIVATION_SHORTCUT);
                auto hotkey = PowerToysSettings::HotkeyObject::from_json(jsonPropertiesObject);

                m_activationHotkey.win = hotkey.win_pressed();
                m_activationHotkey.ctrl = hotkey.ctrl_pressed();
                m_activationHotkey.shift = hotkey.shift_pressed();
                m_activationHotkey.alt = hotkey.alt_pressed();
                m_activationHotkey.key = static_cast<unsigned char>(hotkey.get_code());
            }
            catch (...)
            {
                Logger::warn("Failed to initialize CursorWrap activation shortcut");
            }
            
            try
            {
                // Parse auto activate
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_AUTO_ACTIVATE);
                m_autoActivate = jsonPropertiesObject.GetNamedBoolean(JSON_KEY_VALUE);
            }
            catch (...)
            {
                Logger::warn("Failed to initialize CursorWrap auto activate from settings. Will use default value");
            }
        }
        else
        {
            Logger::info("CursorWrap settings are empty");
        }
        
        // Set default hotkey if not configured
        if (m_activationHotkey.key == 0)
        {
            m_activationHotkey.win = true;
            m_activationHotkey.alt = true;
            m_activationHotkey.ctrl = false;
            m_activationHotkey.shift = false;
            m_activationHotkey.key = 'U'; // Win+Alt+U
        }
    }

    void UpdateMonitorInfo()
    {
        m_monitors.clear();
        
        EnumDisplayMonitors(nullptr, nullptr, [](HMONITOR hMonitor, HDC, LPRECT, LPARAM lParam) -> BOOL {
            auto* self = reinterpret_cast<CursorWrap*>(lParam);
            
            MONITORINFO mi{};
            mi.cbSize = sizeof(MONITORINFO);
            if (GetMonitorInfo(hMonitor, &mi))
            {
                MonitorInfo info{};
                info.rect = mi.rcMonitor;
                info.isPrimary = (mi.dwFlags & MONITORINFOF_PRIMARY) != 0;
                self->m_monitors.push_back(info);
            }
            
            return TRUE;
        }, reinterpret_cast<LPARAM>(this));
    }

    void StartMouseHook()
    {
        if (m_mouseHook || m_hookActive)
        {
            Logger::info("CursorWrap mouse hook already active");
            return;
        }

        UpdateMonitorInfo();
        
        m_mouseHook = SetWindowsHookEx(WH_MOUSE_LL, MouseHookProc, GetModuleHandle(nullptr), 0);
        if (m_mouseHook)
        {
            m_hookActive = true;
            Logger::info("CursorWrap mouse hook started successfully");
#ifdef _DEBUG
            Logger::info(L"CursorWrap DEBUG: Hook installed, instance pointer set");
#endif
        }
        else
        {
            DWORD error = GetLastError();
            Logger::error(L"Failed to install CursorWrap mouse hook, error: {}", error);
        }
    }

    void StopMouseHook()
    {
        if (m_mouseHook)
        {
            UnhookWindowsHookEx(m_mouseHook);
            m_mouseHook = nullptr;
            m_hookActive = false;
            Logger::info("CursorWrap mouse hook stopped");
#ifdef _DEBUG
            Logger::info("CursorWrap DEBUG: Mouse hook stopped and cleaned up");
#endif
        }
    }

    static LRESULT CALLBACK MouseHookProc(int nCode, WPARAM wParam, LPARAM lParam)
    {
#ifdef _DEBUG
        static int hookCallCount = 0;
        hookCallCount++;
        
        // Log every 1000th hook call to avoid spam
        if (hookCallCount % 1000 == 0)
        {
            Logger::info(L"CursorWrap DEBUG: Hook proc called {} times", hookCallCount);
        }
#endif

        if (nCode >= 0 && wParam == WM_MOUSEMOVE)
        {
            auto* pMouseStruct = reinterpret_cast<MSLLHOOKSTRUCT*>(lParam);
            POINT currentPos = { pMouseStruct->pt.x, pMouseStruct->pt.y };
            
#ifdef _DEBUG
            Logger::info(L"CursorWrap DEBUG: Mouse move detected at ({}, {})", currentPos.x, currentPos.y);
#endif
            
            if (g_cursorWrapInstance && g_cursorWrapInstance->m_hookActive)
            {
#ifdef _DEBUG
                Logger::info("CursorWrap DEBUG: Instance found and hook active, calling HandleMouseMove");
#endif
                POINT newPos = g_cursorWrapInstance->HandleMouseMove(currentPos);
                if (newPos.x != currentPos.x || newPos.y != currentPos.y)
                {
#ifdef _DEBUG
                    Logger::info(L"CursorWrap DEBUG: Wrapping cursor from ({}, {}) to ({}, {})", 
                                currentPos.x, currentPos.y, newPos.x, newPos.y);
#endif
                    SetCursorPos(newPos.x, newPos.y);
                    return 1; // Suppress the original message
                }
#ifdef _DEBUG
                else
                {
                    // Log occasionally to verify HandleMouseMove is being called
                    static int noWrapCount = 0;
                    noWrapCount++;
                    if (noWrapCount % 500 == 0)
                    {
                        Logger::info(L"CursorWrap DEBUG: No wrapping needed (checked {} times)", noWrapCount);
                    }
                }
#endif
            }
#ifdef _DEBUG
            else
            {
                static int instanceMissingCount = 0;
                instanceMissingCount++;
                if (instanceMissingCount % 100 == 0)
                {
                    Logger::warn(L"CursorWrap DEBUG: Instance missing or hook inactive (count: {})", instanceMissingCount);
                    Logger::warn(L"CursorWrap DEBUG: Instance pointer: {}, Hook active: {}", 
                                (void*)g_cursorWrapInstance, 
                                g_cursorWrapInstance ? (bool)g_cursorWrapInstance->m_hookActive : false);
                }
            }
#endif
        }
        
        return CallNextHookEx(nullptr, nCode, wParam, lParam);
    }
    
    POINT HandleMouseMove(const POINT& currentPos)
    {
        POINT newPos = currentPos;
        
#ifdef _DEBUG
        Logger::info(L"CursorWrap DEBUG: HandleMouseMove called with position ({}, {})", currentPos.x, currentPos.y);
#endif
        
        // Get virtual screen dimensions
        int virtualScreenLeft = GetSystemMetrics(SM_XVIRTUALSCREEN);
        int virtualScreenTop = GetSystemMetrics(SM_YVIRTUALSCREEN);
        int virtualScreenWidth = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        int virtualScreenHeight = GetSystemMetrics(SM_CYVIRTUALSCREEN);
        
        // Cast to wider type before arithmetic to avoid overflow warnings
        int virtualScreenRight = static_cast<int>(static_cast<int64_t>(virtualScreenLeft) + static_cast<int64_t>(virtualScreenWidth) - 1LL);
        int virtualScreenBottom = static_cast<int>(static_cast<int64_t>(virtualScreenTop) + static_cast<int64_t>(virtualScreenHeight) - 1LL);
        
#ifdef _DEBUG
        Logger::info(L"CursorWrap DEBUG: Virtual screen bounds: Left={}, Top={}, Right={}, Bottom={}", 
                    virtualScreenLeft, virtualScreenTop, virtualScreenRight, virtualScreenBottom);
        Logger::info(L"CursorWrap DEBUG: Virtual screen dimensions: {}x{}", virtualScreenWidth, virtualScreenHeight);
#endif
        
        // Find which monitor the cursor is currently on
        HMONITOR currentMonitor = MonitorFromPoint(currentPos, MONITOR_DEFAULTTONEAREST);
        MONITORINFO currentMonitorInfo{};
        currentMonitorInfo.cbSize = sizeof(MONITORINFO);
        GetMonitorInfo(currentMonitor, &currentMonitorInfo);
        
#ifdef _DEBUG
        Logger::info(L"CursorWrap DEBUG: Current monitor bounds: Left={}, Top={}, Right={}, Bottom={}", 
                    currentMonitorInfo.rcMonitor.left, currentMonitorInfo.rcMonitor.top, 
                    currentMonitorInfo.rcMonitor.right, currentMonitorInfo.rcMonitor.bottom);
        Logger::info(L"CursorWrap DEBUG: Monitor count: {}", m_monitors.size());
#endif
        
        // Handle vertical wrapping (per monitor)
        if (currentPos.y <= currentMonitorInfo.rcMonitor.top)
        {
#ifdef _DEBUG
            Logger::info(L"CursorWrap DEBUG: Cursor at top edge, wrapping to bottom");
#endif
            // Wrap to bottom of current monitor
            newPos.y = currentMonitorInfo.rcMonitor.bottom - 1;
        }
        else if (currentPos.y >= currentMonitorInfo.rcMonitor.bottom - 1)
        {
#ifdef _DEBUG
            Logger::info(L"CursorWrap DEBUG: Cursor at bottom edge, wrapping to top");
#endif
            // Wrap to top of current monitor
            newPos.y = currentMonitorInfo.rcMonitor.top;
        }
        
        // Handle horizontal wrapping (across all monitors)
        if (currentPos.x <= virtualScreenLeft)
        {
#ifdef _DEBUG
            Logger::info(L"CursorWrap DEBUG: Cursor at left edge, wrapping to right");
#endif
            // Wrap to right edge of virtual screen
            newPos.x = virtualScreenRight;
            
            // Adjust Y position if necessary (relative positioning)
            double relativeY = static_cast<double>(currentPos.y - currentMonitorInfo.rcMonitor.top) / 
                              (currentMonitorInfo.rcMonitor.bottom - currentMonitorInfo.rcMonitor.top);
            
#ifdef _DEBUG
            Logger::info(L"CursorWrap DEBUG: Relative Y position: {}", relativeY);
#endif
            
            // Find the rightmost monitor
            HMONITOR targetMonitor = MonitorFromPoint({virtualScreenRight, currentPos.y}, MONITOR_DEFAULTTONEAREST);
            MONITORINFO targetMonitorInfo{};
            targetMonitorInfo.cbSize = sizeof(MONITORINFO);
            GetMonitorInfo(targetMonitor, &targetMonitorInfo);
            
#ifdef _DEBUG
            Logger::info(L"CursorWrap DEBUG: Target monitor bounds: Left={}, Top={}, Right={}, Bottom={}", 
                        targetMonitorInfo.rcMonitor.left, targetMonitorInfo.rcMonitor.top, 
                        targetMonitorInfo.rcMonitor.right, targetMonitorInfo.rcMonitor.bottom);
#endif
            
            // Apply relative Y position to target monitor
            int targetHeight = targetMonitorInfo.rcMonitor.bottom - targetMonitorInfo.rcMonitor.top;
            newPos.y = targetMonitorInfo.rcMonitor.top + static_cast<int>(relativeY * targetHeight);
            
            // Clamp to target monitor bounds
            newPos.y = max(targetMonitorInfo.rcMonitor.top, min(newPos.y, static_cast<int>(static_cast<int64_t>(targetMonitorInfo.rcMonitor.bottom) - 1LL)));
        }
        else if (currentPos.x >= virtualScreenRight)
        {
#ifdef _DEBUG
            Logger::info(L"CursorWrap DEBUG: Cursor at right edge, wrapping to left");
#endif
            // Wrap to left edge of virtual screen
            newPos.x = virtualScreenLeft;
            
            // Adjust Y position if necessary (relative positioning)
            double relativeY = static_cast<double>(currentPos.y - currentMonitorInfo.rcMonitor.top) / 
                              (currentMonitorInfo.rcMonitor.bottom - currentMonitorInfo.rcMonitor.top);
            
#ifdef _DEBUG
            Logger::info(L"CursorWrap DEBUG: Relative Y position: {}", relativeY);
#endif
            
            // Find the leftmost monitor
            HMONITOR targetMonitor = MonitorFromPoint({virtualScreenLeft, currentPos.y}, MONITOR_DEFAULTTONEAREST);
            MONITORINFO targetMonitorInfo{};
            targetMonitorInfo.cbSize = sizeof(MONITORINFO);
            GetMonitorInfo(targetMonitor, &targetMonitorInfo);
            
#ifdef _DEBUG
            Logger::info(L"CursorWrap DEBUG: Target monitor bounds: Left={}, Top={}, Right={}, Bottom={}", 
                        targetMonitorInfo.rcMonitor.left, targetMonitorInfo.rcMonitor.top, 
                        targetMonitorInfo.rcMonitor.right, targetMonitorInfo.rcMonitor.bottom);
#endif
            
            // Apply relative Y position to target monitor
            int targetHeight = targetMonitorInfo.rcMonitor.bottom - targetMonitorInfo.rcMonitor.top;
            newPos.y = targetMonitorInfo.rcMonitor.top + static_cast<int>(relativeY * targetHeight);
            
            // Clamp to target monitor bounds
            newPos.y = max(targetMonitorInfo.rcMonitor.top, min(newPos.y, static_cast<int>(static_cast<int64_t>(targetMonitorInfo.rcMonitor.bottom) - 1LL)));
        }
        
#ifdef _DEBUG
        if (newPos.x != currentPos.x || newPos.y != currentPos.y)
        {
            Logger::info(L"CursorWrap DEBUG: Returning new position ({}, {}) from original ({}, {})", 
                        newPos.x, newPos.y, currentPos.x, currentPos.y);
        }
        else
        {
            static int noChangeCount = 0;
            noChangeCount++;
            if (noChangeCount % 1000 == 0)
            {
                Logger::info(L"CursorWrap DEBUG: No position change needed (count: {})", noChangeCount);
            }
        }
#endif
        
        return newPos;
    }
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new CursorWrap();
}
