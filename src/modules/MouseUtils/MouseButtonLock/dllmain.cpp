// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/utils/logger_helper.h>
#include <common/logger/logger.h>
#include "trace.h"
#include "resource.h"
#include "MouseButtonLockCore.h"

#include <atomic>
#include <thread>

// Mouse Button Lock
//
// A ClickLock equivalent for the right and middle mouse buttons. Hold a button past a
// configurable threshold and release it: the physical button-up is suppressed inside the
// low-level mouse hook, so the OS keeps believing the button is held. The next physical tap
// of that button releases the synthetic hold (and is itself swallowed cleanly so downstream
// apps only ever see the injected up).
//
// The hook lives on a dedicated thread with its own message pump, matching the other Mouse
// Utilities (see CursorWrap). The button-state machine itself lives in MouseButtonLockCore.h
// (Win32-free and unit tested); this file is the thin Win32 adapter around it.

extern "C" IMAGE_DOS_HEADER __ImageBase;

BOOL APIENTRY DllMain(HMODULE /*hModule*/, DWORD ul_reason_for_call, LPVOID /*lpReserved*/)
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

// Non-localizable strings.
namespace
{
    const wchar_t JSON_KEY_PROPERTIES[] = L"properties";
    const wchar_t JSON_KEY_VALUE[] = L"value";
    const wchar_t JSON_KEY_RMB_LOCK_ENABLED[] = L"rmb_lock_enabled";
    const wchar_t JSON_KEY_MMB_LOCK_ENABLED[] = L"mmb_lock_enabled";
    const wchar_t JSON_KEY_HOLD_DURATION_MS[] = L"hold_duration_ms";
    const wchar_t JSON_KEY_MOVE_CANCEL_ENABLED[] = L"move_cancel_enabled";
    const wchar_t JSON_KEY_MOVE_CANCEL_PIXELS[] = L"move_cancel_pixels";

    // dwExtraInfo tag stamped on every event we inject via SendInput, so the hook ignores
    // our own synthetic events and we don't recurse. Magic value 'WINM' (carried over from the
    // standalone reference app); distinct from the centralized keyboard hook's 0x110 flag.
    constexpr ULONG_PTR INJECTION_TAG = 0x57494E4D;

    // Default values mirror the C# MouseButtonLockProperties defaults.
    constexpr int DEFAULT_HOLD_DURATION_MS = 300;
    constexpr int DEFAULT_MOVE_CANCEL_PIXELS = 5;

    // Accepted ranges when reading from settings.json. The Settings UI already constrains these,
    // but a hand-edited file could carry out-of-range or non-finite values, so clamp on read.
    // A hold of 0 would latch every ordinary click; a huge hold would make locking unreachable.
    constexpr int MIN_HOLD_DURATION_MS = 50;
    constexpr int MAX_HOLD_DURATION_MS = 60000;
    constexpr int MAX_MOVE_CANCEL_PIXELS = 10000;

    // Production injector: a tagged SendInput. Lives behind the engine's IButtonUpInjector so the
    // state machine can be unit tested without Win32.
    class WinInjector : public mousebuttonlock::IButtonUpInjector
    {
    public:
        bool InjectUp(mousebuttonlock::MouseButton button) override
        {
            const DWORD flag = button == mousebuttonlock::MouseButton::Right ? MOUSEEVENTF_RIGHTUP : MOUSEEVENTF_MIDDLEUP;
            INPUT input{};
            input.type = INPUT_MOUSE;
            input.mi.dwFlags = flag;
            input.mi.dwExtraInfo = INJECTION_TAG;
            if (SendInput(1, &input, sizeof(INPUT)) == 1)
            {
                return true;
            }
            Logger::warn(L"Failed to inject synthetic button-up; the OS may keep the button held until the next physical click.");
            return false;
        }
    };
}

// The PowerToy name that will be shown in the settings.
const static wchar_t* MODULE_NAME = L"MouseButtonLock";

