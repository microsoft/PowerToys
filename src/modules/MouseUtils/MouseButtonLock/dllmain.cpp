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

#include <array>
#include <atomic>
#include <cmath>
#include <mutex>
#include <thread>

// Mouse Button Lock
//
// A ClickLock equivalent for the left, right, and middle mouse buttons. Hold a button past a
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
    const wchar_t JSON_KEY_LMB_LOCK_ENABLED[] = L"lmb_lock_enabled";
    const wchar_t JSON_KEY_RMB_LOCK_ENABLED[] = L"rmb_lock_enabled";
    const wchar_t JSON_KEY_MMB_LOCK_ENABLED[] = L"mmb_lock_enabled";
    const wchar_t JSON_KEY_HOLD_DURATION_MS[] = L"hold_duration_ms";
    const wchar_t JSON_KEY_MOVE_CANCEL_PIXELS[] = L"move_cancel_pixels";

    // dwExtraInfo tag stamped on every event we inject via SendInput, so the hook ignores
    // our own synthetic events and we don't recurse. Magic value 'WINM' (carried over from the
    // standalone reference app); distinct from the centralized keyboard hook's 0x110 flag.
    constexpr ULONG_PTR INJECTION_TAG = 0x57494E4D;

    // Thread message the hook thread posts to itself to perform a deferred SendInput. Calling
    // SendInput synchronously from inside the low-level hook callback lets the very event we are
    // trying to suppress leak to applications (it reads as a stray click that collapses a text
    // selection). Posting the injection back to the hook thread's own message loop runs it AFTER the
    // callback has returned 1 and the triggering event is fully suppressed. wParam packs the button
    // in bits 1+ and the dismiss-context-menu intent in bit 0.
    constexpr UINT WM_MOUSEBUTTONLOCK_INJECT = WM_APP + 1;

    // How long after a deferred SendInput reported success the injector waits before checking that the
    // button really came up (see WinInjector::VerifyDeferred). Long enough for the raw input thread
    // to have applied the injected event, short enough that a stuck button is repaired before the
    // user notices. A physical re-press inside this window is told apart by the engine (physicalDown).
    constexpr UINT INJECT_VERIFY_DELAY_MS = 50;

    // Default values mirror the C# MouseButtonLockProperties defaults.
    constexpr int DEFAULT_HOLD_DURATION_MS = 1200;
    constexpr int DEFAULT_MOVE_CANCEL_PIXELS = 5;

    // Accepted ranges when reading from settings.json. The Settings UI already constrains these,
    // but a hand-edited file could carry out-of-range or non-finite values, so clamp on read.
    // The 200 ms floor matches Windows' built-in ClickLock minimum and keeps a hold clear of the
    // ordinary-click duration band (~100-350 ms) so a stray click can't latch; a huge hold would
    // make locking unreachable, hence the ceiling.
    constexpr int MIN_HOLD_DURATION_MS = 200;
    constexpr int MAX_HOLD_DURATION_MS = 60000;
    constexpr int MAX_MOVE_CANCEL_PIXELS = 10000;

    // Production injector: a tagged SendInput. Lives behind the engine's IButtonUpInjector so the
    // state machine can be unit tested without Win32.
    class WinInjector : public mousebuttonlock::IButtonUpInjector
    {
    public:
        WinInjector()
        {
            s_instance.store(this);
        }

        ~WinInjector() override
        {
            s_instance.store(nullptr);
        }

        // Called on the hook thread once it starts, so deferred injections post back to that thread.
        void SetTargetThread(DWORD threadId)
        {
            m_threadId.store(threadId);
        }

        // The module only ever injects button-UPs: a lock is held by suppressing the physical up (no
        // down injection), and every release injects the matching up.
        bool InjectUp(mousebuttonlock::MouseButton button, bool dismissContextMenu) override
        {
            return Post(button, dismissContextMenu);
        }

        // The engine registers itself here (and clears it in its destructor) to hear about a posted
        // injection that SendInput later rejects.
        void SetDeferredFailureSink(mousebuttonlock::IDeferredFailureSink* sink) override
        {
            m_sink.store(sink);
        }

        // Runs the actual SendInput. Called from the hook thread's message loop when it drains a
        // WM_MOUSEBUTTONLOCK_INJECT it posted to itself (see HookThreadMain), i.e. after the triggering hook
        // callback has returned and suppressed the physical event. Post() already returned true for
        // this injection, so a failure here must be reported back, or the engine would believe a
        // still-held button was released. Two failures are told apart:
        //  - SendInput returns 0: report immediately.
        //  - SendInput returns success but the event never takes effect. Windows does this silently
        //    when input is blocked (BlockInput) or a higher-integrity window owns the foreground (UIPI);
        //    neither the return value nor GetLastError says so. Schedule a check of the button's
        //    physical state a little later (VerifyDeferred) and report if it is still down.
        // The dismiss intent is handed back unchanged so the engine sees the release as it requested it.
        void PerformDeferred(WPARAM packed)
        {
            const auto button = static_cast<mousebuttonlock::MouseButton>(packed >> 1);
            const bool dismissContextMenu = (packed & 1) != 0;
            if (!InjectUpNow(button, dismissContextMenu))
            {
                ReportFailure(button, dismissContextMenu);
                return;
            }
            ScheduleVerify(button, dismissContextMenu);
        }

    private:
        // Per-button verification pending for an injected up. Only touched on the hook thread (both
        // PerformDeferred and the timer callback run there), so no synchronization is needed.
        struct PendingVerify
        {
            UINT_PTR timerId = 0;
            bool dismissContextMenu = false;
        };

        static size_t Index(mousebuttonlock::MouseButton button)
        {
            switch (button)
            {
            case mousebuttonlock::MouseButton::Left:
                return 0;
            case mousebuttonlock::MouseButton::Middle:
                return 2;
            case mousebuttonlock::MouseButton::Right:
            default:
                return 1;
            }
        }

        static mousebuttonlock::MouseButton ButtonAt(size_t index)
        {
            switch (index)
            {
            case 0:
                return mousebuttonlock::MouseButton::Left;
            case 2:
                return mousebuttonlock::MouseButton::Middle;
            default:
                return mousebuttonlock::MouseButton::Right;
            }
        }

        void ReportFailure(mousebuttonlock::MouseButton button, bool dismissContextMenu)
        {
            if (auto* sink = m_sink.load())
            {
                sink->OnDeferredInjectUpFailed(button, dismissContextMenu);
            }
        }

        // Arm a thread timer (no window: WM_TIMER lands in the hook thread's queue and DispatchMessage
        // calls VerifyTimerProc). A second release of the same button before its check ran re-arms it.
        void ScheduleVerify(mousebuttonlock::MouseButton button, bool dismissContextMenu)
        {
            if (m_sink.load() == nullptr)
            {
                return;
            }
            PendingVerify& pending = m_pending[Index(button)];
            if (pending.timerId != 0)
            {
                KillTimer(nullptr, pending.timerId);
            }
            pending.dismissContextMenu = dismissContextMenu;
            pending.timerId = SetTimer(nullptr, 0, INJECT_VERIFY_DELAY_MS, VerifyTimerProc);
            if (pending.timerId == 0)
            {
                Logger::warn(L"Failed to arm the synthetic button-up verification timer, error: {}", GetLastError());
            }
        }

        static void CALLBACK VerifyTimerProc(HWND, UINT, UINT_PTR timerId, DWORD)
        {
            KillTimer(nullptr, timerId);
            if (auto* self = s_instance.load())
            {
                self->VerifyDeferred(timerId);
            }
        }

        // The injected up was accepted INJECT_VERIFY_DELAY_MS ago. If the OS still reports the button
        // down, the event was dropped: tell the engine so it can repair its state. (A physical re-press
        // in the meantime also reads as down; the engine recognizes that case and ignores the report.)
        void VerifyDeferred(UINT_PTR timerId)
        {
            for (size_t i = 0; i < m_pending.size(); ++i)
            {
                PendingVerify& pending = m_pending[i];
                if (pending.timerId != timerId)
                {
                    continue;
                }
                pending.timerId = 0;
                const auto button = ButtonAt(i);
                if (IsPhysicallyDown(button))
                {
                    Logger::warn(L"Synthetic button-up was accepted but the button is still down; the injection was dropped (blocked input or a higher-integrity foreground window). Repairing state.");
                    ReportFailure(button, pending.dismissContextMenu);
                }
                return;
            }
        }

        // GetAsyncKeyState reports the PHYSICAL buttons, while the hook, SendInput and the engine all
        // speak in logical buttons, so undo a primary/secondary swap before asking.
        static bool IsPhysicallyDown(mousebuttonlock::MouseButton button)
        {
            int vk = VK_RBUTTON;
            switch (button)
            {
            case mousebuttonlock::MouseButton::Left:
                vk = VK_LBUTTON;
                break;
            case mousebuttonlock::MouseButton::Middle:
                vk = VK_MBUTTON;
                break;
            case mousebuttonlock::MouseButton::Right:
            default:
                vk = VK_RBUTTON;
                break;
            }
            if (vk != VK_MBUTTON && GetSystemMetrics(SM_SWAPBUTTON) != 0)
            {
                vk = (vk == VK_LBUTTON) ? VK_RBUTTON : VK_LBUTTON;
            }
            return (GetAsyncKeyState(vk) & 0x8000) != 0;
        }

        // Defer the SendInput to the hook thread's message loop. If the thread id isn't known yet
        // (should not happen once the hook is running) fall back to an inline inject so a release is
        // never silently dropped. wParam layout matches WM_MOUSEBUTTONLOCK_INJECT's doc comment: button in
        // bits 1+, dismiss-context-menu intent in bit 0.
        bool Post(mousebuttonlock::MouseButton button, bool dismissContextMenu)
        {
            const WPARAM packed = (static_cast<WPARAM>(button) << 1) | (dismissContextMenu ? 1 : 0);
            const DWORD threadId = m_threadId.load();
            if (threadId != 0 && PostThreadMessageW(threadId, WM_MOUSEBUTTONLOCK_INJECT, packed, 0))
            {
                return true;
            }
            return InjectUpNow(button, dismissContextMenu);
        }

        static bool InjectUpNow(mousebuttonlock::MouseButton button, bool dismissContextMenu)
        {
            DWORD flag = MOUSEEVENTF_RIGHTUP;
            switch (button)
            {
            case mousebuttonlock::MouseButton::Left:
                flag = MOUSEEVENTF_LEFTUP;
                break;
            case mousebuttonlock::MouseButton::Middle:
                flag = MOUSEEVENTF_MIDDLEUP;
                break;
            case mousebuttonlock::MouseButton::Right:
            default:
                flag = MOUSEEVENTF_RIGHTUP;
                break;
            }
            INPUT input{};
            input.type = INPUT_MOUSE;
            input.mi.dwFlags = flag;
            input.mi.dwExtraInfo = INJECTION_TAG;
            if (SendInput(1, &input, sizeof(INPUT)) != 1)
            {
                Logger::warn(L"Failed to inject synthetic button-up event.");
                return false;
            }
            // Releasing a right-button lock emits a right-button-up, which apps treat as a right-click
            // and answer with a context menu. When the engine says this release will surface one
            // (dismissContextMenu: a same-button release tap or a lifecycle/settings release, where no
            // other button is down), immediately queue an Esc to dismiss it. Esc lands right behind
            // the up in the input queue, so the menu's modal loop consumes it as soon as it opens.
            // This is what makes hands-free right-drag usable; the trade-off is that a genuine
            // right-drag-drop menu (e.g. Explorer's copy/move) is also dismissed. A cross-button
            // release is chorded (another button is physically down), so no menu opens and the engine
            // passes false: sending Esc then would leak the keystroke to the foreground app and could
            // close a dialog or cancel an operation.
            if (dismissContextMenu && button == mousebuttonlock::MouseButton::Right)
            {
                InjectEscape();
            }
            return true;
        }

        // Tap Esc to dismiss a context menu opened by a right-lock release. Tagged like our mouse
        // injections; the module hooks only the mouse, so this never feeds back into our own hook.
        static void InjectEscape()
        {
            INPUT keys[2]{};
            keys[0].type = INPUT_KEYBOARD;
            keys[0].ki.wVk = VK_ESCAPE;
            keys[0].ki.dwExtraInfo = INJECTION_TAG;
            keys[1].type = INPUT_KEYBOARD;
            keys[1].ki.wVk = VK_ESCAPE;
            keys[1].ki.dwFlags = KEYEVENTF_KEYUP;
            keys[1].ki.dwExtraInfo = INJECTION_TAG;
            SendInput(2, keys, sizeof(INPUT));
        }

        std::atomic<DWORD> m_threadId{ 0 };
        std::atomic<mousebuttonlock::IDeferredFailureSink*> m_sink{ nullptr };
        std::array<PendingVerify, 3> m_pending{};

        // The timer callback has no context pointer; the module has exactly one injector.
        static inline std::atomic<WinInjector*> s_instance{ nullptr };
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
    // m_settingsMutex additionally makes a settings apply (parse + EnforceEnabled) atomic with
    // respect to a hook event (snapshot + engine call): without it a button could be disabled and
    // its held lock released, and then a hook callback that had already taken the older snapshot
    // could latch the same button again afterwards.
    std::mutex m_settingsMutex;
    std::atomic<bool> m_lmbLockEnabled{ false };
    std::atomic<bool> m_rmbLockEnabled{ true };
    std::atomic<bool> m_mmbLockEnabled{ false };
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

            // Hold the hook off while the new values land and are enforced; see m_settingsMutex.
            std::lock_guard<std::mutex> lock(m_settingsMutex);
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
            // GetNamedNumber yields a double. NaN compares false against both bounds, so it would
            // slip past the range checks below and make static_cast<int> undefined; reject any
            // non-finite value outright and keep the previous value. Finite out-of-range values are
            // clamped BEFORE the cast so the conversion is always defined.
            double raw = properties.GetNamedObject(key).GetNamedNumber(JSON_KEY_VALUE);
            if (!std::isfinite(raw))
            {
                Logger::warn(L"Ignoring non-finite int setting; keeping previous value.");
                return;
            }
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

    readBool(JSON_KEY_LMB_LOCK_ENABLED, m_lmbLockEnabled);
    readBool(JSON_KEY_RMB_LOCK_ENABLED, m_rmbLockEnabled);
    readBool(JSON_KEY_MMB_LOCK_ENABLED, m_mmbLockEnabled);
    readInt(JSON_KEY_HOLD_DURATION_MS, m_holdDurationMs, MIN_HOLD_DURATION_MS, MAX_HOLD_DURATION_MS);
    readInt(JSON_KEY_MOVE_CANCEL_PIXELS, m_moveCancelPixels, 0, MAX_MOVE_CANCEL_PIXELS);
}

