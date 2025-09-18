#include "pch.h"
#include "trace.h"
#include <atlbase.h>
#include <atomic>
#include <comdef.h>
#include <common/interop/shared_constants.h>
#include <common/logger/logger.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/utils/elevation.h>
#include <common/utils/logger_helper.h>
#include <common/utils/winapi_error.h>
#include <exdisp.h>
#include <filesystem>
#include <interface/powertoy_module_interface.h>

extern "C" IMAGE_DOS_HEADER __ImageBase;

BOOL APIENTRY DllMain(HMODULE /*hModule*/,
                      DWORD ul_reason_for_call,
                      LPVOID /*lpReserved*/)
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

// Forward declare global Peek so anonymous namespace uses same type
class Peek;

namespace
{
    const wchar_t JSON_KEY_PROPERTIES[] = L"properties";
    const wchar_t JSON_KEY_WIN[] = L"win";
    const wchar_t JSON_KEY_ALT[] = L"alt";
    const wchar_t JSON_KEY_CTRL[] = L"ctrl";
    const wchar_t JSON_KEY_SHIFT[] = L"shift";
    const wchar_t JSON_KEY_CODE[] = L"code";
    const wchar_t JSON_KEY_ACTIVATION_SHORTCUT[] = L"ActivationShortcut";
    const wchar_t JSON_KEY_ALWAYS_RUN_NOT_ELEVATED[] = L"AlwaysRunNotElevated";
    const wchar_t JSON_KEY_ENABLE_SPACE_TO_ACTIVATE[] = L"EnableSpaceToActivate";

    // Space activation (single-space mode) state
    std::atomic_bool g_foregroundHookActive{ false }; // Foreground hook installed
    std::atomic_bool g_foregroundEligible{ false }; // Cached eligibility (Explorer/Desktop/Peek focused)
    HWINEVENTHOOK g_foregroundHook = nullptr; // Foreground change hook handle
    constexpr DWORD FOREGROUND_DEBOUNCE_MS = 40; // Delay before eligibility recompute (ms)
    HANDLE g_foregroundDebounceTimer = nullptr; // One-shot scheduled timer
    std::atomic<DWORD> g_foregroundLastScheduleTick{ 0 }; // Tick count when timer last scheduled

    Peek* g_instance = nullptr; // pointer to active instance (global Peek)
}

// The PowerToy name that will be shown in the settings.
const static wchar_t* MODULE_NAME = L"Peek";
// Add a description that will we shown in the module settings page.
const static wchar_t* MODULE_DESC = L"A module that previews an image file.";

// Implement the PowerToy Module Interface and all the required methods.
class Peek : public PowertoyModuleIface
{
private:
    // The PowerToy state.
    bool m_enabled = false;

    Hotkey m_hotkey;

    // If we should always try to run Peek non-elevated.
    bool m_alwaysRunNotElevated = true;
    bool m_enableSpaceToActivate = false; // toggle from settings
    Hotkey m_previousHotkey{}; // stored previous shortcut when forcing space

    HANDLE m_hProcess = 0;
    DWORD m_processPid = 0;

    HANDLE m_hInvokeEvent;
    HANDLE m_hTerminateEvent;

    // Load the settings file.
    void init_settings()
    {
        try
        {
            // Load and parse the settings file for this PowerToy.
            PowerToysSettings::PowerToyValues settings =
                PowerToysSettings::PowerToyValues::load_from_settings_file(Peek::get_name());

            parse_settings(settings);
        }
        catch (std::exception&)
        {
            // Error while loading from the settings file. Let default values stay as they are.
        }
    }

