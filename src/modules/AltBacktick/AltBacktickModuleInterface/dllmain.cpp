// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/logger/logger.h>
#include "trace.h"
#include "resource.h"
#include <shlobj.h>
#include <psapi.h>
#include <deque>
#include <vector>
#include <unordered_map>
#include <algorithm>
#include <thread>
#include <atomic>
#include <filesystem>
#include <common/SettingsAPI/settings_helpers.h>

extern "C" IMAGE_DOS_HEADER __ImageBase;

// Global module handle
static HMODULE g_hModule = nullptr;

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID /*lpReserved*/)
{
    g_hModule = hModule;
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
    const wchar_t JSON_KEY_MODIFIER_KEY[] = L"modifier_key";
    const wchar_t JSON_KEY_IGNORE_MINIMIZED_WINDOWS[] = L"ignore_minimized_windows";

    const UINT BACKTICK_SCAN_CODE = 0x29; // Scan code for backtick key (key above TAB)
}

// The PowerToy name that will be shown in the settings.
const static wchar_t* MODULE_NAME = L"AltBacktick";
const static wchar_t* MODULE_KEY = L"AltBacktick";
const static wchar_t* MODULE_DESC = L"Switch between windows of the same application using Alt+` (like macOS)";

// Modifier key enum (matches C# enum)
enum class ModifierKeyEnum
{
    Alt = 0,
    Win = 1,
};

// Window finding logic (ported from thirdparty/AltBacktick)
class WindowFinder
{
private:
    IVirtualDesktopManager* m_desktopManager = nullptr;

    static std::wstring GetProcessNameFromProcessId(DWORD processId)
    {
        wchar_t filename[MAX_PATH] = { 0 };
        HANDLE processHandle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, processId);
        if (processHandle != nullptr)
        {
            GetModuleFileNameEx(processHandle, NULL, filename, MAX_PATH);
            CloseHandle(processHandle);
        }
        return std::wstring(filename);
    }

    std::wstring GetCurrentDesktopId() const
    {
        if (m_desktopManager == nullptr)
        {
            return L"";
        }

        GUID desktopId{};
        HRESULT result = m_desktopManager->GetWindowDesktopId(GetForegroundWindow(), &desktopId);
        if (FAILED(result))
        {
            return L"";
        }

        wchar_t guidString[39];
        StringFromGUID2(desktopId, guidString, 39);
        return std::wstring(guidString);
    }

public:
    WindowFinder()
    {
        if (SUCCEEDED(CoInitialize(nullptr)))
        {
            CoCreateInstance(CLSID_VirtualDesktopManager, nullptr, CLSCTX_ALL, IID_PPV_ARGS(&m_desktopManager));
        }
    }

    ~WindowFinder()
    {
        if (m_desktopManager)
        {
            m_desktopManager->Release();
            m_desktopManager = nullptr;
        }
        CoUninitialize();
    }

    std::wstring GetProcessUniqueId(HWND windowHandle) const
    {
        DWORD processId;
        GetWindowThreadProcessId(windowHandle, &processId);
        std::wstring processName = GetProcessNameFromProcessId(processId);
        if (processName.empty())
        {
            return L"";
        }

        std::wstring desktopId = GetCurrentDesktopId();
        return desktopId + processName;
    }

    std::wstring GetCurrentProcessUniqueId() const
    {
        return GetProcessUniqueId(GetForegroundWindow());
    }

    BOOL IsWindowOnCurrentDesktop(HWND windowHandle) const
    {
        if (m_desktopManager == nullptr)
        {
            return TRUE;
        }

        BOOL isWindowOnCurrentDesktop;
        if (SUCCEEDED(m_desktopManager->IsWindowOnCurrentVirtualDesktop(windowHandle, &isWindowOnCurrentDesktop)) &&
            !isWindowOnCurrentDesktop)
        {
            return FALSE;
        }

        return TRUE;
    }

    BOOL IsWindowFromCurrentProcess(HWND windowHandle) const
    {
        return GetProcessUniqueId(windowHandle) == GetCurrentProcessUniqueId();
    }

    struct EnumWindowsCallbackArgs
    {
        WindowFinder* windowFinder;
        std::vector<HWND> windowHandles;
        bool ignoreMinimized;

        EnumWindowsCallbackArgs(WindowFinder* wf, bool ignoreMin) : windowFinder(wf), ignoreMinimized(ignoreMin) {}
    };

    static BOOL CALLBACK EnumWindowsCallback(HWND windowHandle, LPARAM parameters)
    {
        EnumWindowsCallbackArgs* args = reinterpret_cast<EnumWindowsCallbackArgs*>(parameters);

        if (GetForegroundWindow() == windowHandle)
        {
            return TRUE;
        }

        // Skip minimized windows if the setting is enabled
        if (args->ignoreMinimized && IsIconic(windowHandle))
        {
            return TRUE;
        }

        if (GetWindow(windowHandle, GW_OWNER) != static_cast<HWND>(0) || !IsWindowVisible(windowHandle))
        {
            return TRUE;
        }

        DWORD windowStyle = static_cast<DWORD>(GetWindowLongPtr(windowHandle, GWL_STYLE));
        DWORD windowExtendedStyle = static_cast<DWORD>(GetWindowLongPtr(windowHandle, GWL_EXSTYLE));
        BOOL hasAppWindowStyle = (windowExtendedStyle & WS_EX_APPWINDOW) != 0;
        if ((windowStyle & WS_POPUP) != 0 && !hasAppWindowStyle)
        {
            return TRUE;
        }

        if ((windowExtendedStyle & WS_EX_TOOLWINDOW) != 0)
        {
            return TRUE;
        }

        WINDOWINFO windowInfo;
        windowInfo.cbSize = sizeof(WINDOWINFO);
        GetWindowInfo(windowHandle, &windowInfo);
        if (windowInfo.rcWindow.right - windowInfo.rcWindow.left <= 1 ||
            windowInfo.rcWindow.bottom - windowInfo.rcWindow.top <= 1)
        {
            return TRUE;
        }

        if (!args->windowFinder->IsWindowFromCurrentProcess(windowHandle))
        {
            return TRUE;
        }

        if (!args->windowFinder->IsWindowOnCurrentDesktop(windowHandle))
        {
            return TRUE;
        }

        args->windowHandles.push_back(windowHandle);
        return TRUE;
    }

    std::vector<HWND> FindCurrentProcessWindows(bool ignoreMinimized)
    {
        EnumWindowsCallbackArgs args(this, ignoreMinimized);
        EnumWindows(&EnumWindowsCallback, reinterpret_cast<LPARAM>(&args));
        return args.windowHandles;
    }
};

