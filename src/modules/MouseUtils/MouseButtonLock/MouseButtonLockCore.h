// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <atomic>
#include <cstdint>
#include <mutex>

// The per-button ClickLock state machine, deliberately decoupled from Win32 so it can be unit
// tested. The caller (the module's low-level mouse hook) feeds it events with a monotonic
// millisecond tick and the cursor position plus a settings snapshot, and acts on the returned
// decisions. No Win32 calls live here: synthetic button-up injection is behind IButtonUpInjector,
// and the clock is the caller-supplied tick, so tests can drive both deterministically.
namespace mousebuttonlock
{
    enum class MouseButton
    {
        Left,
        Right,
        Middle,
    };

    // A plain point so the core does not need <windows.h>.
    struct PointL
    {
        long x = 0;
        long y = 0;
    };

    struct Settings
    {
        // The left (primary) button is off by default: it is the main interaction button and
        // Windows already ships ClickLock for it, so locking it is strictly opt-in.
        bool lmbEnabled = false;
        bool rmbEnabled = true;
        bool mmbEnabled = false;
        // Matches Windows' built-in ClickLock default (1200 ms). Every field is overwritten from
        // settings in production (SettingsSnapshot), so this default only surfaces in tests.
        int holdDurationMs = 1200;
        // The dead-zone (in pixels) that separates hand jitter from a deliberate drag: any motion
        // beyond it during the hold marks the gesture as a drag (text selection, window/file drag)
        // and cancels the pending lock, so the button-up passes through normally.
        int moveCancelPixels = 5;
    };

    // Receives the outcome of an InjectUp that the injector accepted but performed later (see
    // IButtonUpInjector). The engine implements this to repair its state when the deferred SendInput
    // is rejected after the engine has already committed to the release.
    struct IDeferredFailureSink
    {
        virtual ~IDeferredFailureSink() = default;
        virtual void OnDeferredInjectUpFailed(MouseButton button, bool dismissContextMenu) = 0;
    };

    // Abstraction over the synthetic button-up injection (SendInput in production, a recording fake in
    // tests). Returns true if the injection was accepted. A lock is held by suppressing the physical
    // up, so the only injection the engine needs is the up that releases a lock.
    //
    // An injector may defer the actual SendInput (the production one posts it back to the hook
    // thread's message loop, see WinInjector). Then "accepted" only means "queued": if the deferred
    // SendInput later fails, or the injector later observes that the up never took effect (Windows
    // drops an injection silently when input is blocked or a higher-integrity window owns the
    // foreground), the injector must report it through the registered IDeferredFailureSink with the
    // same button and dismissContextMenu it was given, so the engine can undo a release it already
    // committed to. Synchronous injectors never need the sink; the default is a no-op.
    //
    // dismissContextMenu tells the injector whether this release will surface a context menu that
    // should be dismissed on the user's behalf (see WinInjector::InjectEscape). It is true for a
    // same-button release tap and for lifecycle/settings releases (no other button is down, so a
    // right-button up opens a menu), and false for a cross-button release, where the injected up is
    // chorded with the newly pressed button: no menu opens, and a dismissal keystroke would leak to
    // whatever has focus.
    struct IButtonUpInjector
    {
        virtual ~IButtonUpInjector() = default;
        virtual bool InjectUp(MouseButton button, bool dismissContextMenu) = 0;
        virtual void SetDeferredFailureSink(IDeferredFailureSink* /*sink*/) {}
    };

    // Every public operation takes m_mutex, so the engine can be driven from the hook thread
    // (button events, deferred-failure reports) and the runner thread (settings enforcement,
    // enable/disable) without a stale event interleaving with a settings change. The injector is
    // called with the mutex held; it must not call back into the engine synchronously (the deferred
    // failure report arrives later from the injector's own message loop).
    class Engine : private IDeferredFailureSink
    {
    public:
        explicit Engine(IButtonUpInjector& injector) :
            m_injector(injector)
        {
            m_injector.SetDeferredFailureSink(this);
        }