// Forward declaration so the static hook proc can reach the singleton instance.
class MouseButtonLock;
// Atomic because the static hook proc (on the hook thread) reads it while the ctor/destroy
// (on the runner thread) write it. destroy() still unhooks and joins the hook thread before
// clearing this and deleting the object, so a non-null load in the proc stays valid.
static std::atomic<MouseButtonLock*> g_instance{ nullptr };

// Implement the PowerToy Module Interface and all the required methods.
class MouseButtonLock : public PowertoyModuleIface
{
private:
    // The PowerToy enabled state (the whole module, driven by the runner). Atomic because the
    // runner's telemetry thread can call is_enabled() while enable()/disable() write it.
    std::atomic<bool> m_enabled{ false };

    // Settings. Read on the hook thread, written by set_config on the runner thread, so atomic.
    std::atomic<bool> m_rmbLockEnabled{ true };
    std::atomic<bool> m_mmbLockEnabled{ false };
    std::atomic<bool> m_moveCancelEnabled{ true };
    std::atomic<int> m_holdDurationMs{ DEFAULT_HOLD_DURATION_MS };
    std::atomic<int> m_moveCancelPixels{ DEFAULT_MOVE_CANCEL_PIXELS };

    // The state machine. m_injector must be declared before m_engine (the engine binds a reference
    // to it in its constructor).
    WinInjector m_injector;
    mousebuttonlock::Engine m_engine{ m_injector };

    // Hook thread + lifecycle.
    HHOOK m_mouseHook = nullptr;
    HANDLE m_terminateEvent = nullptr;
    std::thread m_hookThread;
    std::atomic<bool> m_listening{ false };

    void init_settings();
    void parse_settings(PowerToysSettings::PowerToyValues& settings);
    mousebuttonlock::Settings SettingsSnapshot() const;

    void HookThreadMain();
    bool HandleMouseMessage(WPARAM wParam, const MSLLHOOKSTRUCT* data);

    static LRESULT CALLBACK MouseHookProc(int nCode, WPARAM wParam, LPARAM lParam);

public:
    MouseButtonLock()
    {
        LoggerHelpers::init_logger(MODULE_NAME, L"ModuleInterface", LogSettings::mouseButtonLockLoggerName);
        init_settings();
        g_instance.store(this);
    }

    virtual void destroy() override
    {
        // Ensure the hook thread is torn down and any locked button released before deletion.
        disable();
        g_instance.store(nullptr);
        delete this;
    }

    virtual const wchar_t* get_name() override
    {
        return MODULE_NAME;
    }

    virtual const wchar_t* get_key() override
    {
        return MODULE_NAME;
    }

    virtual powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
    {
        return powertoys_gpo::getConfiguredMouseButtonLockEnabledValue();
    }

    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.set_description(IDS_MOUSEBUTTONLOCK_NAME);

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    virtual void call_custom_action(const wchar_t* /*action*/) override {}

    virtual void set_config(const wchar_t* config) override
    {
        try
        {
            PowerToysSettings::PowerToyValues values =
                PowerToysSettings::PowerToyValues::from_json_string(config, get_key());

            parse_settings(values);

            // If a button's lock was just turned off while it was logically held, release it now.
            m_engine.EnforceEnabled(SettingsSnapshot());
        }
        catch (...)
        {
            // catch(...) because the JSON accessors throw winrt::hresult_error, which does not
            // derive from std::exception; a malformed payload must not escape and abort apply.
            Logger::error("Invalid json when trying to parse MouseButtonLock settings json.");
        }
    }