// Implement the PowerToy Module Interface
class AltBacktick : public PowertoyModuleIface
{
private:
    bool m_enabled = false;
    ModifierKeyEnum m_modifierKey = ModifierKeyEnum::Alt;
    bool m_ignoreMinimizedWindows = true;

    // Keyboard hook
    HHOOK m_keyboardHook = nullptr;

    // MRU tracking
    std::unordered_map<std::wstring, std::deque<HWND>> m_mruMap;
    std::unordered_map<std::wstring, int> m_offsets;
    HWND m_lastWindow = nullptr;
    bool m_isModifierKeyPressed = false;

    // Window finder
    WindowFinder m_windowFinder;

    // Hotkey ID
    static constexpr int HOTKEY_ID = 1;
    static constexpr int HOTKEY_REVERSE_ID = 2;

    // Static instance for keyboard hook callback
    static inline std::atomic<AltBacktick*> s_instance{ nullptr };

    bool IsModifierKeyEvent(const KBDLLHOOKSTRUCT* kbEvent) const
    {
        if (m_modifierKey == ModifierKeyEnum::Alt)
        {
            return kbEvent->vkCode == VK_MENU || kbEvent->vkCode == VK_LMENU || kbEvent->vkCode == VK_RMENU;
        }
        else if (m_modifierKey == ModifierKeyEnum::Win)
        {
            return kbEvent->vkCode == VK_LWIN || kbEvent->vkCode == VK_RWIN;
        }
        return false;
    }

    void UpdateMRUForProcess(HWND currentWindow, const std::wstring& processUniqueId)
    {
        auto& mru = m_mruMap[processUniqueId];
        auto it = std::find(mru.begin(), mru.end(), currentWindow);
        if (it != mru.end())
        {
            mru.erase(it);
        }
        mru.push_front(currentWindow);
        m_offsets[processUniqueId] = 0;
    }

    static LRESULT CALLBACK KeyboardHookProc(int nCode, WPARAM wParam, LPARAM lParam)
    {
        AltBacktick* instance = s_instance.load();
        if (instance && instance->m_isModifierKeyPressed && nCode == HC_ACTION && wParam == WM_KEYUP)
        {
            KBDLLHOOKSTRUCT* kbEvent = reinterpret_cast<KBDLLHOOKSTRUCT*>(lParam);
            if (instance->IsModifierKeyEvent(kbEvent))
            {
                HWND currentWindow = GetForegroundWindow();
                if (currentWindow != nullptr)
                {
                    std::wstring currentProcessUniqueId = instance->m_windowFinder.GetProcessUniqueId(currentWindow);
                    if (!currentProcessUniqueId.empty())
                    {
                        instance->UpdateMRUForProcess(currentWindow, currentProcessUniqueId);
                    }
                }
                instance->m_isModifierKeyPressed = false;
                instance->m_lastWindow = currentWindow;
            }
        }
        return CallNextHookEx(NULL, nCode, wParam, lParam);
    }