mousebuttonlock::Settings MouseButtonLock::SettingsSnapshot() const
{
    mousebuttonlock::Settings s;
    s.lmbEnabled = m_lmbLockEnabled.load();
    s.rmbEnabled = m_rmbLockEnabled.load();
    s.mmbEnabled = m_mmbLockEnabled.load();
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

    // Route deferred injections (see WinInjector::Post / WM_MOUSEBUTTONLOCK_INJECT) to this thread's loop.
    m_injector.SetTargetThread(GetCurrentThreadId());

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
            // Deferred injections are thread messages (no target HWND), so handle them here rather
            // than via DispatchMessage. Running them now, after the triggering hook callback has
            // returned, keeps the suppressed physical event from leaking to applications.
            if (msg.message == WM_MOUSEBUTTONLOCK_INJECT)
            {
                m_injector.PerformDeferred(msg.wParam);
                continue;
            }
            TranslateMessage(&msg);
            DispatchMessage(&msg);
        }
    }

    // Drain any injection still queued (e.g. a release tap right before shutdown) so we don't leave
    // a button held, then unhook.
    while (PeekMessage(&msg, nullptr, WM_MOUSEBUTTONLOCK_INJECT, WM_MOUSEBUTTONLOCK_INJECT, PM_REMOVE))
    {
        m_injector.PerformDeferred(msg.wParam);
    }

    if (m_mouseHook)
    {
        UnhookWindowsHookEx(m_mouseHook);
        m_mouseHook = nullptr;
    }

    // The message loop is gone, so deferring would strand the injection. We are no longer inside a
    // hook callback here, so a direct SendInput is safe: clear the target thread to make ReleaseAll's
    // injections run inline, guaranteeing no button is left stranded in the locked state.
    m_injector.SetTargetThread(0);
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

    // Taken for the snapshot and the engine call together, so a concurrent set_config can't slip in
    // between them; see m_settingsMutex. set_config holds it only briefly (no I/O), well inside the
    // low-level hook timeout.
    std::lock_guard<std::mutex> lock(m_settingsMutex);
    const mousebuttonlock::Settings snapshot = SettingsSnapshot();
    const uint64_t tick = GetTickCount64();
    const mousebuttonlock::PointL pt{ data->pt.x, data->pt.y };

    switch (wParam)
    {
    case WM_LBUTTONDOWN:
        return m_engine.OnButtonDown(mousebuttonlock::MouseButton::Left, tick, pt, snapshot);
    case WM_LBUTTONUP:
        return m_engine.OnButtonUp(mousebuttonlock::MouseButton::Left, tick, snapshot);
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