    void parse_settings(PowerToysSettings::PowerToyValues& settings)
    {
        auto settingsObject = settings.get_raw_json();
        if (settingsObject.GetView().Size())
        {
            try
            {
                auto jsonHotkeyObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_ACTIVATION_SHORTCUT);
                parse_hotkey(jsonHotkeyObject);
            }
            catch (...)
            {
                Logger::error("Failed to initialize Peek hotkey settings");

                set_default_key_settings();
            }
            try
            {
                auto jsonAlwaysRunNotElevatedObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_ALWAYS_RUN_NOT_ELEVATED);
                m_alwaysRunNotElevated = jsonAlwaysRunNotElevatedObject.GetNamedBoolean(L"value");
            }
            catch (...)
            {
                Logger::error("Failed to initialize Always Run Not Elevated option. Setting to default.");

                m_alwaysRunNotElevated = true;
            }
            try
            {
                auto jsonEnableSpaceObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_ENABLE_SPACE_TO_ACTIVATE);
                m_enableSpaceToActivate = jsonEnableSpaceObject.GetNamedBoolean(L"value");
            }
            catch (...)
            {
                m_enableSpaceToActivate = false;
            }

            // Enforce design: if space toggle ON, force single-space hotkey and store previous combination once.
            if (m_enableSpaceToActivate)
            {
                if (!(m_hotkey.win || m_hotkey.alt || m_hotkey.shift || m_hotkey.ctrl) && m_hotkey.key == ' ')
                {
                    // already single space
                }
                else
                {
                    m_previousHotkey = m_hotkey; // stash existing
                    m_hotkey.win = false;
                    m_hotkey.alt = false;
                    m_hotkey.shift = false;
                    m_hotkey.ctrl = false;
                    m_hotkey.key = ' ';
                }
            }
            else
            {
                // If toggle off and current hotkey is bare space, revert to default (simplified policy)
                if (!(m_hotkey.win || m_hotkey.alt || m_hotkey.shift || m_hotkey.ctrl) && m_hotkey.key == ' ')
                {
                    set_default_key_settings();
                }
            }

            manage_space_mode_hook();
            Trace::SpaceModeEnabled(m_enableSpaceToActivate);
        }
        else
        {
            Logger::info("Peek settings are empty");
            set_default_key_settings();
        }
    }

    void set_default_key_settings()
    {
        Logger::info("Peek is going to use default key settings");
        m_hotkey.win = false;
        m_hotkey.alt = false;
        m_hotkey.shift = false;
        m_hotkey.ctrl = true;
        m_hotkey.key = ' ';
    }

    // Eligibility recompute (debounced via timer)
public: // callable from anonymous namespace helper
    void recompute_space_mode_eligibility()
    {
        if (!m_enableSpaceToActivate)
        {
            g_foregroundEligible.store(false, std::memory_order_relaxed);
            return;
        }
        const bool eligible = is_peek_or_explorer_or_desktop_window_focused();
        g_foregroundEligible.store(eligible, std::memory_order_relaxed);
        Logger::debug(L"Peek space-mode eligibility recomputed: {}", eligible);
    }

