// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "../../../common/logger/logger.h"
#include <atomic>
#include <thread>
#include <vector>
#include <map>
#include <string>
#include <algorithm>
#include <windows.h>

// Disable C26451 arithmetic overflow warning for this file since the operations are safe in this context
#pragma warning(disable: 26451)

extern "C" IMAGE_DOS_HEADER __ImageBase;

// Mouse hook data structure
struct MonitorInfo
{
    RECT rect;
    bool isPrimary;
    int monitorId; // Add monitor ID for easier debugging
};

// Forward declaration
class CursorWrap;

// Global instance pointer
static CursorWrap* g_cursorWrapInstance = nullptr;

// Implement the PowerToy Module Interface and all the required methods.
class CursorWrap
{
private:
    // The PowerToy state.
    bool m_enabled = false;
    bool m_autoActivate = false;
    bool m_disableWrapDuringDrag = true; // Default to true to prevent wrap during drag
    int m_wrapMode = 0; // 0=Both (default), 1=VerticalOnly, 2=HorizontalOnly
    
    // Mouse hook
    HHOOK m_mouseHook = nullptr;
    std::atomic<bool> m_hookActive{ false };
    
    // Core wrapping engine (edge-based polygon model)
    CursorWrapCore m_core;
    
    // Hotkey
    Hotkey m_activationHotkey{};

    // Event-driven trigger support (for CmdPal/automation)
    HANDLE m_triggerEventHandle = nullptr;
    HANDLE m_terminateEventHandle = nullptr;
    std::thread m_eventThread;
    std::atomic_bool m_listening{ false };

    // Display change notification
    HWND m_messageWindow = nullptr;
    HDEVNOTIFY m_deviceNotify = nullptr;
    static constexpr UINT_PTR TIMER_UPDATE_MONITORS = 1;
    static constexpr UINT DEBOUNCE_DELAY_MS = 500;

public:
    // Constructor
    CursorWrap()
    {
        LoggerHelpers::init_logger(MODULE_NAME, L"ModuleInterface", LogSettings::cursorWrapLoggerName);
        init_settings();
        m_core.UpdateMonitorInfo();
        g_cursorWrapInstance = this; // Set global instance pointer
    };

    // Destroy the powertoy and free memory
    virtual void destroy() override
    {
        // Ensure hooks/threads/handles are torn down before deletion
        disable();
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
        settings.add_bool_toggle(JSON_KEY_DISABLE_WRAP_DURING_DRAG, IDS_CURSORWRAP_NAME, m_disableWrapDuringDrag);

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

        // Start listening for external trigger event so we can invoke the same logic as the activation hotkey.
        m_triggerEventHandle = CreateEventW(nullptr, false, false, CommonSharedConstants::CURSOR_WRAP_TRIGGER_EVENT);
        m_terminateEventHandle = CreateEventW(nullptr, false, false, nullptr);
        if (m_triggerEventHandle && m_terminateEventHandle)
        {
            m_listening = true;
            m_eventThread = std::thread([this]() {
                HANDLE handles[2] = { m_triggerEventHandle, m_terminateEventHandle };

                // WH_MOUSE_LL callbacks are delivered to the thread that installed the hook.
                // Ensure this thread has a message queue and pumps messages while the hook is active.
                MSG msg;
                PeekMessage(&msg, nullptr, WM_USER, WM_USER, PM_NOREMOVE);

                // Create message window for display change notifications
                RegisterForDisplayChanges();

                StartMouseHook();
                Logger::info("CursorWrap enabled - mouse hook started");

                while (m_listening)
                {
                    auto res = MsgWaitForMultipleObjects(2, handles, false, INFINITE, QS_ALLINPUT);
                    if (!m_listening)
                    {
                        break;
                    }

                    if (res == WAIT_OBJECT_0)
                    {
                        ToggleMouseHook();
                    }
                    else if (res == WAIT_OBJECT_0 + 1)
                    {
                        break;
                    }
                    else
                    {
                        while (PeekMessage(&msg, nullptr, 0, 0, PM_REMOVE))
                        {
                            TranslateMessage(&msg);
                            DispatchMessage(&msg);
                        }
                    }
                }

                // Cleanup display change notifications
                UnregisterDisplayChanges();

                StopMouseHook();
                Logger::info("CursorWrap event listener stopped");
            });
        }
    }