        ~Engine() override
        {
            // The injector may outlive the engine (or be destroyed after it, as a member declared
            // earlier); either way a late report must not reach a dead engine.
            m_injector.SetDeferredFailureSink(nullptr);
        }

        Engine(const Engine&) = delete;
        Engine& operator=(const Engine&) = delete;

        // Handle a physical button-down. Returns true if the DOWN should be suppressed.
        //
        // A press of ANY button ends every active lock, so a locked button never leaves the mouse
        // stuck: you can free it with a normal click of any button rather than only a tap of the same
        // one. The pressed button's own lock is a tap-to-release (its down is suppressed and its paired
        // up swallowed); a different pressed button just releases the held button and then clicks
        // normally. Because that release happens while this button is physically down, the injected up
        // is chorded, so releasing a right/middle lock this way fires no context menu.
        // The settings are not consulted here (whether the button is enabled is decided by OnButtonUp,
        // which is what locks); the parameter stays so every event carries the same snapshot.
        bool OnButtonDown(MouseButton button, uint64_t tick, PointL pt, const Settings& /*s*/)
        {
            std::lock_guard<std::mutex> lock(m_mutex);
            ButtonState& st = State(button);

            // Tap-to-release the pressed button's own lock. exchange() claims the lock atomically so a
            // concurrent release (settings change / shutdown) can't double-act. On successful injection
            // suppress the DOWN and swallow its paired UP; on failure drop the lock and let the physical
            // events through so the OS can resolve the state. This DOWN is suppressed, so the injected
            // up is not chorded and a right-button release will surface a context menu: ask the
            // injector to dismiss it.
            if (st.locked.exchange(false))
            {
                if (m_injector.InjectUp(button, /*dismissContextMenu=*/true))
                {
                    st.swallowNextRealUp = true;
                    ReleaseAllExcept(button); // also free any other held button
                    return true;
                }
                ReleaseAllExcept(button);
                return false;
            }

            // A different button was pressed: release any held button, then let this press proceed.
            ReleaseAllExcept(button);

            // Track the press even for a disabled button (OnButtonUp re-checks Enabled before locking):
            // a deferred-failure report that arrives mid-press must be able to tell that the held OS
            // state is explained by this press, see OnDeferredInjectUpFailed.
            st.physicalDown = true;
            st.moveCancelled = false;
            st.downTick = tick;
            st.downPos = pt;
            return false;
        }

        // Handle a physical button-up. Returns true if the UP should be suppressed. Two paths suppress:
        // this UP is the swallowed pair of a release tap, or the hold has reached the threshold and the
        // button locks now. Locking suppresses the physical up so the button stays held without the
        // original click ever completing; nothing is injected on lock (the held state IS the suppressed
        // up, and the later clean release is a single injected up).
        bool OnButtonUp(MouseButton button, uint64_t tick, const Settings& s)
        {
            std::lock_guard<std::mutex> lock(m_mutex);
            ButtonState& st = State(button);

            if (st.swallowNextRealUp)
            {
                st.swallowNextRealUp = false;
                return true;
            }

            if (!st.physicalDown)
            {
                return false;
            }
            st.physicalDown = false;

            if (!Enabled(button, s))
            {
                return false;
            }

            const int holdMs = s.holdDurationMs < 0 ? 0 : s.holdDurationMs;
            const uint64_t elapsed = tick - st.downTick;
            if (!st.moveCancelled && elapsed >= static_cast<uint64_t>(holdMs))
            {
                // Suppress the physical up so the button stays held without the original click ever
                // completing. This matters for the right/middle buttons (a completed click would fire
                // a context menu / paste) and avoids a premature caret for the left button. The clean
                // release later (a single deferred injected up, see the adapter) is what preserves a
                // text selection made during the hold; injecting a fresh down here is not needed.
                st.locked.store(true);
                return true;
            }
            return false;
        }