private:
    static void CALLBACK ForegroundDebounceTimerProc(PVOID /*param*/, BOOLEAN /*fired*/)
    {
        if (!g_instance || !g_foregroundHookActive.load(std::memory_order_relaxed))
        {
            return;
        }
        g_instance->recompute_space_mode_eligibility();
    }

    static void CALLBACK ForegroundWinEventProc(HWINEVENTHOOK /*hook*/, DWORD /*event*/, HWND /*hwnd*/, LONG /*idObject*/, LONG /*idChild*/, DWORD /*thread*/, DWORD /*time*/)
    {
        if (!g_foregroundHookActive.load(std::memory_order_relaxed) || !g_instance)
        {
            return;
        }
        const DWORD now = GetTickCount();
        const DWORD last = g_foregroundLastScheduleTick.load(std::memory_order_relaxed);
        // If no timer or sufficient time since last schedule, create a new one.
        if (!g_foregroundDebounceTimer || (now - last) >= FOREGROUND_DEBOUNCE_MS || now < last)
        {
            if (g_foregroundDebounceTimer)
            {
                // Best effort: cancel previous pending timer; ignore failure.
                DeleteTimerQueueTimer(nullptr, g_foregroundDebounceTimer, nullptr);
                g_foregroundDebounceTimer = nullptr;
            }
            if (CreateTimerQueueTimer(&g_foregroundDebounceTimer, nullptr, ForegroundDebounceTimerProc, nullptr, FOREGROUND_DEBOUNCE_MS, 0, WT_EXECUTEDEFAULT))
            {
                g_foregroundLastScheduleTick.store(now, std::memory_order_relaxed);
            }
            else
            {
                Logger::warn(L"Peek failed to create foreground debounce timer");
                // Fallback: compute immediately if timer creation failed.
                g_instance->recompute_space_mode_eligibility();
            }
        }
    }

    void install_foreground_hook()
    {
        if (g_foregroundHook || !m_enableSpaceToActivate)
        {
            return;
        }

        g_instance = this;
        g_foregroundHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, nullptr, ForegroundWinEventProc, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
        if (g_foregroundHook)
        {
            g_foregroundHookActive.store(true, std::memory_order_relaxed);
            recompute_space_mode_eligibility();
        }
        else
        {
            g_foregroundHookActive.store(false, std::memory_order_relaxed);
            Logger::warn(L"Peek failed to install foreground hook. Falling back to polling.");
        }
    }

    void uninstall_foreground_hook()
    {
        if (g_foregroundHook)
        {
            UnhookWinEvent(g_foregroundHook);
            g_foregroundHook = nullptr;
        }
        if (g_foregroundDebounceTimer)
        {
            DeleteTimerQueueTimer(nullptr, g_foregroundDebounceTimer, nullptr);
            g_foregroundDebounceTimer = nullptr;
        }
        g_foregroundLastScheduleTick.store(0, std::memory_order_relaxed);
        g_foregroundHookActive.store(false, std::memory_order_relaxed);
        g_foregroundEligible.store(false, std::memory_order_relaxed);
        g_instance = nullptr;
    }

    void manage_space_mode_hook()
    {
        if (m_enableSpaceToActivate && m_enabled)
        {
            install_foreground_hook();
        }
        else
        {
            uninstall_foreground_hook();
        }
    }

    void parse_hotkey(winrt::Windows::Data::Json::JsonObject& jsonHotkeyObject)
    {
        try
        {
            m_hotkey.win = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_WIN);
            m_hotkey.alt = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_ALT);
            m_hotkey.shift = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_SHIFT);
            m_hotkey.ctrl = jsonHotkeyObject.GetNamedBoolean(JSON_KEY_CTRL);
            m_hotkey.key = static_cast<unsigned char>(jsonHotkeyObject.GetNamedNumber(JSON_KEY_CODE));
        }
        catch (...)
        {
            Logger::error("Failed to initialize Peek start shortcut");
        }

        if (!m_hotkey.key)
        {
            Logger::info("Peek is going to use default shortcut");
            m_hotkey.win = false;
            m_hotkey.alt = false;
            m_hotkey.shift = false;
            m_hotkey.ctrl = true;
            m_hotkey.key = ' ';
        }
    }

    bool is_desktop_window(HWND windowHandle)
    {
        // Similar to the logic in IsDesktopWindow in Peek UI. Keep logic synced.
        // TODO: Refactor into same C++ class consumed by both.
        wchar_t className[MAX_PATH];
        if (GetClassName(windowHandle, className, MAX_PATH) == 0)
        {
            return false;
        }
        if (wcsncmp(className, L"Progman", MAX_PATH) != 0 && wcsncmp(className, L"WorkerW", MAX_PATH) != 0)
        {
            return false;
        }
        return FindWindowEx(windowHandle, NULL, L"SHELLDLL_DefView", NULL);
    }

    inline std::wstring GetErrorString(HRESULT handle)
    {
        _com_error err(handle);
        return err.ErrorMessage();
    }

    bool is_explorer_window(HWND windowHandle)
    {
        CComPtr<IShellWindows> spShellWindows;
        auto result = spShellWindows.CoCreateInstance(CLSID_ShellWindows);
        if (result != S_OK || spShellWindows == nullptr)
        {
            Logger::warn(L"Failed to create instance. {}", GetErrorString(result));
            return true; // Might as well assume it's possible it's an explorer window.
        }

        // Enumerate all Shell Windows to compare the window handle against.
        IUnknownPtr spEnum{}; // _com_ptr_t; no Release required.
        result = spShellWindows->_NewEnum(&spEnum);
        if (result != S_OK || spEnum == nullptr)
        {
            Logger::warn(L"Failed to list explorer Windows. {}", GetErrorString(result));
            return true; // Might as well assume it's possible it's an explorer window.
        }

        IEnumVARIANTPtr spEnumVariant{}; // _com_ptr_t; no Release required.
        result = spEnum.QueryInterface(__uuidof(spEnumVariant), &spEnumVariant);
        if (result != S_OK || spEnumVariant == nullptr)
        {
            Logger::warn(L"Failed to enum explorer Windows. {}", GetErrorString(result));
            spEnum->Release();
            return true; // Might as well assume it's possible it's an explorer window.
        }

        variant_t variantElement{};
        while (spEnumVariant->Next(1, &variantElement, NULL) == S_OK)
        {
            IWebBrowserApp* spWebBrowserApp;
            result = variantElement.pdispVal->QueryInterface(IID_IWebBrowserApp, reinterpret_cast<void**>(&spWebBrowserApp));
            if (result == S_OK)
            {
                HWND hwnd;
                result = spWebBrowserApp->get_HWND(reinterpret_cast<SHANDLE_PTR*>(&hwnd));
                if (result == S_OK)
                {
                    if (hwnd == windowHandle)
                    {
                        VariantClear(&variantElement);
                        spWebBrowserApp->Release();
                        return true;
                    }
                }
                spWebBrowserApp->Release();
            }
            VariantClear(&variantElement);
        }

        return false;
    }

    bool is_peek_or_explorer_or_desktop_window_focused()
    {
        HWND foregroundWindowHandle = GetForegroundWindow();
        if (foregroundWindowHandle == NULL)
        {
            return false;
        }

        DWORD pid{};
        if (GetWindowThreadProcessId(foregroundWindowHandle, &pid) != 0)
        {
            // If the foreground window is the Peek window, send activation signal.
            if (m_processPid != 0 && pid == m_processPid)
            {
                return true;
            }
        }

        if (is_desktop_window(foregroundWindowHandle))
        {
            return true;
        }

        return is_explorer_window(foregroundWindowHandle);
    }

    bool is_viewer_running()
    {
        if (m_hProcess == 0)
        {
            return false;
        }
        return WaitForSingleObject(m_hProcess, 0) == WAIT_TIMEOUT;
    }

    void launch_process()
    {
        Logger::trace(L"Starting Peek.UI process");

        unsigned long powertoys_pid = GetCurrentProcessId();

        std::wstring executable_args = L"";
        executable_args.append(std::to_wstring(powertoys_pid));

        if (m_alwaysRunNotElevated && is_process_elevated(false))
        {
            Logger::trace("Starting Peek non elevated from elevated process");
            const auto modulePath = get_module_folderpath();
            std::wstring runExecutablePath = modulePath;
            runExecutablePath += L"\\WinUI3Apps\\PowerToys.Peek.UI.exe";
            std::optional<ProcessInfo> processStartedInfo = RunNonElevatedFailsafe(runExecutablePath, executable_args, modulePath, PROCESS_QUERY_INFORMATION | SYNCHRONIZE | PROCESS_TERMINATE);
            if (processStartedInfo.has_value())
            {
                m_processPid = processStartedInfo.value().processID;
                m_hProcess = processStartedInfo.value().processHandle.release();
            }
            else
            {
                Logger::error(L"PeekViewer failed to start not elevated.");
            }
        }
        else
        {
            SHELLEXECUTEINFOW sei{ sizeof(sei) };

            sei.fMask = { SEE_MASK_NOCLOSEPROCESS };
            sei.lpVerb = L"open";
            sei.lpFile = L"WinUI3Apps\\PowerToys.Peek.UI.exe";
            sei.nShow = SW_SHOWNORMAL;
            sei.lpParameters = executable_args.data();

            if (ShellExecuteExW(&sei))
            {
                Logger::trace("Successfully started the PeekViewer process");
            }
            else
            {
                Logger::error(L"PeekViewer failed to start. {}", get_last_error_or_default(GetLastError()));
            }

            m_hProcess = sei.hProcess;
            m_processPid = GetProcessId(m_hProcess);
        }
    }

