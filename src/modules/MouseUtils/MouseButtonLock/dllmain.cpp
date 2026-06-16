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
// Utilities (see CursorWrap). The button-state machine is ported from the standalone
// windows-right-click-lock reference app and generalized to cover both RMB and MMB.

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
}

// The PowerToy name that will be shown in the settings.
const static wchar_t* MODULE_NAME = L"MouseButtonLock";
// Add a description that will be shown in the module settings page.
const static wchar_t* MODULE_DESC = L"Hold the right or middle mouse button to lock it, like ClickLock";

// Forward declaration so the static hook proc can reach the singleton instance.
class MouseButtonLock;
static MouseButtonLock* g_instance = nullptr;

// Implement the PowerToy Module Interface and all the required methods.
class MouseButtonLock : public PowertoyModuleIface
{
private:
    // Per-button lock state. The transient fields are owned by the hook thread; only
    // `locked` is touched from other threads (set_config / disable), hence the atomic.
    struct ButtonLockState
    {
        DWORD upFlag = 0; // MOUSEEVENTF_RIGHTUP / MOUSEEVENTF_MIDDLEUP
        bool physicalDown = false;
        bool moveCancelled = false;
        bool swallowNextRealUp = false;
        ULONGLONG downTick = 0;
        POINT downPos{};
        std::atomic<bool> locked{ false };
    };

    // The PowerToy enabled state (the whole module, driven by the runner).
    bool m_enabled = false;

    // Settings. Read on the hook thread, written by set_config on the runner thread, so atomic.
    std::atomic<bool> m_rmbLockEnabled{ true };
    std::atomic<bool> m_mmbLockEnabled{ false };
    std::atomic<bool> m_moveCancelEnabled{ true };
    std::atomic<int> m_holdDurationMs{ DEFAULT_HOLD_DURATION_MS };
    std::atomic<int> m_moveCancelPixels{ DEFAULT_MOVE_CANCEL_PIXELS };

    ButtonLockState m_right;
    ButtonLockState m_middle;

    // Hook thread + lifecycle.
    HHOOK m_mouseHook = nullptr;
    HANDLE m_terminateEvent = nullptr;
    std::thread m_hookThread;
    std::atomic<bool> m_listening{ false };

    void init_settings();
    void parse_settings(PowerToysSettings::PowerToyValues& settings);

    void HookThreadMain();
    bool HandleMouseMessage(WPARAM wParam, const MSLLHOOKSTRUCT* data);
    bool HandleButtonDown(ButtonLockState& st, const std::atomic<bool>& enabled, const MSLLHOOKSTRUCT* data);
    bool HandleButtonUp(ButtonLockState& st, const std::atomic<bool>& enabled);
    void HandleMove(const MSLLHOOKSTRUCT* data);
    static void CheckMoveCancel(ButtonLockState& st, ULONGLONG now, int holdMs, int pixels, POINT pt);

    static bool InjectButtonUp(DWORD upFlag);
    static void ReleaseButton(ButtonLockState& st);
    static void ResetButtonTransient(ButtonLockState& st);
    void ReleaseAllLocked();
    void EnforceEnabledState();

    static LRESULT CALLBACK MouseHookProc(int nCode, WPARAM wParam, LPARAM lParam);

public:
    MouseButtonLock()
    {
        LoggerHelpers::init_logger(MODULE_NAME, L"ModuleInterface", LogSettings::mouseButtonLockLoggerName);
        m_right.upFlag = MOUSEEVENTF_RIGHTUP;
        m_middle.upFlag = MOUSEEVENTF_MIDDLEUP;
        init_settings();
        g_instance = this;
    }