        // Handle cursor movement (never suppressed). A cursor move beyond the dead-zone during the hold
        // marks the gesture as a drag (text selection, window/file drag) and cancels the pending lock,
        // so the button-up passes through normally instead of latching.
        void OnMove(uint64_t /*tick*/, PointL pt, const Settings& s)
        {
            std::lock_guard<std::mutex> lock(m_mutex);
            const int pixels = s.moveCancelPixels < 0 ? 0 : s.moveCancelPixels;
            CheckMoveCancel(m_left, pixels, pt);
            CheckMoveCancel(m_right, pixels, pt);
            CheckMoveCancel(m_middle, pixels, pt);
        }

        // Release any button whose lock has just been turned off in settings. No button is being
        // pressed here, so the injected up is not chorded: request the context-menu dismissal.
        void EnforceEnabled(const Settings& s)
        {
            std::lock_guard<std::mutex> lock(m_mutex);
            if (!s.lmbEnabled)
            {
                ReleaseButton(m_left, MouseButton::Left, /*dismissContextMenu=*/true);
            }
            if (!s.rmbEnabled)
            {
                ReleaseButton(m_right, MouseButton::Right, /*dismissContextMenu=*/true);
            }
            if (!s.mmbEnabled)
            {
                ReleaseButton(m_middle, MouseButton::Middle, /*dismissContextMenu=*/true);
            }
        }

        // Release every locked button (crash/shutdown safety). As with EnforceEnabled, these
        // releases are not chorded, so a surfaced context menu should be dismissed.
        void ReleaseAll()
        {
            std::lock_guard<std::mutex> lock(m_mutex);
            ReleaseButton(m_left, MouseButton::Left, /*dismissContextMenu=*/true);
            ReleaseButton(m_right, MouseButton::Right, /*dismissContextMenu=*/true);
            ReleaseButton(m_middle, MouseButton::Middle, /*dismissContextMenu=*/true);
        }

        // Clear transient hold state. Call when (re)enabling so a button held across a
        // disable/enable cycle can't produce a spurious lock or a swallowed later click.
        void ResetTransient()
        {
            std::lock_guard<std::mutex> lock(m_mutex);
            ResetOne(m_left);
            ResetOne(m_right);
            ResetOne(m_middle);
        }

        bool IsLocked(MouseButton button) const
        {
            std::lock_guard<std::mutex> lock(m_mutex);
            return State(button).locked.load();
        }

    private:
        // A release the engine already committed to (lock cleared, injection accepted) turned out not
        // to reach the OS, so the button is still physically held there. Three cases:
        //  - A same-button release tap whose paired physical UP has not arrived yet: stop swallowing
        //    it. The tap's DOWN was suppressed, so letting its UP through is exactly the release the
        //    injection failed to deliver, and the OS resolves the button itself.
        //  - A new physical press of this button is already in progress: the held OS state is
        //    explained by that press, whose own UP will complete the release, so there is nothing to
        //    repair. (The production injector detects failure by observing the button state shortly
        //    after injecting, so a fast re-press can look like a failed injection.)
        //  - Any other release (settings/lifecycle, cross-button, or a tap whose UP was already
        //    swallowed): restore the logical lock, so the state matches the OS again and the next
        //    press, settings change or shutdown retries the cleanup instead of leaving it stuck.
        void OnDeferredInjectUpFailed(MouseButton button, bool /*dismissContextMenu*/) override
        {
            std::lock_guard<std::mutex> lock(m_mutex);
            ButtonState& st = State(button);
            if (st.swallowNextRealUp)
            {
                st.swallowNextRealUp = false;
                return;
            }
            if (st.physicalDown)
            {
                return;
            }
            st.locked.store(true);
        }
        struct ButtonState
        {
            bool physicalDown = false;
            bool moveCancelled = false;
            bool swallowNextRealUp = false;
            uint64_t downTick = 0;
            PointL downPos{};
            std::atomic<bool> locked{ false };
        };