    void ProcessHotkey(bool isReverse)
    {
        HWND currentWindowHandle = GetForegroundWindow();
        std::wstring processUniqueId = m_windowFinder.GetProcessUniqueId(currentWindowHandle);
        if (processUniqueId.empty())
        {
            return;
        }

        std::deque<HWND>& mru = m_mruMap[processUniqueId];
        int& offset = m_offsets[processUniqueId];

        // Remove invalid windows from MRU
        // When ignoreMinimizedWindows is true, also remove minimized windows from the list
        for (auto handle = mru.begin(); handle != mru.end();)
        {
            if (!IsWindow(*handle))
            {
                handle = mru.erase(handle);
            }
            else if (m_ignoreMinimizedWindows && IsIconic(*handle))
            {
                // Remove minimized windows from MRU when ignore setting is enabled
                handle = mru.erase(handle);
            }
            else
            {
                ++handle;
            }
        }

        // Update MRU if window changed
        if (currentWindowHandle != m_lastWindow)
        {
            UpdateMRUForProcess(currentWindowHandle, processUniqueId);
        }

        // Find all windows for current process (respecting ignoreMinimizedWindows setting)
        std::vector<HWND> windows = m_windowFinder.FindCurrentProcessWindows(m_ignoreMinimizedWindows);
        for (const HWND& window : windows)
        {
            if (std::find(mru.begin(), mru.end(), window) == mru.end())
            {
                mru.push_back(window);
            }
        }

        if (mru.empty())
        {
            return;
        }

        // Ensure offset is within bounds
        if (offset < 0 || static_cast<size_t>(offset) >= mru.size())
        {
            offset = 0;
        }

        // Calculate next window index
        if (isReverse)
        {
            if (offset > 0)
            {
                offset--;
            }
            else
            {
                offset = static_cast<int>(mru.size()) - 1;
            }
        }
        else
        {
            if (static_cast<size_t>(offset) + 1 < mru.size())
            {
                offset++;
            }
            else
            {
                offset = 0;
            }
        }

        HWND windowToFocus = mru[offset];

        if (windowToFocus != nullptr && windowToFocus != currentWindowHandle)
        {
            WINDOWPLACEMENT placement;
            GetWindowPlacement(windowToFocus, &placement);
            if (placement.showCmd == SW_SHOWMINIMIZED)
            {
                ShowWindow(windowToFocus, SW_RESTORE);
            }
            SetForegroundWindow(windowToFocus);
            m_lastWindow = windowToFocus;
        }

        m_isModifierKeyPressed = true;
    }

    void RegisterHotkeys()
    {
        UINT keyCode = MapVirtualKey(BACKTICK_SCAN_CODE, MAPVK_VSC_TO_VK);
        UINT modKey = (m_modifierKey == ModifierKeyEnum::Alt) ? MOD_ALT : MOD_WIN;

        if (!RegisterHotKey(NULL, HOTKEY_ID, modKey | MOD_NOREPEAT, keyCode))
        {
            Logger::error("Failed to register hotkey");
        }

        if (!RegisterHotKey(NULL, HOTKEY_REVERSE_ID, modKey | MOD_SHIFT | MOD_NOREPEAT, keyCode))
        {
            Logger::error("Failed to register reverse hotkey");
        }
    }

    void UnregisterHotkeys()
    {
        UnregisterHotKey(NULL, HOTKEY_ID);
        UnregisterHotKey(NULL, HOTKEY_REVERSE_ID);
    }

    void LoadSettings()
    {
        try
        {
            PowerToysSettings::PowerToyValues settings =
                PowerToysSettings::PowerToyValues::load_from_settings_file(MODULE_KEY);

            auto settingsObject = settings.get_raw_json();
            if (settingsObject.GetView().Size())
            {
                try
                {
                    auto propertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES);
                    
                    if (propertiesObject.HasKey(JSON_KEY_MODIFIER_KEY))
                    {
                        auto modifierKeyObj = propertiesObject.GetNamedObject(JSON_KEY_MODIFIER_KEY);
                        int modifierKeyValue = static_cast<int>(modifierKeyObj.GetNamedNumber(L"value", 0));
                        m_modifierKey = static_cast<ModifierKeyEnum>(modifierKeyValue);
                        Logger::info("Loaded modifier_key setting: {}", modifierKeyValue);
                    }

                    if (propertiesObject.HasKey(JSON_KEY_IGNORE_MINIMIZED_WINDOWS))
                    {
                        auto ignoreMinObj = propertiesObject.GetNamedObject(JSON_KEY_IGNORE_MINIMIZED_WINDOWS);
                        m_ignoreMinimizedWindows = ignoreMinObj.GetNamedBoolean(L"value", true);
                        Logger::info("Loaded ignore_minimized_windows setting: {}", m_ignoreMinimizedWindows);
                    }
                }
                catch (...)
                {
                    Logger::error("Failed to parse settings properties");
                }
            }
        }
        catch (...)
        {
            Logger::error("Failed to load settings file");
        }