    virtual void destroy() override
    {
        // Ensure the hook thread is torn down and any locked button released before deletion.
        disable();
        g_instance = nullptr;
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
            EnforceEnabledState();
        }
        catch (std::exception&)
        {
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
        // uninstalled in between), so without this a stale downTick would make the next release lock
        // spuriously, and a stale swallowNextRealUp would swallow a later legitimate click. Safe to
        // touch these fields here: the hook thread is not running yet.
        ResetButtonTransient(m_right);
        ResetButtonTransient(m_middle);

        m_terminateEvent = CreateEventW(nullptr, FALSE, FALSE, nullptr);
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
    catch (std::exception&)
    {
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

    auto readInt = [&](const wchar_t* key, std::atomic<int>& target, int minValue) {
        if (!properties.HasKey(key))
        {
            return;
        }
        try
        {
            int value = static_cast<int>(properties.GetNamedObject(key).GetNamedNumber(JSON_KEY_VALUE));
            if (value >= minValue)
            {
                target.store(value);
            }
        }
        catch (...)
        {
            Logger::warn(L"Failed to read int setting; keeping previous value.");
        }
    };

    readBool(JSON_KEY_RMB_LOCK_ENABLED, m_rmbLockEnabled);
    readBool(JSON_KEY_MMB_LOCK_ENABLED, m_mmbLockEnabled);
    readBool(JSON_KEY_MOVE_CANCEL_ENABLED, m_moveCancelEnabled);
    readInt(JSON_KEY_HOLD_DURATION_MS, m_holdDurationMs, 0);
    readInt(JSON_KEY_MOVE_CANCEL_PIXELS, m_moveCancelPixels, 0);
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
    ReleaseAllLocked();
}

LRESULT CALLBACK MouseButtonLock::MouseHookProc(int nCode, WPARAM wParam, LPARAM lParam)
{
    if (nCode == HC_ACTION && g_instance)
    {
        auto* data = reinterpret_cast<MSLLHOOKSTRUCT*>(lParam);
        if (g_instance->HandleMouseMessage(wParam, data))
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

    switch (wParam)
    {
    case WM_RBUTTONDOWN:
        return HandleButtonDown(m_right, m_rmbLockEnabled, data);
    case WM_RBUTTONUP:
        return HandleButtonUp(m_right, m_rmbLockEnabled);
    case WM_MBUTTONDOWN:
        return HandleButtonDown(m_middle, m_mmbLockEnabled, data);
    case WM_MBUTTONUP:
        return HandleButtonUp(m_middle, m_mmbLockEnabled);
    case WM_MOUSEMOVE:
        HandleMove(data);
        return false;
    default:
        return false;
    }
}

bool MouseButtonLock::HandleButtonDown(ButtonLockState& st, const std::atomic<bool>& enabled, const MSLLHOOKSTRUCT* data)
{
    if (st.locked.load())
    {
        // Tap-to-release: try to send the synthetic UP first. Only suppress the physical DOWN
        // and arm the UP-swallow flag if the OS accepted our release. If SendInput fails, let the
        // physical events through so the OS has a chance to correct its belief about the button.
        if (InjectButtonUp(st.upFlag))
        {
            st.locked.store(false);
            st.swallowNextRealUp = true;
            return true;
        }
        return false;
    }

    if (!enabled.load())
    {
        return false;
    }

    // Begin a fresh hold.
    st.physicalDown = true;
    st.moveCancelled = false;
    st.downTick = GetTickCount64();
    st.downPos = data->pt;
    return false; // Never suppress the DOWN.
}

bool MouseButtonLock::HandleButtonUp(ButtonLockState& st, const std::atomic<bool>& enabled)
{
    if (st.swallowNextRealUp)
    {
        st.swallowNextRealUp = false;
        return true; // Swallow the physical UP paired with a release tap.
    }

    if (!st.physicalDown)
    {
        return false;
    }
    st.physicalDown = false;

    if (!enabled.load())
    {
        return false;
    }

    int holdMs = m_holdDurationMs.load();
    if (holdMs < 0)
    {
        holdMs = 0;
    }

    const ULONGLONG elapsed = GetTickCount64() - st.downTick;
    if (!st.moveCancelled && elapsed >= static_cast<ULONGLONG>(holdMs))
    {
        // Held past the threshold: suppress the UP so the OS keeps believing the button is held.
        st.locked.store(true);
        return true;
    }

    return false; // Released before the threshold: regular click.
}

void MouseButtonLock::HandleMove(const MSLLHOOKSTRUCT* data)
{
    if (!m_moveCancelEnabled.load())
    {
        return;
    }

    const ULONGLONG now = GetTickCount64();
    int holdMs = m_holdDurationMs.load();
    if (holdMs < 0)
    {
        holdMs = 0;
    }
    int pixels = m_moveCancelPixels.load();
    if (pixels < 0)
    {
        pixels = 0;
    }

    CheckMoveCancel(m_right, now, holdMs, pixels, data->pt);
    CheckMoveCancel(m_middle, now, holdMs, pixels, data->pt);
}

void MouseButtonLock::CheckMoveCancel(ButtonLockState& st, ULONGLONG now, int holdMs, int pixels, POINT pt)
{
    // Only relevant during the arming window: button physically held, not yet locked,
    // not already cancelled. Once the threshold has elapsed the lock is armed and motion
    // no longer prevents it (so a held-button camera drag still locks).
    if (!st.physicalDown || st.locked.load() || st.moveCancelled)
    {
        return;
    }
    if (now - st.downTick >= static_cast<ULONGLONG>(holdMs))
    {
        return;
    }

    const long long dx = pt.x - st.downPos.x;
    const long long dy = pt.y - st.downPos.y;
    if (dx * dx + dy * dy > static_cast<long long>(pixels) * pixels)
    {
        st.moveCancelled = true;
    }
}

bool MouseButtonLock::InjectButtonUp(DWORD upFlag)
{
    INPUT input{};
    input.type = INPUT_MOUSE;
    input.mi.dwFlags = upFlag;
    input.mi.dwExtraInfo = INJECTION_TAG;
    return SendInput(1, &input, sizeof(INPUT)) == 1;
}

void MouseButtonLock::ReleaseButton(ButtonLockState& st)
{
    // exchange() ensures only one caller injects the release if disable() and the hook race.
    if (st.locked.exchange(false))
    {
        InjectButtonUp(st.upFlag);
    }
}

void MouseButtonLock::ReleaseAllLocked()
{
    ReleaseButton(m_right);
    ReleaseButton(m_middle);
}

void MouseButtonLock::ResetButtonTransient(ButtonLockState& st)
{
    st.physicalDown = false;
    st.moveCancelled = false;
    st.swallowNextRealUp = false;
    st.downTick = 0;
}

void MouseButtonLock::EnforceEnabledState()
{
    if (!m_rmbLockEnabled.load())
    {
        ReleaseButton(m_right);
    }
    if (!m_mmbLockEnabled.load())
    {
        ReleaseButton(m_middle);
    }
}

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new MouseButtonLock();
}