        ButtonState& State(MouseButton b)
        {
            switch (b)
            {
            case MouseButton::Left:
                return m_left;
            case MouseButton::Middle:
                return m_middle;
            case MouseButton::Right:
            default:
                return m_right;
            }
        }

        const ButtonState& State(MouseButton b) const
        {
            switch (b)
            {
            case MouseButton::Left:
                return m_left;
            case MouseButton::Middle:
                return m_middle;
            case MouseButton::Right:
            default:
                return m_right;
            }
        }

        static bool Enabled(MouseButton b, const Settings& s)
        {
            switch (b)
            {
            case MouseButton::Left:
                return s.lmbEnabled;
            case MouseButton::Middle:
                return s.mmbEnabled;
            case MouseButton::Right:
            default:
                return s.rmbEnabled;
            }
        }

        static void CheckMoveCancel(ButtonState& st, int pixels, PointL pt)
        {
            // Any motion beyond the dead-zone during the hold marks the gesture as a drag and cancels
            // the pending lock. (Motion after the button locks never reaches here: physicalDown is
            // already false by then, so a genuine lock-then-drag is unaffected.)
            if (!st.physicalDown || st.locked.load() || st.moveCancelled)
            {
                return;
            }
            // Compare squared distance in double. pt coordinates are 32-bit, so a raw long long
            // product (dx*dx + dy*dy) can overflow signed 64-bit for extreme inputs. In production
            // the cursor is screen-bounded so this never triggers, but the engine must stay defined
            // for any input, and the fuzz target drives the full coordinate range. double holds these
            // magnitudes without overflow; precision is far finer than a pixel dead-zone needs.
            const double dx = static_cast<double>(pt.x) - static_cast<double>(st.downPos.x);
            const double dy = static_cast<double>(pt.y) - static_cast<double>(st.downPos.y);
            const double threshold = static_cast<double>(pixels) * static_cast<double>(pixels);
            if (dx * dx + dy * dy > threshold)
            {
                st.moveCancelled = true;
            }
        }

        void ReleaseButton(ButtonState& st, MouseButton button, bool dismissContextMenu)
        {
            // exchange() claims the lock atomically so among racing releasers exactly one injects.
            // A synchronous rejection means the OS still holds the button: keep the logical lock so
            // the cleanup is retried later (a deferred rejection takes the same path through
            // OnDeferredInjectUpFailed).
            if (st.locked.exchange(false))
            {
                if (!m_injector.InjectUp(button, dismissContextMenu))
                {
                    st.locked.store(true);
                }
            }
        }

        // Release every locked button other than `keep` (the one currently being pressed). Used so any
        // button press frees a held button instead of leaving the mouse stuck on the locked one. The
        // injected up is chorded with `keep`'s press, so no context menu opens; never request the
        // dismissal here, or its keystroke would land on the foreground app instead.
        void ReleaseAllExcept(MouseButton keep)
        {
            if (keep != MouseButton::Left)
            {
                ReleaseButton(m_left, MouseButton::Left, /*dismissContextMenu=*/false);
            }
            if (keep != MouseButton::Right)
            {
                ReleaseButton(m_right, MouseButton::Right, /*dismissContextMenu=*/false);
            }
            if (keep != MouseButton::Middle)
            {
                ReleaseButton(m_middle, MouseButton::Middle, /*dismissContextMenu=*/false);
            }
        }

        static void ResetOne(ButtonState& st)
        {
            st.physicalDown = false;
            st.moveCancelled = false;
            st.swallowNextRealUp = false;
            st.downTick = 0;
        }

        mutable std::mutex m_mutex;
        IButtonUpInjector& m_injector;
        ButtonState m_left;
        ButtonState m_right;
        ButtonState m_middle;
    };
}