    virtual void enable() override
    {
        m_enabled = true;
        Trace::EnableMouseButtonLock(true);

        if (m_listening)
        {
            return;
        }

        // Clear any stale per-button hold state left over from a previous enable session. A button
        // physically held across a disable/enable cycle never delivers its UP to us (the hook is
        // uninstalled in between), so without this a stale hold could lock spuriously or swallow a
        // later click. Safe to touch this state here: the hook thread is not running yet.
        m_engine.ResetTransient();

        m_terminateEvent = CreateEventW(nullptr, FALSE, FALSE, nullptr);
        if (m_terminateEvent == nullptr)
        {
            // Without the terminate event the hook thread's wait would spin at 100% CPU; don't start it.
            Logger::error(L"Failed to create MouseButtonLock terminate event, error: {}. Hook not started.", GetLastError());
            return;
        }
        m_listening = true;
        m_hookThread = std::thread([this]() { HookThreadMain(); });
    }

    virtual void disable() override
    {
        m_enabled = false;
        Trace::EnableMouseButtonLock(false);

        if (!m_listening)
        {
            return;
        }

        m_listening = false;
        if (m_terminateEvent)
        {
            SetEvent(m_terminateEvent);
        }
        if (m_hookThread.joinable())
        {
            m_hookThread.join();
        }
        if (m_terminateEvent)
        {
            CloseHandle(m_terminateEvent);
            m_terminateEvent = nullptr;
        }
    }

    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    virtual bool is_enabled_by_default() const override
    {
        return false;
    }
};

void MouseButtonLock::init_settings()
{
    try
    {
        PowerToysSettings::PowerToyValues settings =
            PowerToysSettings::PowerToyValues::load_from_settings_file(MouseButtonLock::get_key());
        parse_settings(settings);
    }
    catch (...)
    {
        // catch(...) so winrt::hresult_error from the JSON accessors can't escape the constructor;
        // keep the defaults on any parse error.
        Logger::error("Invalid json when trying to load the MouseButtonLock settings json from file.");
    }
}

void MouseButtonLock::parse_settings(PowerToysSettings::PowerToyValues& settings)
{
    auto settingsObject = settings.get_raw_json();
    if (!settingsObject.GetView().Size() || !settingsObject.HasKey(JSON_KEY_PROPERTIES))
    {
        Logger::info("MouseButtonLock settings are empty; keeping defaults.");
        return;
    }

    auto properties = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES);

    auto readBool = [&](const wchar_t* key, std::atomic<bool>& target) {
        if (!properties.HasKey(key))
        {
            return;
        }
        try
        {
            target.store(properties.GetNamedObject(key).GetNamedBoolean(JSON_KEY_VALUE));
        }
        catch (...)
        {
            Logger::warn(L"Failed to read bool setting; keeping previous value.");
        }
    };

    auto readInt = [&](const wchar_t* key, std::atomic<int>& target, int minValue, int maxValue) {
        if (!properties.HasKey(key))
        {
            return;
        }
        try
        {
            // GetNamedNumber yields a double; clamp into range BEFORE the cast so an out-of-range
            // or non-finite value can't produce an undefined double-to-int conversion.
            double raw = properties.GetNamedObject(key).GetNamedNumber(JSON_KEY_VALUE);
            if (raw < minValue)
            {
                raw = minValue;
            }
            else if (raw > maxValue)
            {
                raw = maxValue;
            }
            target.store(static_cast<int>(raw));
        }
        catch (...)
        {
            Logger::warn(L"Failed to read int setting; keeping previous value.");
        }
    };

    readBool(JSON_KEY_RMB_LOCK_ENABLED, m_rmbLockEnabled);
    readBool(JSON_KEY_MMB_LOCK_ENABLED, m_mmbLockEnabled);
    readBool(JSON_KEY_MOVE_CANCEL_ENABLED, m_moveCancelEnabled);
    readInt(JSON_KEY_HOLD_DURATION_MS, m_holdDurationMs, MIN_HOLD_DURATION_MS, MAX_HOLD_DURATION_MS);
    readInt(JSON_KEY_MOVE_CANCEL_PIXELS, m_moveCancelPixels, 0, MAX_MOVE_CANCEL_PIXELS);
}