public:
    Peek()
    {
        LoggerHelpers::init_logger(MODULE_NAME, L"ModuleInterface", "Peek");
        g_instance = this;
        init_settings();

        m_hInvokeEvent = CreateDefaultEvent(CommonSharedConstants::SHOW_PEEK_SHARED_EVENT);
        m_hTerminateEvent = CreateDefaultEvent(CommonSharedConstants::TERMINATE_PEEK_SHARED_EVENT);
    };

    ~Peek()
    {
        if (m_enabled)
        {
        }
        m_enabled = false;
        uninstall_foreground_hook();
        if (g_instance == this)
        {
            g_instance = nullptr;
        }
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

    virtual const wchar_t* get_key() override
    {
        return MODULE_NAME;
    }

    // Return the configured status for the gpo policy for the module
    virtual powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
    {
        return powertoys_gpo::getConfiguredPeekEnabledValue();
    }

    // Return JSON with the configuration options.
    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        // Create a Settings object.
        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.set_description(MODULE_DESC);
        settings.add_bool_toggle(JSON_KEY_ENABLE_SPACE_TO_ACTIVATE, L"Enable single Space key activation", m_enableSpaceToActivate);

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    // Called by the runner to pass the updated settings values as a serialized JSON.
    virtual void set_config(const wchar_t* config) override
    {
        try
        {
            // Parse the input JSON string.
            PowerToysSettings::PowerToyValues values =
                PowerToysSettings::PowerToyValues::from_json_string(config, get_key());

            parse_settings(values);

            values.save_to_settings_file();
        }
        catch (std::exception&)
        {
            // Improper JSON.
        }
    }

    // Enable the powertoy
    virtual void enable()
    {
        Logger::trace("Peek::enable()");
        ResetEvent(m_hInvokeEvent);
        launch_process();
        m_enabled = true;
        Trace::EnablePeek(true);
        manage_space_mode_hook();
    }

    // Disable the powertoy
    virtual void disable()
    {
        Logger::trace("Peek::disable()");
        if (m_enabled)
        {
            ResetEvent(m_hInvokeEvent);
            SetEvent(m_hTerminateEvent);

            HANDLE hProcess = OpenProcess(SYNCHRONIZE | PROCESS_TERMINATE, FALSE, m_processPid);
            if (WaitForSingleObject(hProcess, 1500) == WAIT_TIMEOUT)
            {
                auto result = TerminateProcess(hProcess, 1);
                if (result == 0)
                {
                    int error = GetLastError();
                    Logger::trace("Couldn't terminate the process. Last error: {}", error);
                }
            }

            CloseHandle(hProcess);
            CloseHandle(m_hProcess);
            m_hProcess = 0;
            m_processPid = 0;
        }

        m_enabled = false;
        Trace::EnablePeek(false);
        uninstall_foreground_hook();
    }

    // Returns if the powertoys is enabled
    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    virtual size_t get_hotkeys(Hotkey* hotkeys, size_t buffer_size) override
    {
        if (m_hotkey.key)
        {
            if (hotkeys && buffer_size >= 1)
            {
                hotkeys[0] = m_hotkey;
            }

            return 1;
        }
        else
        {
            return 0;
        }
    }

    virtual bool on_hotkey(size_t /*hotkeyId*/) override
    {
        if (m_enabled)
        {
            bool spaceMode = m_enableSpaceToActivate && !(m_hotkey.win || m_hotkey.alt || m_hotkey.shift || m_hotkey.ctrl) && m_hotkey.key == ' ';
            bool eligible = false;
            if (spaceMode && g_foregroundHookActive.load(std::memory_order_relaxed))
            {
                eligible = g_foregroundEligible.load(std::memory_order_relaxed);
            }
            else
            {
                eligible = is_peek_or_explorer_or_desktop_window_focused();
            }

            if (eligible)
            {
                Logger::trace(L"Peek hotkey pressed and eligible for launching");

                // TODO: fix VK_SPACE DestroyWindow in viewer app
                if (!is_viewer_running())
                {
                    launch_process();
                }

                SetEvent(m_hInvokeEvent);

                Trace::PeekInvoked();
                return true;
            }
        }

        return false;
    }

    virtual void send_settings_telemetry() override
    {
        Logger::info("Send settings telemetry");
        Trace::SettingsTelemetry(m_hotkey);
    }
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new Peek();
}