    // Disable the powertoy
    virtual void disable()
    {
        m_enabled = false;
        Trace::EnableCursorWrap(false);

        m_listening = false;
        if (m_terminateEventHandle)
        {
            SetEvent(m_terminateEventHandle);
        }
        if (m_eventThread.joinable())
        {
            m_eventThread.join();
        }
        if (m_triggerEventHandle)
        {
            CloseHandle(m_triggerEventHandle);
            m_triggerEventHandle = nullptr;
        }
        if (m_terminateEventHandle)
        {
            CloseHandle(m_terminateEventHandle);
            m_terminateEventHandle = nullptr;
        }
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

        // Toggle on the thread that owns the WH_MOUSE_LL hook (the event listener thread).
        if (m_triggerEventHandle)
        {
            return SetEvent(m_triggerEventHandle);
        }

        return false;
    }

    // Called when display configuration changes - update monitor topology
    void OnDisplayChange()
    {
#ifdef _DEBUG
        OutputDebugStringW(L"[CursorWrap] Display configuration changed, updating monitor topology\n");
#endif
        Logger::info("Display configuration changed, updating monitor topology");
        m_core.UpdateMonitorInfo();
    }

private:
    void ToggleMouseHook()
    {
        // Toggle cursor wrapping.
        if (m_hookActive)
        {
            StopMouseHook();
        }
        else
        {
            StartMouseHook();
        }
    }

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
            
            try
            {
                // Parse disable wrap during drag
                auto propertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES);
                if (propertiesObject.HasKey(JSON_KEY_DISABLE_WRAP_DURING_DRAG))
                {
                    auto disableDragObject = propertiesObject.GetNamedObject(JSON_KEY_DISABLE_WRAP_DURING_DRAG);
                    m_disableWrapDuringDrag = disableDragObject.GetNamedBoolean(JSON_KEY_VALUE);
                }
            }
            catch (...)
            {
                Logger::warn("Failed to initialize CursorWrap disable wrap during drag from settings. Will use default value (true)");
            }
            
            try
            {
                // Parse wrap mode
                auto propertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES);
                if (propertiesObject.HasKey(JSON_KEY_WRAP_MODE))
                {
                    auto wrapModeObject = propertiesObject.GetNamedObject(JSON_KEY_WRAP_MODE);
                    m_wrapMode = static_cast<int>(wrapModeObject.GetNamedNumber(JSON_KEY_VALUE));
                }
            }
            catch (...)
            {
                Logger::warn("Failed to initialize CursorWrap wrap mode from settings. Will use default value (0=Both)");
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
            Logger::info(L"CursorWrap DEBUG: Hook installed");
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
            Logger::info("CursorWrap DEBUG: Mouse hook stopped");
#endif
        }
    }