        Logger::info("Current settings - modifier_key: {}, ignore_minimized_windows: {}", 
            static_cast<int>(m_modifierKey), m_ignoreMinimizedWindows);
    }

    // Message loop thread
    std::thread m_messageLoopThread;
    std::atomic<bool> m_messageLoopRunning{ false };
    DWORD m_messageLoopThreadId = 0;

    void MessageLoopThreadProc()
    {
        m_messageLoopThreadId = GetCurrentThreadId();

        // Register hotkeys on this thread
        RegisterHotkeys();

        // Install keyboard hook
        m_keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, KeyboardHookProc, NULL, 0);
        if (m_keyboardHook == NULL)
        {
            Logger::error("Failed to install keyboard hook");
        }

        // Message loop
        MSG msg;
        while (m_messageLoopRunning && GetMessage(&msg, NULL, 0, 0))
        {
            if (msg.message == WM_HOTKEY)
            {
                int hotkeyId = static_cast<int>(msg.wParam);
                ProcessHotkey(hotkeyId == HOTKEY_REVERSE_ID);
            }
            TranslateMessage(&msg);
            DispatchMessage(&msg);
        }

        // Cleanup
        UnregisterHotkeys();
        if (m_keyboardHook)
        {
            UnhookWindowsHookEx(m_keyboardHook);
            m_keyboardHook = nullptr;
        }
    }

public:
    AltBacktick()
    {
        std::filesystem::path logFilePath(PTSettingsHelper::get_module_save_folder_location(MODULE_KEY));
        logFilePath.append(L"Logs");
        std::filesystem::create_directories(logFilePath);
        logFilePath.append(L"AltBacktick.log");
        Logger::init("AltBacktick", logFilePath.wstring(), PTSettingsHelper::get_log_settings_file_location());
        Logger::info("AltBacktick module constructed");
        LoadSettings();
    }

    ~AltBacktick()
    {
        if (m_enabled)
        {
            disable();
        }
    }

    // Return the configured status for the gpo policy for the module
    virtual powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
    {
        return powertoys_gpo::gpo_rule_configured_not_configured;
    }

    virtual const wchar_t* get_name() override
    {
        return MODULE_NAME;
    }

    virtual const wchar_t* get_key() override
    {
        return MODULE_KEY;
    }

    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);
        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.set_description(MODULE_DESC);
        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    virtual void set_config(const wchar_t* config) override
    {
        try
        {
            PowerToysSettings::PowerToyValues values =
                PowerToysSettings::PowerToyValues::from_json_string(config, get_key());
            values.save_to_settings_file();

            // Reload settings
            LoadSettings();

            // If enabled, restart to apply new settings
            if (m_enabled)
            {
                disable();
                enable();
            }
        }
        catch (std::exception&)
        {
            Logger::error("Failed to set config");
        }
    }

    virtual void enable() override
    {
        if (m_enabled)
        {
            return;
        }

        Logger::info("Enabling AltBacktick");
        m_enabled = true;
        Trace::EnableAltBacktick(true);

        // Set static instance for hook callback
        s_instance.store(this);

        // Start message loop thread
        m_messageLoopRunning = true;
        m_messageLoopThread = std::thread(&AltBacktick::MessageLoopThreadProc, this);
    }

    virtual void disable() override
    {
        if (!m_enabled)
        {
            return;
        }

        Logger::info("Disabling AltBacktick");
        m_enabled = false;
        Trace::EnableAltBacktick(false);

        // Stop message loop
        m_messageLoopRunning = false;
        
        // Post a quit message to break the message loop
        if (m_messageLoopThreadId != 0)
        {
            PostThreadMessage(m_messageLoopThreadId, WM_QUIT, 0, 0);
        }
        if (m_messageLoopThread.joinable())
        {
            m_messageLoopThread.join();
        }
        m_messageLoopThreadId = 0;

        // Clear static instance
        s_instance.store(nullptr);

        // Clear MRU state
        m_mruMap.clear();
        m_offsets.clear();
        m_lastWindow = nullptr;
        m_isModifierKeyPressed = false;
    }

    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    virtual void destroy() override
    {
        delete this;
    }
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new AltBacktick();
}
