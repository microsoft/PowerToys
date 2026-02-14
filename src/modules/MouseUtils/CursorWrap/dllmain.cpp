
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "../../../common/SettingsAPI/settings_objects.h"
#include "trace.h"
#include "../../../common/utils/process_path.h"
#include "../../../common/utils/resources.h"
#include "../../../common/logger/logger.h"
#include "../../../common/utils/logger_helper.h"
#include "../../../common/interop/shared_constants.h"
#include <atomic>
#include <thread>
#include <vector>
#include <map>
#include <string>
#include <algorithm>
#include <windows.h>
#include <dbt.h>
#include <sstream>
#include "resource.h"
#include "CursorWrapCore.h"

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
    const wchar_t JSON_KEY_DISABLE_WRAP_DURING_DRAG[] = L"disable_wrap_during_drag";
    const wchar_t JSON_KEY_WRAP_MODE[] = L"wrap_mode";
    const wchar_t JSON_KEY_DISABLE_ON_SINGLE_MONITOR[] = L"disable_cursor_wrap_on_single_monitor";
}

// The PowerToy name that will be shown in the settings.
const static wchar_t* MODULE_NAME = L"CursorWrap";
// Add a description that will we shown in the module settings page.
const static wchar_t* MODULE_DESC = L"<no description>";

// Monitor device interface GUID for RegisterDeviceNotification
// {e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}
static const GUID GUID_DEVINTERFACE_MONITOR =
    { 0xe6f07b5f, 0xee97, 0x4a90, { 0xb0, 0x76, 0x33, 0xf5, 0x7b, 0xf4, 0xea, 0xa7 } };

// Forward declaration
class CursorWrap;

// Global instance pointer for the mouse hook
static CursorWrap* g_cursorWrapInstance = nullptr;

// Implement the PowerToy Module Interface and all the required methods.
class CursorWrap
{
private:
    // The PowerToy state.
    bool m_enabled = false;
    bool m_autoActivate = false;
    bool m_disableWrapDuringDrag = true; // Default to true to prevent wrap during drag
    bool m_disableOnSingleMonitor = false; // Default to false
    int m_wrapMode = 0; // 0=Both (default), 1=VerticalOnly, 2=HorizontalOnly
    
    // Mouse hook
    HHOOK m_mouseHook = nullptr;
    std::atomic<bool> m_hookActive{ false };
    
    // Core wrapping engine (edge-based polygon model)
    CursorWrapCore m_core;
    
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

    // Enable the powertoy
    virtual void enable()
    {
        m_enabled = true;
        Trace::EnableCursorWrap(true);

        // Start listening for external trigger event so we can invoke the same logic as the activation hotkey.
        m_triggerEventHandle = CreateEventW(nullptr, false, false, CommonSharedConstants::CURSOR_WRAP_TRIGGER_EVENT);
        m_terminateEventHandle = CreateEventW(nullptr, false, false, nullptr);
        if (m_triggerEventHandle)
        {
            ResetEvent(m_triggerEventHandle);
        }
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

                // Only start the mouse hook automatically if auto-activate is enabled
                if (m_autoActivate)
                {
                    StartMouseHook();
                    Logger::info("CursorWrap enabled - mouse hook started (auto-activate on)");
                }
                else
                {
                    Logger::info("CursorWrap enabled - waiting for activation hotkey (auto-activate off)");
                }

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
                PowerToysSettings::PowerToyValues::load_from_settings_file(L"CursorWrap");
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
            
            try
            {
                // Parse disable on single monitor
                auto propertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES);
                if (propertiesObject.HasKey(JSON_KEY_DISABLE_ON_SINGLE_MONITOR))
                {
                    auto disableOnSingleMonitorObject = propertiesObject.GetNamedObject(JSON_KEY_DISABLE_ON_SINGLE_MONITOR);
                    m_disableOnSingleMonitor = disableOnSingleMonitorObject.GetNamedBoolean(JSON_KEY_VALUE);
                }
            }
            catch (...)
            {
                Logger::warn("Failed to initialize CursorWrap disable on single monitor from settings. Will use default value (false)");
            }
        }
        else
        {
            Logger::info("CursorWrap settings are empty");
        }
    }

public:
    void StartMouseHook()
    {
        if (m_mouseHook || m_hookActive)
        {
            Logger::info("CursorWrap mouse hook already active");
            return;
        }

        // Refresh monitor info before starting hook
        m_core.UpdateMonitorInfo();
        
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

    void RegisterForDisplayChanges()
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
        else
        {
            DWORD error = GetLastError();
            Logger::error(L"Failed to create message window for display changes, error: {}", error);
        }
    }

    void UnregisterDisplayChanges()
    {
        if (m_deviceNotify)
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