mousebuttonlock::Settings MouseButtonLock::SettingsSnapshot() const
{
    mousebuttonlock::Settings s;
    s.rmbEnabled = m_rmbLockEnabled.load();
    s.mmbEnabled = m_mmbLockEnabled.load();
    s.moveCancelEnabled = m_moveCancelEnabled.load();
    s.holdDurationMs = m_holdDurationMs.load();
    s.moveCancelPixels = m_moveCancelPixels.load();
    return s;
}

void MouseButtonLock::HookThreadMain()
{
    // WH_MOUSE_LL callbacks are delivered to the thread that installed the hook, so this
    // thread needs a message queue and must pump messages while the hook is active.
    MSG msg;
    PeekMessage(&msg, nullptr, WM_USER, WM_USER, PM_NOREMOVE);

    m_mouseHook = SetWindowsHookEx(WH_MOUSE_LL, MouseHookProc, GetModuleHandle(nullptr), 0);
    if (!m_mouseHook)
    {
        Logger::error(L"Failed to install MouseButtonLock mouse hook, error: {}", GetLastError());
    }

    HANDLE handles[1] = { m_terminateEvent };
    while (m_listening)
    {
        DWORD res = MsgWaitForMultipleObjects(1, handles, FALSE, INFINITE, QS_ALLINPUT);
        if (!m_listening || res == WAIT_OBJECT_0)
        {
            break;
        }
        if (res == WAIT_FAILED)
        {
            // Defensive: shouldn't happen now that the terminate event is validated before start.
            // Bail rather than spin if the wait ever fails.
            Logger::error(L"MouseButtonLock wait failed, error: {}. Stopping hook thread.", GetLastError());
            break;
        }

        while (PeekMessage(&msg, nullptr, 0, 0, PM_REMOVE))
        {
            TranslateMessage(&msg);
            DispatchMessage(&msg);
        }
    }

    if (m_mouseHook)
    {
        UnhookWindowsHookEx(m_mouseHook);
        m_mouseHook = nullptr;
    }

    // Crash/shutdown safety: never leave a button stranded in the locked state.
    m_engine.ReleaseAll();
}

LRESULT CALLBACK MouseButtonLock::MouseHookProc(int nCode, WPARAM wParam, LPARAM lParam)
{
    // Load the singleton once into a local; see the g_instance declaration for why this is safe.
    MouseButtonLock* instance = g_instance.load();
    if (nCode == HC_ACTION && instance)
    {
        auto* data = reinterpret_cast<MSLLHOOKSTRUCT*>(lParam);
        if (instance->HandleMouseMessage(wParam, data))
        {
            return 1; // Suppress: downstream apps never see this event.
        }
    }
    return CallNextHookEx(nullptr, nCode, wParam, lParam);
}

bool MouseButtonLock::HandleMouseMessage(WPARAM wParam, const MSLLHOOKSTRUCT* data)
{
    // Ignore anything we injected ourselves so we don't recurse on our own events.
    if (data->dwExtraInfo == INJECTION_TAG)
    {
        return false;
    }

    const mousebuttonlock::Settings snapshot = SettingsSnapshot();
    const uint64_t tick = GetTickCount64();
    const mousebuttonlock::PointL pt{ data->pt.x, data->pt.y };

    switch (wParam)
    {
    case WM_RBUTTONDOWN:
        return m_engine.OnButtonDown(mousebuttonlock::MouseButton::Right, tick, pt, snapshot);
    case WM_RBUTTONUP:
        return m_engine.OnButtonUp(mousebuttonlock::MouseButton::Right, tick, snapshot);
    case WM_MBUTTONDOWN:
        return m_engine.OnButtonDown(mousebuttonlock::MouseButton::Middle, tick, pt, snapshot);
    case WM_MBUTTONUP:
        return m_engine.OnButtonUp(mousebuttonlock::MouseButton::Middle, tick, snapshot);
    case WM_MOUSEMOVE:
        m_engine.OnMove(tick, pt, snapshot);
        return false;
    default:
        return false;
    }
}

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new MouseButtonLock();
}