private:
    // The PowerToy state.
    bool m_disableWrapDuringDrag = true; // Default to true to prevent wrap during drag
    
    // Mouse hook
    HHOOK m_mouseHook = nullptr;
    std::atomic<bool> m_hookActive{ false };
    
    // Monitor information
    std::vector<MonitorInfo> m_monitors;
    MonitorTopology m_topology;
    
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
                info.monitorId = static_cast<int>(self->m_monitors.size());
                self->m_monitors.push_back(info);
            }
            
            return TRUE;
        }, reinterpret_cast<LPARAM>(this));
        
        // Initialize monitor topology
        m_topology.Initialize(m_monitors);
    }

    static LRESULT CALLBACK MouseHookProc(int nCode, WPARAM wParam, LPARAM lParam)
    {
        if (m_messageWindow)
        {
            return; // Already registered
        }

        // Create a hidden top-level window to receive broadcast messages
        // NOTE: Message-only windows (HWND_MESSAGE parent) do NOT receive
        // WM_DISPLAYCHANGE, WM_SETTINGCHANGE, or WM_DEVICECHANGE broadcasts.
        // We must use a real (hidden) top-level window instead.
        WNDCLASSEXW wc = { sizeof(WNDCLASSEXW) };
        wc.lpfnWndProc = MessageWindowProc;
        wc.hInstance = GetModuleHandle(nullptr);
        wc.lpszClassName = L"CursorWrapDisplayChangeWindow";

        RegisterClassExW(&wc);

        // Create a hidden top-level window (not message-only)
        // WS_EX_TOOLWINDOW prevents taskbar button, WS_POPUP with no size makes it invisible
        m_messageWindow = CreateWindowExW(
            WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE,
            L"CursorWrapDisplayChangeWindow",
            nullptr,
            WS_POPUP,  // Minimal window style
            0, 0, 0, 0,  // Zero size = invisible
            nullptr,  // No parent - top-level window to receive broadcasts
            nullptr,
            GetModuleHandle(nullptr),
            nullptr);

        if (m_messageWindow)
        {
#ifdef _DEBUG
            OutputDebugStringW(L"[CursorWrap] Registered for display change notifications\n");
#endif
            Logger::info("Registered for display change notifications");

            // Register for device notifications (monitor hardware add/remove)
            DEV_BROADCAST_DEVICEINTERFACE filter = {};
            filter.dbcc_size = sizeof(DEV_BROADCAST_DEVICEINTERFACE);
            filter.dbcc_devicetype = DBT_DEVTYP_DEVICEINTERFACE;
            filter.dbcc_classguid = GUID_DEVINTERFACE_MONITOR;

            m_deviceNotify = RegisterDeviceNotificationW(
                m_messageWindow,
                &filter,
                DEVICE_NOTIFY_WINDOW_HANDLE);

            if (m_deviceNotify)
            {
#ifdef _DEBUG
                OutputDebugStringW(L"[CursorWrap] Registered for device notifications (monitor hardware changes)\n");
#endif
                Logger::info("Registered for device notifications (monitor hardware changes)");
            }
            else
            {
                DWORD error = GetLastError();
#ifdef _DEBUG
                std::wostringstream oss;
                oss << L"[CursorWrap] Failed to register device notifications. Error: " << error << L"\n";
                OutputDebugStringW(oss.str().c_str());
#endif
                Logger::warn("Failed to register device notifications. Error: {}", error);
            }
        }
        else if (currentPos.y >= currentMonitorInfo.rcMonitor.bottom - 1)
        {
#ifdef _DEBUG
            Logger::info(L"CursorWrap DEBUG: ======= VERTICAL WRAP: BOTTOM EDGE DETECTED =======");
#endif
            
            // Check if there's an adjacent monitor below in coordinate space
            if (HasAdjacentMonitorInCoordinateSpace(currentMonitorInfo.rcMonitor, 3))
            {
#ifdef _DEBUG
                Logger::info(L"CursorWrap DEBUG: SKIPPING WRAP - Adjacent monitor exists below (Windows will handle)");
#endif
                return currentPos; // Let Windows handle natural cursor movement
            }
            
            // Find the top-most monitor in the vertical stack (same column)
            HMONITOR topMonitor = nullptr;
            
            if (currentLogicalPos.isValid) {
                // Search up from current position to find the top-most monitor in same column
                for (int row = 0; row <= 2; row++) { // Start from top and work down
                    HMONITOR candidateMonitor = m_topology.GetMonitorAt(row, currentLogicalPos.col);
                    if (candidateMonitor) {
                        topMonitor = candidateMonitor;
                        break; // Found the top-most monitor
                    }
                }
            }
            

            if (topMonitor && topMonitor != currentMonitor) {
                // *** MOVE TO TOP OF VERTICAL STACK ***
                MONITORINFO topInfo{};
                topInfo.cbSize = sizeof(MONITORINFO);
                GetMonitorInfo(topMonitor, &topInfo);
                
                // Calculate relative X position to maintain cursor X alignment
                double relativeX = static_cast<double>(currentPos.x - currentMonitorInfo.rcMonitor.left) / 
                                  (currentMonitorInfo.rcMonitor.right - currentMonitorInfo.rcMonitor.left);
                
                int targetWidth = topInfo.rcMonitor.right - topInfo.rcMonitor.left;
                newPos.x = topInfo.rcMonitor.left + static_cast<int>(relativeX * targetWidth);
                newPos.y = topInfo.rcMonitor.top; // Top edge of top monitor
                
                // Clamp X to target monitor bounds
                newPos.x = max(topInfo.rcMonitor.left, min(newPos.x, topInfo.rcMonitor.right - 1));
                wrapped = true;
                
#ifdef _DEBUG
                Logger::info(L"CursorWrap DEBUG: VERTICAL WRAP SUCCESS - Moved to top of vertical stack");
                Logger::info(L"CursorWrap DEBUG: New position: ({}, {})", newPos.x, newPos.y);
#endif
            } else {
                // *** NO OTHER MONITOR IN VERTICAL STACK - WRAP WITHIN CURRENT MONITOR ***
                newPos.y = currentMonitorInfo.rcMonitor.top;
                wrapped = true;
                
#ifdef _DEBUG
                Logger::info(L"CursorWrap DEBUG: VERTICAL WRAP - No other monitor in stack, wrapping within current monitor");
#endif
            }
        }
        
        // *** FIXED HORIZONTAL WRAPPING LOGIC ***
        // Move to opposite end of horizontal stack when hitting left/right edge
        // Only wrap if there's NO adjacent monitor in the coordinate space (let Windows handle natural transitions)
        if (!wrapped && currentPos.x <= currentMonitorInfo.rcMonitor.left)
        {
#ifdef _DEBUG
            Logger::info(L"CursorWrap DEBUG: ======= HORIZONTAL WRAP: LEFT EDGE DETECTED =======");
#endif
            
            // Check if there's an adjacent monitor to the left in coordinate space
            if (HasAdjacentMonitorInCoordinateSpace(currentMonitorInfo.rcMonitor, 0))
            {
#ifdef _DEBUG
            OutputDebugStringW(L"[CursorWrap] Unregistering device notifications...\n");
#endif
            UnregisterDeviceNotification(m_deviceNotify);
            m_deviceNotify = nullptr;
            Logger::info("Unregistered device notifications");
        }

        if (m_messageWindow)
        {
            KillTimer(m_messageWindow, TIMER_UPDATE_MONITORS);
            DestroyWindow(m_messageWindow);
            m_messageWindow = nullptr;
            UnregisterClassW(L"CursorWrapDisplayChangeWindow", GetModuleHandle(nullptr));
#ifdef _DEBUG
            OutputDebugStringW(L"[CursorWrap] Unregistered display change notifications\n");
#endif
            Logger::info("Unregistered display change notifications");
        }
    }

    static LRESULT CALLBACK MessageWindowProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
    {
        if (!g_cursorWrapInstance)
        {
            return DefWindowProcW(hwnd, msg, wParam, lParam);
        }

        switch (msg)
        {
        case WM_DISPLAYCHANGE:
#ifdef _DEBUG
            OutputDebugStringW(L"[CursorWrap] WM_DISPLAYCHANGE received - monitor resolution/DPI changed\n");
#endif
            Logger::info("WM_DISPLAYCHANGE received - resolution/DPI changed");
            // Debounce: Wait for multiple changes to settle
            KillTimer(hwnd, TIMER_UPDATE_MONITORS);
            SetTimer(hwnd, TIMER_UPDATE_MONITORS, DEBOUNCE_DELAY_MS, nullptr);
            break;

        case WM_SETTINGCHANGE:
            if (wParam == SPI_SETWORKAREA)
            {
#ifdef _DEBUG
                OutputDebugStringW(L"[CursorWrap] WM_SETTINGCHANGE (SPI_SETWORKAREA) received - taskbar changed\n");
#endif
                Logger::info("WM_SETTINGCHANGE (SPI_SETWORKAREA) received");
                // Taskbar position/size changed
                KillTimer(hwnd, TIMER_UPDATE_MONITORS);
                SetTimer(hwnd, TIMER_UPDATE_MONITORS, DEBOUNCE_DELAY_MS, nullptr);
            }
            break;

        case WM_DEVICECHANGE:
            // Handle monitor hardware add/remove
            if (wParam == DBT_DEVNODES_CHANGED)
            {
#ifdef _DEBUG
                OutputDebugStringW(L"[CursorWrap] DBT_DEVNODES_CHANGED received - monitor hardware change detected\n");
#endif
                Logger::info("DBT_DEVNODES_CHANGED received - monitor hardware change detected");
                // Debounce: Wait for multiple changes to settle
                KillTimer(hwnd, TIMER_UPDATE_MONITORS);
                SetTimer(hwnd, TIMER_UPDATE_MONITORS, DEBOUNCE_DELAY_MS, nullptr);
                return TRUE;
            }
            break;

        case WM_TIMER:
            if (wParam == TIMER_UPDATE_MONITORS)
            {
#ifdef _DEBUG
                OutputDebugStringW(L"[CursorWrap] Debounce timer expired - triggering topology update\n");
#endif
                KillTimer(hwnd, TIMER_UPDATE_MONITORS);
                g_cursorWrapInstance->OnDisplayChange();
            }
            break;
        }

        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    static LRESULT CALLBACK MouseHookProc(int nCode, WPARAM wParam, LPARAM lParam)
    {
        if (nCode >= 0 && wParam == WM_MOUSEMOVE)
        {
            auto* pMouseStruct = reinterpret_cast<MSLLHOOKSTRUCT*>(lParam);
            POINT currentPos = { pMouseStruct->pt.x, pMouseStruct->pt.y };
            
            if (g_cursorWrapInstance && g_cursorWrapInstance->m_hookActive)
            {
                POINT newPos = g_cursorWrapInstance->m_core.HandleMouseMove(
                    currentPos,
                    g_cursorWrapInstance->m_disableWrapDuringDrag,
                    g_cursorWrapInstance->m_wrapMode,
                    g_cursorWrapInstance->m_disableOnSingleMonitor);
                    
                if (newPos.x != currentPos.x || newPos.y != currentPos.y)
                {
#ifdef _DEBUG
                    Logger::info(L"CursorWrap DEBUG: Wrapping cursor from ({}, {}) to ({}, {})", 
                                currentPos.x, currentPos.y, newPos.x, newPos.y);
#endif
                    SetCursorPos(newPos.x, newPos.y);
                    return 1; // Suppress the original message
                }
            }
        }
        
        return CallNextHookEx(nullptr, nCode, wParam, lParam);
    }
};

EXTERN_C __declspec(dllexport) void CursorWrapStartMouseHook()
{
    if (!g_cursorWrapInstance)
    {
        g_cursorWrapInstance = new CursorWrap();
    }
    g_cursorWrapInstance->StartMouseHook();
}

EXTERN_C __declspec(dllexport) void CursorWrapStopMouseHook()
{
    if (g_cursorWrapInstance)
    {
        g_cursorWrapInstance->StopMouseHook();
    }
}

